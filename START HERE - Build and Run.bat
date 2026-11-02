@echo off
setlocal
cd /d "%~dp0"
if not exist "RobloxMultiHub.exe" (
  call "%~dp0Build.bat"
  if errorlevel 1 exit /b 1
)
start "" "%~dp0RobloxMultiHub.exe"
