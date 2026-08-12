@echo off
setlocal EnableExtensions

set "INST=%LocalAppData%\Programs\LfpHub\LfpHub.exe"
set "PUB=%~dp0publish\LfpHub.exe"
set "HERE=%~dp0LfpHub.exe"
set "REL=%~dp0bin\Release\net8.0-windows\LfpHub.exe"
set "DBG=%~dp0bin\Debug\net8.0-windows\LfpHub.exe"

if exist "%INST%" (
  start "" "%INST%"
  exit /b 0
)
if exist "%HERE%" (
  start "" "%HERE%"
  exit /b 0
)
if exist "%PUB%" (
  start "" "%PUB%"
  exit /b 0
)
if exist "%REL%" (
  start "" "%REL%"
  exit /b 0
)
if exist "%DBG%" (
  start "" "%DBG%"
  exit /b 0
)

echo LfpHub.exe not found. Build and install first:
echo   powershell -File "%~dp0build-release.ps1"
echo   "%~dp0install.bat"
pause
exit /b 1
