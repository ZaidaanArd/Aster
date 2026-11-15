param(
    [string]$Output = "artifacts/aot-probe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet publish src/Aster.Avalonia/tools/Aster.AotProbe/Aster.AotProbe.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=partial `
  -p:StripSymbols=true -p:InvariantGlobalization=false -p:DebugType=None `
  -p:CopyOutputSymbolsToPublishDirectory=false -o $Output

if ($LASTEXITCODE -ne 0) {
    throw "AOT publish failed."
}

$probe = Join-Path $Output "Aster.AotProbe.exe"
if (-not (Test-Path $probe)) {
    throw "AOT probe executable not found."
}

# Windows PowerShell 5.1 can collapse native stdout into one string when a large
# publish command precedes it, so match the marker instead of testing array items.
$output = (& $probe --allow-no-session) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "AOT probe failed with exit code $LASTEXITCODE."
}
if ($output -notmatch 'AOT_PROBE_OK') {
    throw "AOT probe did not report AOT_PROBE_OK."
}

Write-Host "AOT probe stage 1 PASS"
