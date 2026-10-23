; windowfit setup: ISCC /DArch=<x64|x86|arm64> /DAppVersion=<X.Y.Z> /DSourceExe=<published windowfit.exe> windowfit.iss
; Files go to Program Files only, so the exe behind the elevated task stays writable only by Administrators.

#ifndef Arch
  #error Define Arch: x64, x86 or arm64
#endif
#ifndef AppVersion
  #error Define AppVersion: X.Y.Z
#endif
#ifndef SourceExe
  #error Define SourceExe: the published windowfit.exe
#endif
#if Arch == "x64"
  #define InstallIn64 "x64os"
#elif Arch == "arm64"
  #define InstallIn64 "arm64"
#elif Arch == "x86"
  #define InstallIn64 ""
#else
  #error Arch must be x64, x86 or arm64
#endif

#define TaskName "\windowfit\windowfit"
#define FolderSddl "D:P(A;;FA;;;BA)(A;;FA;;;SY)(A;;FR;;;AU)"

[Setup]
AppId={{2FC0575C-6F80-4F56-B04B-085EE2BC8E51}
AppName=windowfit
AppVersion={#AppVersion}
AppVerName=windowfit {#AppVersion}
AppPublisher=6Up3r
AppPublisherURL=https://github.com/6up3r/windowfit
VersionInfoVersion={#AppVersion}
DefaultDirName={commonpf}\windowfit
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableWelcomePage=no
LicenseFile=..\LICENSE
PrivilegesRequired=admin
MinVersion=10.0
#if InstallIn64 != ""
ArchitecturesInstallIn64BitMode={#InstallIn64}
#endif
SetupIconFile=..\src\WindowFit\Assets\windowfit.ico
UninstallDisplayIcon={app}\windowfit.exe
UninstallDisplayName=windowfit
WizardStyle=modern
CloseApplications=no
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=windowfit-{#AppVersion}-setup-{#Arch}

[Languages]
; English first: an unlisted Windows UI language falls back to it.
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
; Not bundled with Inno Setup: copied unchanged from jrsoftware/issrc Files/Languages/Unofficial at is-6_7_3.
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

#include "CustomMessages.iss"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\scripts\status.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\scripts\windowfit-common.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "session-user.ps1"; Flags: dontcopy

[Icons]
; Starts the task, never the exe itself: a non-elevated instance would take the single-instance mutex.
Name: "{commonprograms}\windowfit"; Filename: "{app}\windowfit.exe"; Parameters: "--start"; IconFilename: "{app}\windowfit.exe"

[Run]
Filename: "{app}\windowfit.exe"; Parameters: "--start"; Description: "{cm:LaunchNow}"; Flags: postinstall runascurrentuser runhidden nowait; Check: TaskRegistered

[Code]
const
  UsersGroupSid = 'S-1-5-32-545';
  AuthenticatedUsersSid = 'AU';

var
  ScopePage: TWizardPage;
  OnlyMeButton, AllUsersButton: TNewRadioButton;
  SessionUserName: String;
  SessionUserSid: String;
  TaskFailed: Boolean;

function NativeArch: String;
begin
  if IsArm64 then
    Result := 'arm64'
  else if IsX64OS then
    Result := 'x64'
  else
    Result := 'x86';
end;

// The signed-in user of this session: under over-the-shoulder elevation Setup runs as another administrator.
procedure FindSessionUser;
var
  Code: Integer;
  Output: TExecOutput;
begin
  ExtractTemporaryFile('session-user.ps1');
  if ExecAndCaptureOutput(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\session-user.ps1') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, Code, Output) and (Code = 0) and (GetArrayLength(Output.StdOut) >= 2) then
  begin
    SessionUserName := Trim(Output.StdOut[0]);
    SessionUserSid := Trim(Output.StdOut[1]);
  end;
end;

function FancyZonesOverridesSnap: Boolean;
var
  Profile: String;
  Settings: AnsiString;
  Text: String;
begin
  Result := False;
  if (SessionUserSid = '') or not RegQueryStringValue(HKLM,
    'SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\' + SessionUserSid, 'ProfileImagePath', Profile) then
    Exit;
  StringChangeEx(Profile, '%SystemDrive%', ExpandConstant('{sd}'), True);
  if not LoadStringFromFile(Profile + '\AppData\Local\Microsoft\PowerToys\FancyZones\settings.json', Settings) then
    Exit;
  Text := Lowercase(String(Settings));
  StringChangeEx(Text, ' ', '', True);
  StringChangeEx(Text, #9, '', True);
  StringChangeEx(Text, #13, '', True);
  StringChangeEx(Text, #10, '', True);
  Result := Pos('"fancyzones_overridesnaphotkeys":{"value":true', Text) > 0;
end;

function InitializeSetup: Boolean;
begin
  Result := NativeArch = '{#Arch}';
  if not Result then
    SuppressibleMsgBox(FmtMessage(CustomMessage('WrongArch'), ['{#Arch}',
      'windowfit-{#AppVersion}-setup-' + NativeArch + '.exe']), mbError, MB_OK, IDOK);
end;

// A local account reads PCNAME\user; only a domain account keeps its prefix.
function DisplayUserName: String;
var
  Prefix: String;
begin
  Result := SessionUserName;
  Prefix := Uppercase(GetComputerNameString) + '\';
  if Pos(Prefix, Uppercase(Result)) = 1 then
    Delete(Result, 1, Length(Prefix));
end;

function NewScopeButton(const Caption: String; const Top: Integer): TNewRadioButton;
begin
  Result := TNewRadioButton.Create(ScopePage);
  Result.Parent := ScopePage.Surface;
  Result.Caption := Caption;
  Result.Left := 0;
  Result.Top := Top;
  Result.Width := ScopePage.SurfaceWidth;
  Result.Height := ScaleY(17);
end;

procedure InitializeWizard;
begin
  FindSessionUser;
  // Radio buttons on a custom page: CreateInputOptionPage's list clips the left edge of its circles.
  ScopePage := CreateCustomPage(wpLicense, CustomMessage('ScopeTitle'), CustomMessage('ScopeDescription'));
  OnlyMeButton := NewScopeButton(FmtMessage(CustomMessage('ScopeOnlyMe'), [DisplayUserName]), 0);
  AllUsersButton := NewScopeButton(CustomMessage('ScopeAllUsers'), ScaleY(26));
  // /SCOPE=all on the command line, else the previous install's choice, else "Only for me".
  if (SessionUserSid = '') or (ExpandConstant('{param:SCOPE|' + GetPreviousData('Scope', 'user') + '}') = 'all') then
    AllUsersButton.Checked := True
  else
    OnlyMeButton.Checked := True;
  OnlyMeButton.Enabled := SessionUserSid <> '';
  if FancyZonesOverridesSnap then
    CreateOutputMsgPage(ScopePage.ID, CustomMessage('FancyZonesTitle'), '', CustomMessage('FancyZonesWarning'));
end;

function ForAllUsers: Boolean;
begin
  Result := AllUsersButton.Checked;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  if ForAllUsers then
    SetPreviousData(PreviousDataKey, 'Scope', 'all')
  else
    SetPreviousData(PreviousDataKey, 'Scope', 'user');
end;

procedure RunHidden(const Filename, Params: String);
var
  Code: Integer;
begin
  Exec(Filename, Params, '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

// Ends the task's instance and any other one, elevated or not; nothing may be running.
procedure StopWindowFit;
begin
  RunHidden(ExpandConstant('{sys}\schtasks.exe'), '/End /TN "{#TaskName}"');
  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/IM windowfit.exe /F');
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopWindowFit;
  Result := '';
end;

function TaskXml: String;
var
  Trigger, Principal, Runner, Exe: String;
begin
  Exe := ExpandConstant('{app}\windowfit.exe');
  if ForAllUsers then
  begin
    Trigger := '';
    Principal := '<GroupId>' + UsersGroupSid + '</GroupId>';
    Runner := AuthenticatedUsersSid;
  end else
  begin
    Trigger := '<UserId>' + SessionUserSid + '</UserId>';
    Principal := '<UserId>' + SessionUserSid + '</UserId><LogonType>InteractiveToken</LogonType>';
    Runner := SessionUserSid;
  end;
  Result :=
    '<?xml version="1.0" encoding="UTF-16"?>' + #13#10 +
    '<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' +
    '<RegistrationInfo><Description>windowfit: Win+arrow window tiling</Description>' +
    '<SecurityDescriptor>D:P(A;;FA;;;BA)(A;;FA;;;SY)(A;;GRGX;;;' + Runner + ')</SecurityDescriptor></RegistrationInfo>' +
    '<Triggers><LogonTrigger><Enabled>true</Enabled>' + Trigger + '</LogonTrigger></Triggers>' +
    '<Principals><Principal id="Author">' + Principal + '<RunLevel>HighestAvailable</RunLevel></Principal></Principals>' +
    '<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>' +
    '<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>' +
    '<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>4</Priority>' +
    '<RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure><Enabled>true</Enabled></Settings>' +
    '<Actions Context="Author"><Exec><Command>' + Exe + '</Command><WorkingDirectory>' + ExpandConstant('{app}') +
    '</WorkingDirectory></Exec></Actions></Task>';
end;

// schtasks /XML rejects UTF-8 with a BOM ("incorrect document syntax" at 1,2); it reads UTF-16 LE, its own export format.
// The XML holds only a SID and the Program Files path, so it is ASCII; anything else is refused.
function SaveAsciiAsUtf16(const FileName, S: String): Boolean;
var
  Bytes: AnsiString;
  I: Integer;
begin
  Result := False;
  Bytes := #$FF#$FE;
  for I := 1 to Length(S) do
  begin
    if Ord(S[I]) > 127 then
      Exit;
    Bytes := Bytes + Chr(Ord(S[I])) + #0;
  end;
  Result := SaveStringToFile(FileName, Bytes, False);
end;

// Task Scheduler drops the task descriptor's P flag, so the \windowfit folder itself is created, or rewritten when a
// standard user created it first, to grant write to Administrators and SYSTEM only.
function SecureTaskFolder: Boolean;
var
  Service, Folder: Variant;
begin
  Result := False;
  try
    Service := CreateOleObject('Schedule.Service');
    Service.Connect();
    try
      Folder := Service.GetFolder('\windowfit');
      Folder.SetSecurityDescriptor('{#FolderSddl}', 0);
    except
      Service.GetFolder('\').CreateFolder('windowfit', '{#FolderSddl}');
    end;
    Result := True;
  except
    Log('task folder: ' + GetExceptionMessage);
  end;
end;

// ssPostInstall swallows exceptions, so a failure is reported here and through the exit code.
procedure RegisterTask;
var
  Xml: String;
  Code, I: Integer;
  Output: TExecOutput;
begin
  Xml := ExpandConstant('{tmp}\windowfit-task.xml');
  Code := -1;
  if SecureTaskFolder and SaveAsciiAsUtf16(Xml, TaskXml) and
    ExecAndCaptureOutput(ExpandConstant('{sys}\schtasks.exe'), '/Create /XML "' + Xml + '" /TN "{#TaskName}" /F', '',
      SW_HIDE, ewWaitUntilTerminated, Code, Output) and (Code = 0) then
    Exit;
  Log('task XML: ' + TaskXml);
  for I := 0 to GetArrayLength(Output.StdOut) - 1 do
    Log('schtasks: ' + Output.StdOut[I]);
  for I := 0 to GetArrayLength(Output.StdErr) - 1 do
    Log('schtasks: ' + Output.StdErr[I]);
  TaskFailed := True;
  SuppressibleMsgBox(FmtMessage(CustomMessage('TaskFailed'), [IntToStr(Code)]), mbCriticalError, MB_OK, IDOK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RegisterTask;
end;

function TaskRegistered: Boolean;
begin
  Result := not TaskFailed;
end;

function GetCustomSetupExitCode: Integer;
begin
  if TaskFailed then
    Result := 1
  else
    Result := 0;
end;

// Removes the task and its Task Scheduler folder when no other task is left in it.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Service, Folder: Variant;
begin
  if CurUninstallStep <> usUninstall then
    Exit;
  StopWindowFit;
  RunHidden(ExpandConstant('{sys}\schtasks.exe'), '/Delete /TN "{#TaskName}" /F');
  try
    Service := CreateOleObject('Schedule.Service');
    Service.Connect();
    Folder := Service.GetFolder('\windowfit');
    if Folder.GetTasks(1).Count = 0 then
      Service.GetFolder('\').DeleteFolder('windowfit', 0);
  except
  end;
end;
