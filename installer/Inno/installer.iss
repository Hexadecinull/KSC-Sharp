; Inno Setup script (Windows publish outputs)
[Setup]
AppName=KSC-Sharp
AppVersion={#MyAppVersion}
DefaultDirName={pf}\KSC-Sharp
DefaultGroupName=KSC-Sharp
OutputBaseFilename=KSC-Sharp-Setup
Compression=lzma
SolidCompression=yes
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\..\dist\win\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KSC-Sharp"; Filename: "{app}\KSC-Sharp.App.exe"
Name: "{commondesktop}\KSC-Sharp"; Filename: "{app}\KSC-Sharp.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\KSC-Sharp.App.exe"; Description: "Launch KSC-Sharp"; Flags: nowait postinstall skipifsilent

[Code]
#define MyAppVersion GetString(FileNameExpand("..\..\VERSION.txt"))
