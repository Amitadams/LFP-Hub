#Requires -Version 5.1
<#
.SYNOPSIS
  Publish LFP Hub and build a Windows Setup.exe with Inno Setup 6.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "",
    [string]$IsccPath = ""
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $Root "publish" }

function Get-ProjectVersion {
    param([string]$Csproj)
    [xml]$proj = Get-Content -LiteralPath $Csproj
    foreach ($pg in $proj.Project.PropertyGroup) {
        if ($pg.Version) { return [string]$pg.Version }
    }
    return "0.0.0"
}

function Find-Iscc {
    param([string]$Hint)
    if ($Hint -and (Test-Path -LiteralPath $Hint)) { return (Resolve-Path -LiteralPath $Hint).Path }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return (Resolve-Path -LiteralPath $c).Path }
    }

    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    throw @"
Inno Setup 6 compiler (ISCC.exe) not found.
Install: winget install JRSoftware.InnoSetup
Or pass -IsccPath 'C:\Path\To\ISCC.exe'
"@
}

$csproj = Join-Path $Root "LfpHub.csproj"
if (-not (Test-Path -LiteralPath $csproj)) { throw "LfpHub.csproj not found at $Root" }

$core = Join-Path $Root "..\DesksideHub\DesksideHub.Core\DesksideHub.Core.csproj"
if (-not (Test-Path -LiteralPath $core)) {
    throw "Sibling DesksideHub.Core not found at $core"
}

$ver = Get-ProjectVersion -Csproj $csproj
$iss = Join-Path $Root "installer\LfpHub.iss"
if (-not (Test-Path -LiteralPath $iss)) { throw "Missing $iss" }

$ico = Join-Path $Root "Assets\LfpHub.ico"
if (-not (Test-Path -LiteralPath $ico)) {
    $gen = Join-Path $Root "Assets\generate-icon.ps1"
    if (Test-Path -LiteralPath $gen) {
        Write-Host "==> Generating app icon..." -ForegroundColor Cyan
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $gen
    }
}
if (-not (Test-Path -LiteralPath $ico)) { throw "Missing Assets\LfpHub.ico" }

Write-Host "==> Publishing LFP Hub v$ver ($Configuration, framework-dependent win-x64)..." -ForegroundColor Cyan
if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

dotnet publish $csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -o $OutputDir `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $OutputDir "LfpHub.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "LfpHub.exe missing after publish: $exe" }

# Icon next to published exe (also packed by Inno)
Copy-Item -LiteralPath $ico -Destination (Join-Path $OutputDir "LfpHub.ico") -Force

$dist = Join-Path $Root "dist"
if (-not (Test-Path -LiteralPath $dist)) {
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
}

$iscc = Find-Iscc -Hint $IsccPath
Write-Host "==> Building Setup.exe with Inno Setup..." -ForegroundColor Cyan
Write-Host "    ISCC: $iscc"

$sourceDir = (Resolve-Path -LiteralPath $OutputDir).Path
$outDir = (Resolve-Path -LiteralPath $dist).Path

& $iscc `
    "/DMyAppVersion=$ver" `
    "/DSourceDir=$sourceDir" `
    "/DOutputDir=$outDir" `
    $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setupName = "LfpHub-$ver-Setup.exe"
$setupPath = Join-Path $dist $setupName
if (-not (Test-Path -LiteralPath $setupPath)) {
    # Fallback: newest Setup.exe in dist
    $setupPath = Get-ChildItem -LiteralPath $dist -Filter "LfpHub-*-Setup.exe" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $setupPath -or -not (Test-Path -LiteralPath $setupPath)) {
    throw "Setup.exe not produced in $dist"
}

# Optional portable zip (app only, no PowerShell installer)
$zipName = "LfpHub-$ver-win-x64-portable.zip"
$zipPath = Join-Path $dist $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Build OK  v$ver" -ForegroundColor Green
Write-Host "  App:     $exe"
Write-Host "  Setup:   $setupPath"
Write-Host "  Portable zip (optional): $zipPath"
Write-Host ""
Write-Host "Install:  double-click $setupName" -ForegroundColor Yellow
Write-Host "Requires: .NET 8 Desktop Runtime (x64)"
