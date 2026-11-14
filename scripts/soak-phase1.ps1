param(
    [string]$Exe = "artifacts/win-x64/Aster.Avalonia.exe",
    [int]$Minutes = 60
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force -Path "docs/results" | Out-Null
$process = Start-Process -FilePath $Exe -PassThru
$samples = @()

try {
    for ($i = 0; $i -lt $Minutes; $i++) {
        Start-Sleep -Seconds 60
        $p = Get-Process -Id $process.Id -ErrorAction Stop
        $samples += [pscustomobject]@{
            Minute       = $i + 1
            WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
            PrivateMB    = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
            Handles      = $p.HandleCount
        }
    }
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
}

$samples | Export-Csv -NoTypeInformation -Path "docs/results/phase1-soak.csv"

$firstTen = $samples | Select-Object -First 10
$baseline = ($firstTen | Measure-Object WorkingSetMB -Average).Average
$last = $samples[-1].WorkingSetMB
$handleMin = ($samples | Measure-Object Handles -Minimum).Minimum
$handleMax = ($samples | Measure-Object Handles -Maximum).Maximum

if ($last -gt $baseline * 1.10) { throw "Working set grew beyond 110% of the 10-minute baseline." }
if (($handleMax - $handleMin) -gt 10) { throw "Handle count moved by more than 10." }

Write-Host "Soak PASS: baseline=$baseline MB last=$last MB handles=$handleMin..$handleMax"
