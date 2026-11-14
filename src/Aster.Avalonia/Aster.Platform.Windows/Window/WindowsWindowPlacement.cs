using Aster.Core.Interfaces;
using static Aster.Platform.Windows.Window.NativeMethods;

namespace Aster.Platform.Windows.Window;

public readonly record struct MonitorRect(int Left, int Top, int Width, int Height);

public sealed class WindowsWindowPlacement
{
    private readonly IAppLog _log;

    public WindowsWindowPlacement(IAppLog log) => _log = log;

    public MonitorRect GetPrimaryMonitorRect()
    {
        var monitor = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            _log.Warn("Primary monitor lookup failed; using 1920x1080 fallback.");
            return new MonitorRect(0, 0, 1920, 1080);
        }

        return new MonitorRect(
            info.rcMonitor.Left,
            info.rcMonitor.Top,
            info.rcMonitor.Width,
            info.rcMonitor.Height);
    }

    public uint GetDpi(IntPtr hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi == 0 ? 96u : dpi;
    }

    public void SetBounds(IntPtr hwnd, int x, int y, int width, int height)
    {
        if (!SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE))
        {
            _log.Warn("SetWindowPos failed.");
        }
    }

    public void Hide(IntPtr hwnd) => ShowWindow(hwnd, SW_HIDE);

    public void ShowNoActivate(IntPtr hwnd) => ShowWindow(hwnd, SW_SHOWNOACTIVATE);
}
