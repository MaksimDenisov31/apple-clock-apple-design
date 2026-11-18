#define AppVersion "1.0.6"
#ifdef InstallerTest
  #define AppName "Apple Clock Test " + InstallerTest
  #define AppId "AppleClock.InstallerTest." + InstallerTest
  #define OutputName "AppleClock-Setup-Test"
#else
  #define AppName "Apple Clock"
  #define AppId "AppleClock.MaksimDenisovRussia"
  #define OutputName "AppleClock-Setup-" + AppVersion
#endif

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Максим Денисов
AppPublisherURL=https://github.com/MaksimDenisovRussia/apple-clock-apple-design
AppSupportURL=https://github.com/MaksimDenisovRussia/apple-clock-apple-design/issues
AppUpdatesURL=https://github.com/MaksimDenisovRussia/apple-clock-apple-design/releases
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
UninstallDisplayIcon={app}\AppleClock.exe
UninstallDisplayName={#AppName}
SetupIconFile=..\assets\app.ico
OutputDir=..\installer-output
OutputBaseFilename={#OutputName}
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Установка {#AppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
SetupLogging=yes
CloseApplications=yes
CloseApplicationsFilter=AppleClock.exe
RestartApplications=no
ChangesAssociations=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.DesktopTask=Создать ярлык на рабочем столе
english.DesktopTask=Create a desktop shortcut
russian.LaunchApp=Запустить Apple Clock
english.LaunchApp=Launch Apple Clock
russian.DotNetMissing=Для Apple Clock требуется .NET Framework 4.8 или новее.%n%nОткрыть официальную страницу Microsoft для его установки? После установки .NET запустите этот установщик снова.
english.DotNetMissing=Apple Clock requires .NET Framework 4.8 or later.%n%nOpen the official Microsoft download page? After installing .NET, run this setup again.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopTask}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\AppleClock.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\AppleClock.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\SOUND-CREDITS.md"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "..\docs\preview-light.png"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\docs\preview-dark.png"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}\Apple Clock"; Filename: "{app}\AppleClock.exe"; WorkingDir: "{app}"
Name: "{userprograms}\{#AppName}\Инструкция"; Filename: "https://github.com/MaksimDenisovRussia/apple-clock-apple-design#readme"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\AppleClock.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\AppleClock.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
  ErrorCode: Integer;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
  begin
    if not WizardSilent then
      if MsgBox(ExpandConstant('{cm:DotNetMissing}'), mbInformation, MB_YESNO) = IDYES then
        ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;
