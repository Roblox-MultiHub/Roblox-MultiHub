# Roblox MultiHub - uninstaller. Removes the app, the shortcuts and the Add/remove-programs
# entry. Your accounts and settings (in %AppData%\RobloxMultiHub) are kept unless you say otherwise.

$ErrorActionPreference = 'SilentlyContinue'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\RobloxMultiHub'

Write-Host ''
Write-Host '  Uninstalling Roblox MultiHub...' -ForegroundColor Cyan

# shortcuts
Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Roblox MultiHub.lnk') -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Roblox MultiHub.lnk') -Force
# startup entry (if "Start with Windows" was on)
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'RobloxMultiHub' -ErrorAction SilentlyContinue
# add/remove-programs entry
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RobloxMultiHub' -Recurse -Force

$ans = Read-Host '  Also delete your saved accounts and settings? (y/N)'
if ($ans -match '^[Yy]') {
    Remove-Item (Join-Path $env:APPDATA 'RobloxMultiHub') -Recurse -Force
    Write-Host '  Accounts and settings deleted.'
} else {
    Write-Host '  Accounts and settings kept in %AppData%\RobloxMultiHub.'
}

# the program files (do this last; the folder holds this script)
Start-Process powershell -ArgumentList "-NoProfile -WindowStyle Hidden -Command `"Start-Sleep 1; Remove-Item -Recurse -Force '$dest'`""
Write-Host '  Done.' -ForegroundColor Green
Start-Sleep 2
