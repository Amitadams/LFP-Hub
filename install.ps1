#Requires -Version 5.1
<#
.SYNOPSIS
  Install LFP Hub to %LocalAppData%\Programs\LfpHub and create shortcuts.
#>
[CmdletBinding()]
param(
    [string]$SourceDir = "",
    [string]$InstallDir = "",
    [switch]$DesktopShortcut,
    [switch]$NoStartMenu,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"

function Get-SourceDir {
    param([string]$Hint)
    if ($Hint -and (Test-Path (Join-Path $Hint "LfpHub.exe"))) { return (Resolve-Path $Hint).Path }

    $here = $PSScriptRoot
    $candidates = @(
        $here,
        (Join-Path $here "publish"),
        (Join-Path (Split-Path $here -Parent) "publish")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c "LfpHub.exe"))) {
            return (Resolve-Path $c).Path
        }
    }
    throw "LfpHub.exe not found. Run build-release.ps1 first, or pass -SourceDir."
}

$SourceDir = Get-SourceDir -Hint $SourceDir
if (-not $InstallDir) {
    $InstallDir = Join-Path $env:LOCALAPPDATA "Programs\LfpHub"
}

$exeName = "LfpHub.exe"
$srcExe = Join-Path $SourceDir $exeName
if (-not (Test-Path $srcExe)) { throw "Missing $srcExe" }

Write-Host "==> Installing LFP Hub" -ForegroundColor Cyan
Write-Host "    From: $SourceDir"
Write-Host "    To:   $InstallDir"

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Copy payload (exclude nested install noise if re-running from install dir)
$skip = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($name in @("install.ps1", "install.bat", "uninstall.ps1")) { [void]$skip.Add($name) }

Get-ChildItem -Path $SourceDir -Force | ForEach-Object {
    if ($skip.Contains($_.Name) -and $SourceDir -eq $InstallDir) { return }
    $dest = Join-Path $InstallDir $_.Name
    if ($_.PSIsContainer) {
        Copy-Item $_.FullName $dest -Recurse -Force
    }
    else {
        Copy-Item $_.FullName $dest -Force
    }
}

# Always keep uninstall + launcher in install dir
Copy-Item (Join-Path $SourceDir "uninstall.ps1") (Join-Path $InstallDir "uninstall.ps1") -Force -ErrorAction SilentlyContinue
if (-not (Test-Path (Join-Path $InstallDir "uninstall.ps1"))) {
    $repoUninstall = Join-Path $PSScriptRoot "uninstall.ps1"
    if (Test-Path $repoUninstall) {
        Copy-Item $repoUninstall (Join-Path $InstallDir "uninstall.ps1") -Force
    }
}

$launcher = @"
@echo off
setlocal
set "EXE=%~dp0LfpHub.exe"
if not exist "%EXE%" (
  echo LfpHub.exe not found next to this launcher.
  pause
  exit /b 1
)
start "" "%EXE%"
exit /b 0
"@
Set-Content -Path (Join-Path $InstallDir "Open LFP Hub.bat") -Value $launcher -Encoding ASCII

$targetExe = Join-Path $InstallDir $exeName
if (-not (Test-Path $targetExe)) { throw "Install failed - $targetExe missing" }

function Get-ShortcutIconLocation {
    param([string]$Target)
    $dir = Split-Path $Target -Parent
    foreach ($name in @("LfpHub.ico", "Assets\LfpHub.ico")) {
        $candidate = Join-Path $dir $name
        if (Test-Path -LiteralPath $candidate) {
            return "$candidate,0"
        }
    }
    return "$Target,0"
}

function New-Shortcut {
    param(
        [string]$Path,
        [string]$Target,
        [string]$WorkingDirectory,
        [string]$Description = "LFP Hub"
    )
    $dir = Split-Path $Path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $w = New-Object -ComObject WScript.Shell
    $s = $w.CreateShortcut($Path)
    $s.TargetPath = $Target
    $s.WorkingDirectory = $WorkingDirectory
    $s.Description = $Description
    $s.IconLocation = Get-ShortcutIconLocation -Target $Target
    $s.Save()
}

if (-not $NoStartMenu) {
    $startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\LFP Hub"
    New-Shortcut -Path (Join-Path $startMenu "LFP Hub.lnk") `
        -Target $targetExe `
        -WorkingDirectory $InstallDir
    New-Shortcut -Path (Join-Path $startMenu "Uninstall LFP Hub.lnk") `
        -Target "powershell.exe" `
        -WorkingDirectory $InstallDir `
        -Description "Uninstall LFP Hub"
    # Fix uninstall shortcut args
    $w = New-Object -ComObject WScript.Shell
    $u = $w.CreateShortcut((Join-Path $startMenu "Uninstall LFP Hub.lnk"))
    $u.TargetPath = "powershell.exe"
    $u.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $InstallDir 'uninstall.ps1')`""
    $u.WorkingDirectory = $InstallDir
    $u.Description = "Uninstall LFP Hub"
    $u.Save()
    Write-Host "    Start Menu: $startMenu"
}

if ($DesktopShortcut) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    New-Shortcut -Path (Join-Path $desktop "LFP Hub.lnk") `
        -Target $targetExe `
        -WorkingDirectory $InstallDir
    Write-Host "    Desktop:    $(Join-Path $desktop 'LFP Hub.lnk')"
}

# Uninstall registry entry (per-user)
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\LfpHub"
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name "DisplayName" -Value "LFP Hub"
Set-ItemProperty -Path $regPath -Name "DisplayVersion" -Value "0.3.0"
Set-ItemProperty -Path $regPath -Name "Publisher" -Value "Tesla IT - GFNV"
Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $regPath -Name "DisplayIcon" -Value $targetExe
Set-ItemProperty -Path $regPath -Name "UninstallString" -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $InstallDir 'uninstall.ps1')`""
Set-ItemProperty -Path $regPath -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $regPath -Name "NoRepair" -Value 1 -Type DWord

Write-Host ""
Write-Host "Installed." -ForegroundColor Green
Write-Host "  Launch:  $targetExe"
Write-Host "  Or:      $(Join-Path $InstallDir 'Open LFP Hub.bat')"
Write-Host ""
Write-Host "First launch opens the tech-identity setup wizard." -ForegroundColor Yellow

if ($Launch) {
    Start-Process -FilePath $targetExe -WorkingDirectory $InstallDir
}
