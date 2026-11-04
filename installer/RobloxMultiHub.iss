; Roblox MultiHub - Inno Setup script (for a real .exe installer, like RoALT ships).
; Build it with Inno Setup 6+ :   ISCC.exe installer\RobloxMultiHub.iss
; It expects a prebuilt RobloxMultiHub.exe in the project root (run Build.bat first).
; Output: installer\Output\RobloxMultiHub-Setup.exe  (per-user, no admin needed).

#define AppName "Roblox MultiHub"
#define AppVersion "1.3.5"
#define AppExe "RobloxMultiHub.exe"

[Setup]
AppId={{B6B6F3A2-7C2E-4C2B-9F1A-ROBLOXMULTIHUB}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Roblox MultiHub
DefaultDirName={localappdata}\Programs\RobloxMultiHub
DefaultGroupName=Roblox MultiHub
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=RobloxMultiHub-Setup
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\RobloxMultiHub.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\app.ico";           DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md";         DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE";           DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Roblox MultiHub"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"
Name: "{userdesktop}\Roblox MultiHub"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start Roblox MultiHub"; Flags: nowait postinstall skipifsilent
