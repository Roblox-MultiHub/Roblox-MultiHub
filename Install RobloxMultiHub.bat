@echo off
title Install Roblox MultiHub
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\install.ps1"
if errorlevel 1 pause
