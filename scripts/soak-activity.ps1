param(
    [int]$Minutes = 60
)

$ErrorActionPreference = 'SilentlyContinue'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class SoakActivityNative {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static List<IntPtr> WindowsForPid(uint pid) { var list = new List<IntPtr>(); EnumWindows((h,l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) list.Add(h); return true; }, IntPtr.Zero); return list; }
    public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
    public static string Class(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
}
"@
[SoakActivityNative]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$log = @()
function Say($s) { $script:log += $s; $s }
function Get-IslandUia {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Aster Lab Avalonia')
    $els = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $c)
    for ($i=0; $i -lt $els.Count; $i++) { if ($els.Item($i).Current.ClassName -eq 'MainWindow') { return $els.Item($i) } }
    return $null
}
$end = (Get-Date).AddMinutes($Minutes)
$cycles = 0
$tracks = 0
Say "start=$(Get-Date -Format o) minutes=$Minutes"
while ((Get-Date) -lt $end) {
    $p = Get-Process Aster.Avalonia -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { Start-Sleep -Seconds 2; continue }
    $hwnd = [IntPtr]::Zero
    foreach ($h in [SoakActivityNative]::WindowsForPid([uint32]$p.Id)) {
        if ([SoakActivityNative]::Title($h) -eq 'Aster Lab Avalonia' -and [SoakActivityNative]::Class($h) -like 'Avalonia-*') { $hwnd = $h; break }
    }
    if ($hwnd -eq [IntPtr]::Zero) { Start-Sleep -Seconds 2; continue }
    $r = New-Object SoakActivityNative+RECT
    [SoakActivityNative]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $cx = [int](($r.Left + $r.Right) / 2); $cy = [int](($r.Top + $r.Bottom) / 2)
    [SoakActivityNative]::SetCursorPos($cx, $cy) | Out-Null
    Start-Sleep -Milliseconds 1300
    [SoakActivityNative]::SetCursorPos(200, 500) | Out-Null
    Start-Sleep -Milliseconds 1200
    $cycles++
    if (($cycles % 5) -eq 0 -and $tracks -lt 20) {
        $island = Get-IslandUia
        if ($island) {
            $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Next')
            $next = $island.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
            if ($next) { $next.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); $tracks++; Say "track_change=$tracks cycle=$cycles at $(Get-Date -Format o)" }
        }
    }
    if (($cycles % 10) -eq 0) { Say "cycles=$cycles tracks=$tracks at $(Get-Date -Format o)" }
    Start-Sleep -Seconds 28
}
Say "done=$(Get-Date -Format o) cycles=$cycles tracks=$tracks"
$log | Set-Content artifacts/soak-activity.log
