; Meta Receipts Lite — per-user Inno Setup installer (no admin)
; Compile with ISCC.exe after: dotnet build -c Release

#ifndef MyAppVersion
  #define MyAppVersion "1.0.5"
#endif

#define MyAppName "Meta Receipts Lite"
#define MyAppPublisher "Meta2Excel"
#define MyAppExeName "MetaReceiptsLite.exe"
#define MyAppSource "..\MetaReceiptsLite\bin\Release\net48"
#define MyAppIcon "..\MetaReceiptsLite\AppIcon.ico"

[Setup]
AppId={{A8E4C2F1-7B3D-4E9A-9C61-2F0D8B5A1E73}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\MetaReceiptsLite
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=MetaReceiptsLite-Setup-v{#MyAppVersion}
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
InfoBeforeFile=..\PRIVACY.txt
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &Desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MyAppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\PRIVACY.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\AppIcon.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\AppIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function IsDotNet48OrNewer(): Boolean;
var
  release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release) then
  begin
    { 528040 = .NET Framework 4.8 }
    Result := release >= 528040;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet48OrNewer() then
  begin
    MsgBox(
      '{#MyAppName} requires Microsoft .NET Framework 4.8.'#13#10#13#10 +
      'Install it from:'#13#10 +
      'https://dotnet.microsoft.com/download/dotnet-framework/net48'#13#10#13#10 +
      'Then run this Setup again.',
      mbError, MB_OK);
    Result := False;
  end;
end;
