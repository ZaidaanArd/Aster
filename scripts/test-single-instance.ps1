param(
    [string]$Exe = "src/Aster.Avalonia/Aster.Desktop.Windows/bin/Release/net10.0-windows10.0.19041.0/Aster.Avalonia.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet build src/Aster.Avalonia/Aster.Desktop.Windows/Aster.Desktop.Windows.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$first = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 1
$second = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 2

try {
    if (-not $second.HasExited) {
        throw "Second instance is still running."
    }
    if ($second.ExitCode -ne 1) {
        throw "Second instance exited with unexpected code $($second.ExitCode)."
    }
    Write-Host "Single instance PASS"
}
finally {
    if (-not $first.HasExited) { $first.Kill() }
    if (-not $second.HasExited) { $second.Kill() }
}
