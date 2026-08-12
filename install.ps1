#Requires -Version 5.1
<#
.SYNOPSIS
  Deprecated. Launches the Inno Setup installer if present.
#>
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$dist = Join-Path $Root "dist"
$setup = Get-ChildItem -LiteralPath $dist -Filter "LfpHub-*-Setup.exe" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

Write-Host ""
Write-Host "The PowerShell zip installer was removed." -ForegroundColor Yellow
Write-Host "It failed when Windows expanded the zip under a temp path with illegal characters."
Write-Host ""
Write-Host "Use the Inno Setup package instead:" -ForegroundColor Cyan
Write-Host "  dist\LfpHub-*-Setup.exe"
Write-Host ""
Write-Host "Build:  .\build-release.ps1"
Write-Host ""

if ($setup) {
    Write-Host "Launching $($setup.FullName)"
    Start-Process -FilePath $setup.FullName
    exit 0
}

Write-Host "No Setup.exe found. Run build-release.ps1 first." -ForegroundColor Red
exit 1
