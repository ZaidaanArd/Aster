param(
    [string]$Exe = "artifacts/win-x64/Aster.Avalonia.exe",
    [int]$Minutes = 60,
    [string]$Csv = "docs/results/phase2-soak-windows.csv",
    [string]$ActivityLog = "artifacts/soak-activity.log",
    [switch]$SkipActivityCheck
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Csv) | Out-Null
Remove-Item $Csv -ErrorAction SilentlyContinue
# Start the soak before the activity harness; this removes any previous run's log.
if (-not $SkipActivityCheck) {
    Remove-Item $ActivityLog -ErrorAction SilentlyContinue
}

function Get-SlopeMBPerHour($samples, [int]$from, [int]$to) {
    $points = @($samples | Where-Object { $_.Minute -ge $from -and $_.Minute -le $to })
    if ($points.Count -lt 2) { return 0 }
    $n = $points.Count
    $sumX = 0.0; $sumY = 0.0; $sumXY = 0.0; $sumX2 = 0.0
    foreach ($p in $points) {
        $x = [double]$p.Minute
        $y = [double]$p.WorkingSetMB
        $sumX += $x; $sumY += $y; $sumXY += $x * $y; $sumX2 += $x * $x
    }
    $denominator = ($n * $sumX2) - ($sumX * $sumX)
    if ($denominator -eq 0) { return 0 }
    return ((($n * $sumXY) - ($sumX * $sumY)) / $denominator) * 60
}

$process = Start-Process -FilePath $Exe -PassThru
$samples = @()

try {
    for ($i = 1; $i -le $Minutes; $i++) {
        Start-Sleep -Seconds 60
        $p = Get-Process -Id $process.Id -ErrorAction Stop
        $sample = [pscustomobject]@{
            Minute       = $i
            WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
            PrivateMB    = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
            Handles      = $p.HandleCount
            Timestamp    = (Get-Date).ToString("O")
        }
        $samples += $sample
        if ($i -eq 1) {
            $sample | Export-Csv -NoTypeInformation -Path $Csv
        }
        else {
            $sample | Export-Csv -NoTypeInformation -Append -Path $Csv
        }
        Write-Host ("minute {0}: {1} MB, {2} handles" -f $i, $sample.WorkingSetMB, $sample.Handles)
    }

    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        "Aster Lab Avalonia")
    $typeCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $trayCondition = New-Object System.Windows.Automation.AndCondition(
        $nameCondition, $typeCondition)
    $tray = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $trayCondition)
    if ($null -eq $tray) {
        throw "Tray icon was not found at the end of the soak. Open the notification overflow and rerun the check."
    }
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
}

if (-not $SkipActivityCheck) {
    if (-not (Test-Path $ActivityLog)) {
        throw "Activity log is missing: $ActivityLog"
    }

    $activity = Get-Content $ActivityLog -Raw
    $cycleMatches = [regex]::Matches($activity, 'cycles=(\d+)')
    $trackMatches = [regex]::Matches($activity, 'tracks=(\d+)')
    if ($cycleMatches.Count -eq 0 -or
        [int]$cycleMatches[$cycleMatches.Count - 1].Groups[1].Value -lt 100) {
        throw "Activity log does not evidence 100 expand/collapse cycles."
    }
    if ($trackMatches.Count -eq 0 -or
        [int]$trackMatches[$trackMatches.Count - 1].Groups[1].Value -lt 20) {
        throw "Activity log does not evidence 20 track changes."
    }
}

if ($Minutes -lt 60) {
    Write-Host "Dry run complete; no PASS verdict."
    exit 0
}

$baselineSamples = @($samples | Where-Object { $_.Minute -le 10 })
$baseline = ($baselineSamples | Measure-Object WorkingSetMB -Average).Average
$last = $samples[-1].WorkingSetMB
$handleSamples = @($samples | Where-Object { $_.Minute -ge 10 })
$handleMin = ($handleSamples | Measure-Object Handles -Minimum).Minimum
$handleMax = ($handleSamples | Measure-Object Handles -Maximum).Maximum
$slope3060 = Get-SlopeMBPerHour $samples 30 60
$slope2060 = Get-SlopeMBPerHour $samples 20 60
$slope1060 = Get-SlopeMBPerHour $samples 10 60

$summary = [pscustomobject]@{
    BaselineMB      = [math]::Round($baseline, 1)
    Minute60MB      = $last
    PercentOfBase   = [math]::Round(($last / $baseline) * 100, 1)
    HandleMin       = $handleMin
    HandleMax       = $handleMax
    HandleRange     = $handleMax - $handleMin
    Slope10To60     = [math]::Round($slope1060, 1)
    Slope20To60     = [math]::Round($slope2060, 1)
    Slope30To60     = [math]::Round($slope3060, 1)
}
$summary | Format-List

if ($last -gt $baseline * 1.10) { throw "Working set exceeded 110% of baseline." }
if (($handleMax - $handleMin) -gt 10) { throw "Handle range exceeded 10 in minutes 10-60." }
if ($slope3060 -gt 5) { throw "Working-set trend 30-60 exceeded 5 MB/hour." }

Write-Host "Soak PASS"
