param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$Label = "app",
    [int]$Runs = 5,
    [int]$IdleSeconds = 0,
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path "docs/results" | Out-Null

$binaryMb = [math]::Round((Get-Item $ExePath).Length / 1MB, 1)
$rows = @()

for ($i = 1; $i -le $Runs; $i++) {
    $readyFile = Join-Path $env:TEMP "$Label-$i.ready"
    Remove-Item $readyFile -ErrorAction SilentlyContinue
    $env:ASTER_READY_FILE = $readyFile

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath $ExePath -PassThru
    $ready = $false
    $startupMs = -1

    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (Test-Path $readyFile) {
            $startupMs = [int](Get-Content $readyFile)
            $ready = $true
            break
        }
        if ($proc.HasExited) { break }
        $proc.Refresh()
        if ($proc.MainWindowHandle -ne 0) {
            $startupMs = $watch.ElapsedMilliseconds
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 50
    }
    $cpuStart = $proc.TotalProcessorTime.TotalSeconds
    $idleClock = [System.Diagnostics.Stopwatch]::StartNew()

    if ($IdleSeconds -gt 0) {
        Start-Sleep -Seconds $IdleSeconds
    }

    $idleClock.Stop()
    $proc.Refresh()
    $cpuEnd = $proc.TotalProcessorTime.TotalSeconds
    $cpuPercent = if ($idleClock.Elapsed.TotalSeconds -gt 0) {
        [math]::Round((($cpuEnd - $cpuStart) / $idleClock.Elapsed.TotalSeconds) * 100, 2)
    } else { 0 }

    $binaryFolderMb = [math]::Round(
        (Get-ChildItem (Split-Path -Parent $ExePath) -File | Measure-Object Length -Sum).Sum / 1MB, 1)

    $rows += [pscustomobject]@{
        Label          = $Label
        Run            = $i
        Cold           = $i -eq 1
        Ready          = $ready
        StartupMs      = $startupMs
        WorkingSetMB   = [math]::Round($proc.WorkingSet64 / 1MB, 1)
        PrivateMemoryMB = [math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
        CpuPercent     = $cpuPercent
        BinaryMB       = $binaryMb
        BinaryFolderMB = $binaryFolderMb
    }

    $null = $proc.CloseMainWindow()
    if (-not $proc.WaitForExit(3000)) { $proc.Kill() }
    Remove-Item $readyFile -ErrorAction SilentlyContinue
}

$rows | Format-Table -AutoSize
$rows | Export-Csv -NoTypeInformation -Path "docs/results/$Label.csv"
