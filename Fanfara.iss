; Inno Setup script for Fanfara — builds FanfaraSetup.exe
; Installs per-user (no administrator rights), creates Start Menu / optional desktop
; shortcuts, an optional "start with Windows" entry, and an uninstaller.

#define MyAppName "Fanfara"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.3"
#endif
#define MyAppPublisher "AmbroJack27"
#define MyAppExeName "Fanfara.exe"

[Setup]
; A fixed AppId keeps upgrades/uninstall consistent across versions.
AppId={{8F3A6B21-4C7D-4E0A-9B55-FA4E1A2C0F01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer_output
OutputBaseFilename=FanfaraSetup
SetupIconFile=src\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Per-user install → no UAC / admin prompt.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Avvia Fanfara all'accensione di Windows"; GroupDescription: "Avvio automatico:"

[Files]
; The whole self-contained publish folder (Fanfara.exe, web\, steam_api64.dll, config.json, ...)
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Disinstalla {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Optional "run at Windows startup" — same key the in-app toggle uses.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "Fanfara"; ValueData: """{app}\{#MyAppExeName}"" --autostart"; \
  Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove the settings file written next to the exe at runtime.
Type: files; Name: "{app}\config.json"
