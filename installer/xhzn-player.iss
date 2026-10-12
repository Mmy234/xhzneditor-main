#define MyAppName "XHZN播放器"
#define MyAppVersion "1.0.0"
#define MyAppExeName "XhznPlayer.exe"

[Setup]
AppId={{8F3C1B6E-7A21-4D9E-9C44-A1B2C3D4E5F6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={pf}\XHZN\Player
DefaultGroupName=XHZN
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=XhznPlayerSetup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
PrivilegesRequired=admin
SetupIconFile=

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
Source: "..\player\src\XhznPlayer\bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
; 若已准备 WebView2 Fixed Version 运行时，放到 installer\WebView2Fixed 后取消下一行注释
; Source: "WebView2Fixed\*"; DestDir: "{app}\WebView2Fixed"; Flags: ignoreversion recursesubdirs

[Dirs]
Name: "{commonappdata}\XHZN"
Name: "{commonappdata}\XHZN\projects"
Name: "{commonappdata}\XHZN\projects\_cache"
Name: "{commonappdata}\XHZN\logs"
Name: "{commonappdata}\XHZN\bindings"
Name: "{commonappdata}\XHZN\trials"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即打开播放器"; Flags: nowait postinstall skipifsilent
