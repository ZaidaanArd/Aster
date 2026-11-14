param(
    [switch]$RequireSession
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet build src/Aster.Avalonia/Aster.Avalonia.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "Phase 1 solution build failed." }

dotnet test src/Aster.Avalonia/Aster.Core.Tests/Aster.Core.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Core tests failed." }

dotnet test src/Aster.Avalonia/Aster.Platform.Windows.Tests/Aster.Platform.Windows.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Platform tests failed." }

dotnet test src/Aster.Avalonia/Aster.Avalonia.Tests/Aster.Avalonia.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Avalonia tests failed." }

$aotProbeLog = & powershell -ExecutionPolicy Bypass -File scripts/test-aot-probe.ps1 2>&1
$aotProbeLog | Tee-Object -FilePath artifacts/aot-probe.log
if ($LASTEXITCODE -ne 0) { throw "AOT probe failed." }
if ($aotProbeLog -match 'IL2\d{3}|IL3\d{3}') { throw "Trim warning in AOT probe." }

if ($RequireSession) {
    & artifacts/aot-probe/Aster.AotProbe.exe --controls
    if ($LASTEXITCODE -ne 0) { throw "Real-session AOT probe failed." }
}

$aotShellLog = & powershell -ExecutionPolicy Bypass -File scripts/test-aot-shell.ps1 2>&1
$aotShellLog | Tee-Object -FilePath artifacts/aot-shell.log
if ($LASTEXITCODE -ne 0) { throw "AOT shell failed." }
if ($aotShellLog -match 'IL2\d{3}|IL3\d{3}') { throw "Trim warning in AOT shell." }

dotnet build DynamicIsland.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "WPF regression build failed." }

Write-Host "Phase 1 verification PASS"
