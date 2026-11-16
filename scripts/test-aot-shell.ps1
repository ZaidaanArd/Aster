param(
    [string]$Output = "artifacts/win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet publish src/Aster.Avalonia/Aster.Desktop.Windows/Aster.Desktop.Windows.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=partial `
  -p:StripSymbols=true -p:InvariantGlobalization=false -p:DebugType=None `
  -p:CopyOutputSymbolsToPublishDirectory=false -o $Output

if ($LASTEXITCODE -ne 0) {
    throw "AOT shell publish failed."
}

$exe = Join-Path $Output "Aster.Avalonia.exe"
if (-not (Test-Path $exe)) {
    throw "AOT shell executable not found."
}

$ready = Join-Path $env:TEMP ("aster-aot-shell-" + [guid]::NewGuid().ToString("N") + ".ready")
$env:ASTER_READY_FILE = $ready
$process = Start-Process -FilePath $exe -PassThru

try {
    for ($i = 0; $i -lt 150; $i++) {
        if (Test-Path $ready) { break }
        if ($process.HasExited) { throw "AOT shell exited before writing the ready file." }
        Start-Sleep -Milliseconds 100
    }

    if (-not (Test-Path $ready)) {
        throw "AOT shell ready file was not written."
    }

    Write-Host "AOT shell stage 2 PASS"
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
    Remove-Item $ready -ErrorAction SilentlyContinue
}
