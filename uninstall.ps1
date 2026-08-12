#Requires -Version 5.1
<#
.SYNOPSIS
  Remove LFP Hub install (Programs folder, shortcuts, uninstall registry key).
  Does not delete %LocalAppData%\LfpHub config unless -RemoveConfig is set.
#>
[CmdletBinding()]
param(
    [switch]$RemoveConfig
)

$ErrorActionPreference = "Stop"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\LfpHub"

Write-Host "==> Uninstalling LFP Hub" -ForegroundColor Cyan

$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\LFP Hub"
if (Test-Path $startMenu) {
    Remove-Item $startMenu -Recurse -Force
    Write-Host "    Removed Start Menu folder"
}

$desktopLnk = Join-Path ([Environment]::GetFolderPath("Desktop")) "LFP Hub.lnk"
if (Test-Path $desktopLnk) {
    Remove-Item $desktopLnk -Force
    Write-Host "    Removed desktop shortcut"
}

$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\LfpHub"
if (Test-Path $regPath) {
    Remove-Item $regPath -Recurse -Force
    Write-Host "    Removed uninstall registry key"
}

if (Test-Path $InstallDir) {
    # If running from inside install dir, schedule delete after exit
    $runningFromInstall = $PSScriptRoot -and (
        [string]::Equals(
            (Resolve-Path $PSScriptRoot).Path.TrimEnd('\'),
            (Resolve-Path $InstallDir).Path.TrimEnd('\'),
            [StringComparison]::OrdinalIgnoreCase)
    )
    if ($runningFromInstall) {
        $cmd = "Start-Sleep -Seconds 2; Remove-Item -LiteralPath '$InstallDir' -Recurse -Force -ErrorAction SilentlyContinue"
        Start-Process powershell.exe -ArgumentList @("-NoProfile", "-Command", $cmd) -WindowStyle Hidden
        Write-Host "    Scheduled removal of $InstallDir"
    }
    else {
        Remove-Item $InstallDir -Recurse -Force
        Write-Host "    Removed $InstallDir"
    }
}

if ($RemoveConfig) {
    $cfg = Join-Path $env:LOCALAPPDATA "LfpHub"
    if (Test-Path $cfg) {
        Remove-Item $cfg -Recurse -Force
        Write-Host "    Removed config $cfg"
    }
}

Write-Host "Uninstall complete." -ForegroundColor Green
