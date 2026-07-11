; Inno Setup script - compiles the published Windows output (dist/win) into an installer.
;
; Version is passed in from the command line: `iscc /DMyAppVersion=1.2.3 installer.iss`
; (the previous version of this script tried to read VERSION.txt via a GetString/FileNameExpand
; combo that isn't real Inno Setup Preprocessor syntax, and referenced {#MyAppVersion} before
; the #define was even reached - it would have failed to compile the first time anyone actually
; ran ISCC against it.)
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

[Setup]
AppName=KSC-Sharp
AppVersion={#MyAppVersion}
AppPublisher=SSMG4
DefaultDirName={pf}\KSC-Sharp
DefaultGroupName=KSC-Sharp
OutputDir=Output
OutputBaseFilename=KSC-Sharp-Setup-{#MyAppVersion}
UninstallDisplayIcon={app}\KSC-Sharp.App.exe
Compression=lzma
SolidCompression=yes
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\..\dist\win\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KSC-Sharp"; Filename: "{app}\KSC-Sharp.App.exe"
Name: "{group}\Uninstall KSC-Sharp"; Filename: "{uninstallexe}"
Name: "{commondesktop}\KSC-Sharp"; Filename: "{app}\KSC-Sharp.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
; Registers the pekora-player:// link handler as part of install, silently.
Filename: "{app}\KSC-Sharp.App.exe"; Parameters: "--install"; Flags: runhidden; StatusMsg: "Registering pekora-player:// link handler..."
Filename: "{app}\KSC-Sharp.App.exe"; Description: "Launch KSC-Sharp"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Unregisters the URI scheme and removes the downloaded bootstrapper + FastFlags cache from
; AppData before the installer removes the program files themselves. RunOnceId is required by
; Inno Setup for every [UninstallRun] entry.
Filename: "{app}\KSC-Sharp.App.exe"; Parameters: "--uninstall"; Flags: runhidden; RunOnceId: "UnregisterKscSharp"
