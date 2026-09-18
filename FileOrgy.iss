; =====================================================================
; FileOrgy - Inno Setup 6 Installer Script
; Produces: installer\FileOrgy-Setup-v1.0.0.exe
; Sources:  dist\*
; =====================================================================

#define MyAppName "FileOrgy"
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "FileOrgy"
#define MyAppURL "https://github.com/almadinaabudhabi/FileOrgy"
#define MyAppExeName "FileOrgy.exe"

[Setup]
AppId={{8B2F3D14-949A-4E38-9B7C-A57B96A831E0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=FileOrgy-Setup-v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=100
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
SetupIconFile=assets\app.ico
WizardImageFile=assets\wizard_side.bmp
WizardSmallImageFile=assets\wizard_top.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}
AppComments=Lightweight real-time folder automation and multi-step workflow pipeline for Windows.
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=FileOrgy Windows Installer
VersionInfoCopyright=Copyright (C) 2026 FileOrgy. All rights reserved.
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to FileOrgy Setup
WelcomeLabel2=FileOrgy is an intelligent, real-time desktop automation pipeline that sorts, extracts, renames, and cleans your files automatically in the background.%n%nThis setup will install FileOrgy with administrative privileges into Program Files, configure background folder monitoring, and enable Windows File Explorer integration.%n%nClick Next to continue.
FinishedHeadingLabel=FileOrgy is Ready to Organize
FinishedLabel=FileOrgy has been installed successfully on your computer.%n%nYour configured watch folders and automation rules will now monitor incoming downloads and files automatically.%n%nClick Finish to exit Setup and launch FileOrgy.

[Tasks]
; =====================================================================
; Group 1: Shortcuts & Launch
; =====================================================================
Name: "startmenuicon"; Description: "Pin to Start Menu (Recommended for easy search and access)"; GroupDescription: "Shortcuts & Launch:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a Desktop shortcut (Quick launch directly from desktop)"; GroupDescription: "Shortcuts & Launch:"; Flags: unchecked

; =====================================================================
; Group 2: System Integration
; =====================================================================
Name: "contextmenu"; Description: "Add 'Organize with FileOrgy' to File Explorer right-click menu (Instant one-click folder cleanup)"; GroupDescription: "System Integration:"; Flags: checkedonce

; =====================================================================
; Group 3: Automation & Startup
; =====================================================================
Name: "autostart"; Description: "Start FileOrgy automatically when you sign in to Windows (Enables continuous background monitoring)"; GroupDescription: "Automation & Startup:"; Flags: checkedonce
Name: "startminimized"; Description: "Start minimized to the System Tray (Runs quietly near the taskbar clock without opening a window)"; GroupDescription: "Automation & Startup:"; Flags: checkedonce

; =====================================================================
; Group 4: Privileges & Security
; =====================================================================
Name: "runasadmin"; Description: "Always run FileOrgy with Administrator privileges (Only check this if monitoring protected system folders like Program Files)"; GroupDescription: "Privileges & Security:"; Flags: unchecked

[Files]
Source: "dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "assets\app.ico"; DestDir: "{app}\assets"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; 1. Windows Startup with Minimized / Tray toggle
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: autostart and startminimized; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart and not startminimized; Flags: uninsdeletevalue

; 2. Always Run As Administrator (~ RUNASADMIN)
Root: HKA; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: string; ValueName: "{app}\{#MyAppExeName}"; ValueData: "~ RUNASADMIN"; Tasks: runasadmin; Flags: uninsdeletevalue

; 3. Windows Explorer Context Menu on Folders ("Organize with FileOrgy")
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}"; ValueType: string; ValueName: ""; ValueData: "Organize with FileOrgy"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#MyAppName}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --scan ""%1"""; Tasks: contextmenu; Flags: uninsdeletekey

; 4. Windows Explorer Context Menu on Directory Background (Inside a folder)
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\{#MyAppName}"; ValueType: string; ValueName: ""; ValueData: "Organize with FileOrgy"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\{#MyAppName}"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\{#MyAppName}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --scan ""%V"""; Tasks: contextmenu; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec
