; LFP Hub — Inno Setup 6 installer (per-user, no admin)
; Built by build-release.ps1 after dotnet publish into ..\publish

#define MyAppName "LFP Hub"
#define MyAppPublisher "Tesla IT · GFNV"
#define MyAppURL "https://github.com/Amitadams/LFP-Hub"
#define MyAppExeName "LfpHub.exe"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.3"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{A8C4E2F1-9B3D-4E6A-8F01-2C7D91B4E5A0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\LfpHub
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install — no UAC / admin
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=LfpHub-{#MyAppVersion}-Setup
SetupIconFile=..\Assets\LfpHub.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Close running app before upgrade
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
; Single Setup.exe — no zip path games
DisableWelcomePage=no
InfoBeforeFile=
LicenseFile=
; Start Menu + optional desktop
AllowNoIcons=yes
ChangesAssociations=no
MinVersion=10.0
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Published app payload (exe, dlls, Templates, icon)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "install.ps1,install.bat,uninstall.ps1,Open LFP Hub.bat"
; Ensure icon beside exe for shell
Source: "..\Assets\LfpHub.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\LfpHub.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\LfpHub.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ConfigDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    ConfigDir := ExpandConstant('{localappdata}\LfpHub');
    if DirExists(ConfigDir) then
    begin
      if MsgBox('Also remove LFP Hub settings and templates under:' + #13#10 +
                ConfigDir + #13#10#13#10 +
                'Choose No to keep your tech identity for a reinstall.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(ConfigDir, True, True, True);
      end;
    end;
  end;
end;
