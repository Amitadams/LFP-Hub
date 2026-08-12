@echo off
setlocal EnableExtensions
cd /d "%~dp0"

REM Dev launcher: prefer local build, else installed app.
set "EXE="
if exist "%~dp0bin\Release\net8.0-windows\LfpHub.exe" set "EXE=%~dp0bin\Release\net8.0-windows\LfpHub.exe"
if not defined EXE if exist "%~dp0bin\Debug\net8.0-windows\LfpHub.exe" set "EXE=%~dp0bin\Debug\net8.0-windows\LfpHub.exe"
if not defined EXE if exist "%~dp0publish\LfpHub.exe" set "EXE=%~dp0publish\LfpHub.exe"
if not defined EXE if exist "%LocalAppData%\Programs\LfpHub\LfpHub.exe" set "EXE=%LocalAppData%\Programs\LfpHub\LfpHub.exe"

if not defined EXE (
  echo Building LFP Hub...
  dotnet build -c Release --nologo
  if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
  )
  set "EXE=%~dp0bin\Release\net8.0-windows\LfpHub.exe"
)

if not exist "%EXE%" (
  echo LfpHub.exe not found.
  pause
  exit /b 1
)

start "" "%EXE%"
exit /b 0
