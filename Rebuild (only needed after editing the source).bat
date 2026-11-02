@echo off
cd /d "%~dp0"
call "%~dp0Build.bat"
if errorlevel 1 exit /b 1
start "" "%~dp0RobloxMultiHub.exe"
