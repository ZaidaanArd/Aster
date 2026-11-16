param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Output
)

$ErrorActionPreference = "Stop"

if ($env:DOTNET_ROOT -and (Test-Path $env:DOTNET_ROOT)) {
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
New-Item -ItemType Directory -Force -Path $Output | Out-Null

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("aster-package-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $stage | Out-Null

try {
    $publish = Join-Path $stage "publish"
    $log = Join-Path $stage "publish.log"

    dotnet publish src/Aster.Avalonia/Aster.Desktop.Windows/Aster.Desktop.Windows.csproj `
        -c Release -r win-x64 --self-contained true `
        -p:Version=$Version `
        -p:PublishAot=true -p:PublishTrimmed=true -p:TrimMode=partial `
        -p:DebugType=None -p:CopyOutputSymbolsToPublishDirectory=false `
        -o $publish 2>&1 | Tee-Object -FilePath $log

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
    if (Select-String -Path $log -Pattern 'IL2\d{3}|IL3\d{3}') {
        throw "trim warning in AOT publish"
    }

    $required = @(
        "Aster.Avalonia.exe",
        "av_libglesv2.dll",
        "libSkiaSharp.dll",
        "libHarfBuzzSharp.dll"
    )
    foreach ($file in $required) {
        if (-not (Test-Path (Join-Path $publish $file))) {
            throw "missing publish output: $file"
        }
    }

    $name = "Aster-$Version-win-x64"
    $dir = Join-Path $stage $name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    Copy-Item (Join-Path $publish "Aster.Avalonia.exe") (Join-Path $dir "Aster.exe")
    foreach ($file in @("av_libglesv2.dll", "libSkiaSharp.dll", "libHarfBuzzSharp.dll")) {
        Copy-Item (Join-Path $publish $file) $dir
    }
    Copy-Item (Join-Path $root "LICENSE") $dir
    $readme = (Get-Content (Join-Path $root "packaging\README.txt") -Raw).Replace("{{VERSION}}", $Version)
    [System.IO.File]::WriteAllText((Join-Path $dir "README.txt"), $readme)
    Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.txt") $dir

    $expected = @(
        "Aster.exe",
        "av_libglesv2.dll",
        "libHarfBuzzSharp.dll",
        "libSkiaSharp.dll",
        "LICENSE",
        "README.txt",
        "THIRD-PARTY-NOTICES.txt"
    ) | Sort-Object
    $actual = Get-ChildItem $dir -File | Select-Object -ExpandProperty Name | Sort-Object
    if (Compare-Object $expected $actual) {
        throw "unexpected archive files: $($actual -join ', ')"
    }

    $archive = Join-Path $Output "$name.zip"
    Remove-Item $archive -ErrorAction SilentlyContinue
    Compress-Archive -Path $dir -DestinationPath $archive
    if (-not (Test-Path $archive) -or (Get-Item $archive).Length -eq 0) {
        throw "archive missing or empty: $archive"
    }

    Get-FileHash $archive -Algorithm SHA256 | Format-List
    Write-Output $archive
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
