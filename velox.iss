[Setup]
AppName=Velox Browser
AppVersion=1.0
DefaultDirName={autopf}\Velox Browser
DefaultGroupName=Velox Browser
OutputBaseFilename=VeloxInstaller
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern

[Files]
Source: "InstallerFiles\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "InstallerFiles\WebView2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Velox Browser"; Filename: "{app}\VeloxBrowser.exe"
Name: "{commondesktop}\Velox Browser"; Filename: "{app}\VeloxBrowser.exe"

[Run]
Filename: "{tmp}\WebView2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing WebView2 Runtime..."; Flags: runhidden
Filename: "{app}\VeloxBrowser.exe"; Description: "Launch Velox Browser"; Flags: nowait postinstall skipifsilent