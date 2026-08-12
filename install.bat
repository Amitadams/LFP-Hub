@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "PS1=%~dp0install.ps1"
if not exist "%PS1%" (
  echo install.ps1 not found next to this script.
  pause
  exit /b 1
)

REM Prefer published app beside this bat, else .\publish
set "SRC="
if exist "%~dp0LfpHub.exe" set "SRC=%~dp0"
if not defined SRC if exist "%~dp0publish\LfpHub.exe" set "SRC=%~dp0publish\"

if defined SRC (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -SourceDir "%SRC%" -DesktopShortcut %*
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -DesktopShortcut %*
)

set "ERR=%ERRORLEVEL%"
if not "%ERR%"=="0" (
  echo Install failed with exit %ERR%.
  pause
  exit /b %ERR%
)

echo.
echo Done. Use Start Menu "LFP Hub", Desktop shortcut, or:
echo   %%LocalAppData%%\Programs\LfpHub\Open LFP Hub.bat
echo.
exit /b 0
