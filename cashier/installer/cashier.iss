[Setup]
AppName=GamePort Cashier
AppVersion=1.0.0
AppPublisher=GamePort
DefaultDirName={autopf}\GamePort Cashier
DefaultGroupName=GamePort Cashier
OutputDir=installer\output
OutputBaseFilename=WingPort-Cashier-1.0.0-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
SetupIconFile=installer\cashier.ico
UninstallDisplayIcon={app}\GamePort.Cashier.exe
LicenseFile=..\..\LICENSE
WizardStyle=modern

[Languages]
Name: "farsian"; MessagesFile: "compiler:Languages\Farsi.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "ایجاد میانبر روی صفحه‌ی دسکتاپ"; GroupDescription: "میانبرها:"; Flags: checkedonce

[Files]
Source: "..\publish\output\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\GamePort Cashier"; Filename: "{app}\GamePort.Cashier.exe"
Name: "{group}\حذف GamePort Cashier"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GamePort Cashier"; Filename: "{app}\GamePort.Cashier.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GamePort.Cashier.exe"; Description: "اجرای نرم‌افزار"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Logs"
Type: filesandordirs; Name: "{app}\Backups"
Type: filesandordirs; Name: "{app}\Data"
