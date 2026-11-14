param(
    [string]$ProcessName = "Aster.Avalonia",
    [int]$Seconds = 30,
    [string]$Output = "docs/results/phase1-frames.csv"
)

$ErrorActionPreference = "Stop"
$presentMon = Get-Command PresentMon.exe -ErrorAction SilentlyContinue
if (-not $presentMon) {
    throw "PresentMon.exe not found. Install PresentMon, then rerun this script."
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
& $presentMon.Source --process_name $ProcessName --output_file $Output --timed $Seconds --terminate_on_proc_exit
if ($LASTEXITCODE -ne 0) { throw "PresentMon capture failed." }
Write-Host "Frame capture written to $Output"
