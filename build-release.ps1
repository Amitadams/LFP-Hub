#Requires -Version 5.1
<#
.SYNOPSIS
  Publish LFP Hub (framework-dependent net8.0-windows) and stage installer assets.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $Root "publish" }

$csproj = Join-Path $Root "LfpHub.csproj"
if (-not (Test-Path $csproj)) { throw "LfpHub.csproj not found at $Root" }

$core = Join-Path $Root "..\DesksideHub\DesksideHub.Core\DesksideHub.Core.csproj"
if (-not (Test-Path $core)) {
    throw "Sibling DesksideHub.Core not found at $core"
}

Write-Host "==> Publishing LFP Hub ($Configuration, framework-dependent)..." -ForegroundColor Cyan
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
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
if (-not (Test-Path $exe)) { throw "LfpHub.exe missing after publish: $exe" }

# Stage installer + launcher next to the published app
Copy-Item (Join-Path $Root "install.ps1") $OutputDir -Force
Copy-Item (Join-Path $Root "install.bat") $OutputDir -Force
Copy-Item (Join-Path $Root "Open LFP Hub.bat") $OutputDir -Force
Copy-Item (Join-Path $Root "uninstall.ps1") $OutputDir -Force

$dist = Join-Path $Root "dist"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist -Force | Out-Null

$zipName = "LfpHub-win-x64.zip"
$zipPath = Join-Path $dist $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath -Force

# Convenience copies at repo root / dist for double-click install
Copy-Item (Join-Path $Root "install.bat") $dist -Force
Copy-Item (Join-Path $Root "Open LFP Hub.bat") $dist -Force

Write-Host ""
Write-Host "Publish OK" -ForegroundColor Green
Write-Host "  App:       $exe"
Write-Host "  Installer: $(Join-Path $OutputDir 'install.bat')"
Write-Host "  Zip:       $zipPath"
Write-Host ""
Write-Host "Next:  .\publish\install.bat" -ForegroundColor Yellow
Write-Host "   or: .\publish\install.ps1 -DesktopShortcut"
