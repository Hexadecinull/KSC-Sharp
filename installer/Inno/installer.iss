; Inno Setup script - compiles a published Windows output into an installer.
;
; Version and architecture are passed in from the command line:
;   iscc /DMyAppVersion=1.2.3 /DMyAppArch=x64 /DMySourceDir=..\..\dist\win-x64 installer.iss
;   iscc /DMyAppVersion=1.2.3 /DMyAppArch=x86 /DMySourceDir=..\..\dist\win-x86 installer.iss
; (the previous version of this script tried to read VERSION.txt via a GetString/FileNameExpand
; combo that isn't real Inno Setup Preprocessor syntax, and referenced {#MyAppVersion} before
; the #define was even reached - it would have failed to compile the first time anyone actually
; ran ISCC against it.)
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef MyAppArch
  #define MyAppArch "x64"
#endif
#ifndef MySourceDir
  #define MySourceDir "..\..\dist\win-x64"
#endif

[Setup]
AppName=KSC-Sharp
AppVersion={#MyAppVersion}
AppPublisher=SSMG4
DefaultDirName={autopf}\KSC-Sharp
DefaultGroupName=KSC-Sharp
OutputDir=Output
OutputBaseFilename=KSC-Sharp-Setup-{#MyAppVersion}-{#MyAppArch}
UninstallDisplayIcon={app}\KSC-Sharp.App.exe
Compression=lzma
SolidCompression=yes
SetupLogging=yes
; {#MyAppArch}-specific targeting: a 32-bit build must stay installable on a 32-bit Windows
; install (ArchitecturesInstallIn64BitMode left unset for x86), while the 64-bit build should
; use the native Program Files rather than the WOW64 redirected one.
#if MyAppArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#else
ArchitecturesAllowed=x86compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

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
