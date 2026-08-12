@echo off
setlocal EnableExtensions
cd /d "%~dp0"

REM Legacy entry point — real installs use Inno Setup (dist\LfpHub-*-Setup.exe).
echo.
echo LFP Hub no longer uses install.bat / PowerShell for setup.
echo That path breaks when Windows unzips under a temp folder.
echo.
echo Use the Windows installer instead:
echo   dist\LfpHub-*-Setup.exe
echo.
echo Build it with:
echo   powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1"
echo.

set "SETUP="
for %%F in ("%~dp0dist\LfpHub-*-Setup.exe") do set "SETUP=%%~fF"
if defined SETUP if exist "%SETUP%" (
  echo Launching: %SETUP%
  start "" "%SETUP%"
  exit /b 0
)

echo No Setup.exe found yet. Run build-release.ps1 first.
pause
exit /b 1
