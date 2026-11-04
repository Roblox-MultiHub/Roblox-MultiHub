# Roblox MultiHub - one-click installer (no admin needed).
# Copies the app into your user profile, makes Start-menu + Desktop shortcuts and an
# "Add or remove programs" entry. If RobloxMultiHub.exe is missing it is built first with
# the C# compiler that ships with Windows 10/11 - nothing to download.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)   # project root (parent of \installer)
$exe  = Join-Path $root 'RobloxMultiHub.exe'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\RobloxMultiHub'

Write-Host ''
Write-Host '  Roblox MultiHub - installer' -ForegroundColor Cyan
Write-Host '  ---------------------------'

# 1. build the exe if it is not already there
if (-not (Test-Path $exe)) {
    Write-Host '  Building RobloxMultiHub.exe (a few seconds)...'
    $csc = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) { $csc = "$env:windir\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    if (-not (Test-Path $csc)) { Write-Host '  Could not find the .NET Framework 4 compiler. Run Windows Update and try again.' -ForegroundColor Red; Read-Host '  Press Enter to close'; exit 1 }
    Push-Location $root
    & $csc /nologo /target:winexe /optimize+ /debug- /warn:1 /nowarn:0168,0219,0414,0649,0169,0067 /out:"$exe" /win32icon:app.ico /win32manifest:app.manifest /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Net.Http.dll /r:System.Security.dll /r:System.Management.dll /recurse:src\*.cs
    Pop-Location
    if (-not (Test-Path $exe)) { Write-Host '  Build failed - see the messages above.' -ForegroundColor Red; Read-Host '  Press Enter to close'; exit 1 }
}

# 2. copy the app into the install folder
Write-Host "  Installing to $dest"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $exe (Join-Path $dest 'RobloxMultiHub.exe') -Force
foreach ($f in 'app.ico','README.txt','README.md','LICENSE') {
    $p = Join-Path $root $f
    if (Test-Path $p) { Copy-Item $p (Join-Path $dest $f) -Force }
}
Copy-Item $MyInvocation.MyCommand.Path (Join-Path $dest 'install.ps1') -Force
$un = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'uninstall.ps1'
if (Test-Path $un) { Copy-Item $un (Join-Path $dest 'uninstall.ps1') -Force }

$destExe = Join-Path $dest 'RobloxMultiHub.exe'
$ico     = Join-Path $dest 'app.ico'
if (-not (Test-Path $ico)) { $ico = $destExe }

# 3. shortcuts (Start menu + Desktop)
function New-Shortcut($path, $target, $icon) {
    $ws = New-Object -ComObject WScript.Shell
    $s = $ws.CreateShortcut($path)
    $s.TargetPath = $target
    $s.WorkingDirectory = Split-Path -Parent $target
    $s.IconLocation = $icon
    $s.Description = 'Roblox MultiHub'
    $s.Save()
}
$startDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
New-Shortcut (Join-Path $startDir 'Roblox MultiHub.lnk') $destExe $ico
$desktop = [Environment]::GetFolderPath('Desktop')
New-Shortcut (Join-Path $desktop 'Roblox MultiHub.lnk') $destExe $ico

# 4. Add or remove programs entry (per-user, no admin)
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RobloxMultiHub'
New-Item -Path $key -Force | Out-Null
Set-ItemProperty $key 'DisplayName'     'Roblox MultiHub'
Set-ItemProperty $key 'DisplayIcon'     $ico
Set-ItemProperty $key 'InstallLocation' $dest
Set-ItemProperty $key 'Publisher'       'Roblox MultiHub'
Set-ItemProperty $key 'NoModify'        1
Set-ItemProperty $key 'NoRepair'        1
$unInstalled = Join-Path $dest 'uninstall.ps1'
if (Test-Path $unInstalled) {
    Set-ItemProperty $key 'UninstallString' "powershell -ExecutionPolicy Bypass -File `"$unInstalled`""
}

Write-Host ''
Write-Host '  Done! Roblox MultiHub is installed.' -ForegroundColor Green
Write-Host '  A shortcut is on your Desktop and in the Start menu.'
Write-Host ''
$ans = Read-Host '  Start Roblox MultiHub now? (Y/n)'
if ($ans -eq '' -or $ans -match '^[Yy]') { Start-Process $destExe }
