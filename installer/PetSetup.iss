; Installateur de Claude Pet (Inno Setup 6). Fabriqué par build.ps1 dans dist\PetSetup.exe.
; - installation pour l'utilisateur, sans droits administrateur ;
; - raccourci « Pet » avec l'icône maison sur le bureau (c'est la maison de Pet) ;
; - démarrage automatique avec Windows (clé « Run » de l'utilisateur) ;
; - désinstalleur qui enlève tout : programme, raccourcis, démarrage automatique et réglages (%AppData%\Pet).

#define AppName "Claude Pet"
#define AppVersion "1.0.0"
#define AppExe "ClaudePet.exe"

[Setup]
AppId={{BB12DCFE-98C2-4784-8503-1B649ACA8C10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Ixorak
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Claude Pet
DefaultGroupName=Claude Pet
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=PetSetup
SetupIconFile=..\assets\house.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
ShowLanguageDialog=no

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Files]
Source: "..\out\app\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Le raccourci « Pet » du bureau est créé dans [Code] (CurStepChanged) : s'il est bloqué, l'installation continue.
Name: "{userprograms}\Claude Pet"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExe}"; Comment: "Claude Pet"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClaudePet"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Lancer Pet maintenant"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{userdesktop}\Pet.lnk"
Type: filesandordirs; Name: "{userappdata}\Pet"
Type: dirifempty; Name: "{app}"

[Code]
const
  MsgQuitNow = $800B; { WM_APP + 11 : Pet sauvegarde et quitte tout de suite }

{ Ferme Pet s'il tourne (sinon son exe serait verrouillé), et attend jusqu'à 4 s. }
procedure ClosePet();
var
  Wnd: HWND;
  I: Integer;
begin
  Wnd := FindWindowByClassName('ClaudePet.Controller');
  if Wnd = 0 then Exit;
  PostMessage(Wnd, MsgQuitNow, 0, 0);
  for I := 1 to 40 do
  begin
    Sleep(100);
    if FindWindowByClassName('ClaudePet.Controller') = 0 then Exit;
  end;
end;

function InitializeSetup(): Boolean;
begin
  ClosePet();
  Result := True;
end;

{ Le raccourci « Pet » du bureau (la maison de Pet), avec l'icône maison de l'exe. }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then Exit;
  try
    CreateShellLink(ExpandConstant('{userdesktop}\Pet.lnk'), 'Claude Pet',
      ExpandConstant('{app}\{#AppExe}'), '', ExpandConstant('{app}'),
      ExpandConstant('{app}\{#AppExe}'), 0, SW_SHOWNORMAL);
  except
    if not WizardSilent then
      MsgBox('Le raccourci « Pet » n''a pas pu être créé sur le bureau (un antivirus le bloque peut-être).' + #13#10 +
        'Pet fonctionne quand même : sans sa maison, il sort du sol en bas de l''écran principal.', mbInformation, MB_OK);
  end;
end;

function InitializeUninstall(): Boolean;
begin
  ClosePet();
  Result := True;
end;
