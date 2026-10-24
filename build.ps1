param(
    [switch]$Release,
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"

if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
} else {
    $env:DOTNET_ROOT = Split-Path $dotnet
}

$project = Join-Path $root "src\DynamicIsland\DynamicIsland.csproj"

if ($Publish) {
    & $dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None -p:DebugSymbols=false `
        -o (Join-Path $root "dist")
} else {
    $configuration = if ($Release) { "Release" } else { "Debug" }
    & $dotnet build $project -c $configuration
}
