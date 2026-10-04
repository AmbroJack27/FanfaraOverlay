; Inno Setup script for Fanfara — builds FanfaraSetup.exe
; Installs per-user (no administrator rights), creates Start Menu / optional desktop
; shortcuts, an optional "start with Windows" entry, and an uninstaller.

#define MyAppName "Fanfara"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.9"
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
; Let the installer update Fanfara even while it is running (used by the in-app auto-update).
AppMutex=FanfaraOverlayAppMutex
CloseApplications=yes
RestartApplications=yes
; Per-user install → no UAC / admin prompt.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; Follow the user's Windows language automatically, with no language-picker dialog.
; Inno matches the system UI language to one of the [Languages] below; if none match,
; it falls back to the first entry (English). The app has its own in-app language selector.
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "french";  MessagesFile: "compiler:Languages\French.isl"
Name: "german";  MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; Nota: l'avvio automatico con Windows si attiva/disattiva dentro l'app (Impostazioni),
; così c'e un unico punto di controllo e nessun disallineamento.

[Files]
; The self-contained publish folder (Fanfara.exe, web\, steam_api64.dll, ...) — config.json excluded here.
Source: "publish\*"; DestDir: "{app}"; Excludes: "config.json"; Flags: ignoreversion recursesubdirs createallsubdirs
; Install the default settings only on a fresh install, so updates keep the user's choices.
Source: "publish\config.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Non crea nulla all'installazione; rimuove la voce di avvio automatico
; (creata dall'app, se attivata) quando si disinstalla.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Fanfara"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove the settings file written next to the exe at runtime.
Type: files; Name: "{app}\config.json"
