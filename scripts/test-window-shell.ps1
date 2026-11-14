param(
    [string]$Exe = "src/Aster.Avalonia/Aster.Avalonia/bin/Release/net10.0-windows10.0.19041.0/Aster.Avalonia.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet build src/Aster.Avalonia/Aster.Avalonia/Aster.Avalonia.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$ready = Join-Path $env:TEMP ("aster-shell-" + [guid]::NewGuid().ToString("N") + ".ready")
$env:ASTER_READY_FILE = $ready
$process = Start-Process -FilePath $Exe -PassThru

try {
    for ($i = 0; $i -lt 100; $i++) {
        if (Test-Path $ready) { break }
        if ($process.HasExited) { throw "Aster.Avalonia exited before writing the ready file." }
        Start-Sleep -Milliseconds 100
    }

    if (-not (Test-Path $ready)) { throw "Ready file was not written." }
    Write-Host "Window shell PASS"
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
    Remove-Item $ready -ErrorAction SilentlyContinue
}
