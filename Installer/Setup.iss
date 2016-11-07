; Inno Setup script file for CLRegAsm
; Copyright (c) 2016 Charles Lechasseur
;
; Permission is hereby granted, free of charge, to any person obtaining a copy
; of this software and associated documentation files (the "Software"), to deal
; in the Software without restriction, including without limitation the rights
; to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
; copies of the Software, and to permit persons to whom the Software is
; furnished to do so, subject to the following conditions:
;
; The above copyright notice and this permission notice shall be included in
; all copies or substantial portions of the Software.
;
; THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
; IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
; FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
; AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
; LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
; OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
; THE SOFTWARE.

#define MyAppName "CLRegAsm"
#define MyAppVersion "1.0"
#define MyAppPublisher "Charles Lechasseur"
#define MyAppURL "https://github.com/clechasseur/clregasm"
#define MyAppDescription "CL's .NET Assembly Registration Tool"
#define MyAppCopyright "(c) 2016, Charles Lechasseur. See LICENSE for details."
#define MyLicenseFile "..\LICENSE"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{94E68FAB-94E5-4C03-83C6-13006F9B8EC7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile={#MyLicenseFile}
OutputDir=Output
OutputBaseFilename={#MyAppName}{#MyAppVersion}
Compression=lzma
SolidCompression=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppDescription}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCopyright={#MyAppCopyright}
ArchitecturesInstallIn64BitMode=x64
DisableReadyPage=yes
MinVersion=6.0
ChangesEnvironment=yes
ShowComponentSizes=no

[Languages]
Name: english; MessagesFile: compiler:Default.isl

[Messages]
SelectTasksLabel2=Select an additional task you would like Setup to perform while installing [name], then click Install.

[Types]
Name: full; Description: Full Installation
Name: custom; Description: Custom Installation; Flags: iscustom

[Components]
Name: net20; Description: For .NET 2.0; Types: full
Name: net40; Description: For .NET 4; Types: full

[Tasks]
Name: nopathchange; Description: Do not modify PATH; Flags: exclusive
Name: net20path; Description: Add .NET &2.0 executable to PATH; Components: net20; Flags: exclusive unchecked
Name: net40path; Description: Add .NET &4 executable to PATH; Components: net40; Flags: exclusive unchecked
Name: net20path_x64; Description: Add x64 .NET &2.0 executable to PATH; Components: net20; Flags: exclusive unchecked; Check: Is64BitInstallMode
Name: net40path_x64; Description: Add x64 .NET &4 executable to PATH; Components: net40; Flags: exclusive unchecked; Check: Is64BitInstallMode

[Files]
Source: ..\bin\v2.0.50727\Win32\Release\CLRegAsm.exe; DestDir: {app}\v2.0.50727\Win32; Components: net20; Flags: restartreplace uninsrestartdelete
Source: ..\bin\v2.0.50727\Win32\Release\CLRegAsmLib.dll; DestDir: {app}\v2.0.50727\Win32; Components: net20; Flags: restartreplace uninsrestartdelete
Source: ..\bin\v2.0.50727\x64\Release\CLRegAsm.exe; DestDir: {app}\v2.0.50727\x64; Components: net20; Flags: restartreplace uninsrestartdelete; Check: Is64BitInstallMode
Source: ..\bin\v2.0.50727\x64\Release\CLRegAsmLib.dll; DestDir: {app}\v2.0.50727\x64; Components: net20; Flags: restartreplace uninsrestartdelete; Check: Is64BitInstallMode
Source: ..\bin\v4.0.30319\Win32\Release\CLRegAsm.exe; DestDir: {app}\v4.0.30319\Win32; Components: net40; Flags: restartreplace uninsrestartdelete
Source: ..\bin\v4.0.30319\Win32\Release\CLRegAsmLib.dll; DestDir: {app}\v4.0.30319\Win32; Components: net40; Flags: restartreplace uninsrestartdelete
Source: ..\bin\v4.0.30319\x64\Release\CLRegAsm.exe; DestDir: {app}\v4.0.30319\x64; Components: net40; Flags: restartreplace uninsrestartdelete; Check: Is64BitInstallMode
Source: ..\bin\v4.0.30319\x64\Release\CLRegAsmLib.dll; DestDir: {app}\v4.0.30319\x64; Components: net40; Flags: restartreplace uninsrestartdelete; Check: Is64BitInstallMode
Source: ..\LICENSE; DestDir: {app}

[Icons]
Name: {group}\{#MyAppName} License; Filename: {app}\LICENSE; Flags: excludefromshowinnewinstall
Name: {group}\{#MyAppName} on GitHub; Filename: {#MyAppURL}; Flags: excludefromshowinnewinstall
Name: {group}\{cm:UninstallProgram,{#MyAppName}}; Filename: {uninstallexe}; Flags: excludefromshowinnewinstall

[Code]
const
  // Constants to access environment in the registry.
  CEnvironmentRootKey = HKEY_LOCAL_MACHINE;
  CEnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  
  // Separator used for the PATH environment variable.
  CPATHSeparator = ';';

// Splits a string into subparts using a separator.
procedure Split(const AString: string; ASeparator: Char; AResult: TStrings);
var
 Working: string;
 P: Integer;
begin
  Working := AString;
  while Working <> '' do
  begin
    P := Pos(ASeparator, Working);
    if P > 0 then
    begin
      AResult.Add(Copy(Working, 1, P - 1));
      Working := Copy(Working, P + 1, MaxInt);
    end
    else
    begin
      AResult.Add(Working);
      Working := '';
    end;
  end;
end;

// Merges several strings into one, using a separator.
function Merge(AStrings: TStrings; ASeparator: Char): string;
var
  I: Integer;
begin
  Result := '';
  for I := 0 to AStrings.Count - 1 do
  begin
    if Result <> '' then
      Result := Result + ASeparator;
    Result := Result + AStrings[I];
  end;
end;

// Modifies the PATH environment variable by adding or removing a specific path from it.
procedure ModifyPATHEnvironmentVariable(const APath: string; AAdd: Boolean);
var
  PathVar, LowerPath: string;
  Paths: TStringList;
  I, FoundPos: Integer;
  Modified: Boolean;
begin
  // Get current PATH value.
  if RegQueryStringValue(CEnvironmentRootKey, CEnvironmentKey, 'PATH', PathVar) then
  begin
    // Split into paths.
    Paths := TStringList.Create;
    try
      Split(PathVar, CPATHSeparator, Paths);
      
      // Look for the path user asked for. Note that paths are case-insensitive.
      LowerPath := Lowercase(APath);
      FoundPos := -1;
      for I := 0 to Paths.Count - 1 do
      begin
        if LowerPath = Lowercase(Paths[I]) then
        begin
          FoundPos := I;
          Break;
        end;
      end;
      
      // Add or remove path as appropriate.
      Modified := False;
      if AAdd then
      begin
        if FoundPos = -1 then
        begin
          Paths.Add(APath);
          Modified := True;
        end;
      end
      else
      begin
        if FoundPos >= 0 then
        begin
          Paths.Delete(FoundPos);
          Modified := True;
        end;
      end;
      
      if Modified then
      begin
        // Merge paths again and save back to registry.
        RegWriteStringValue(CEnvironmentRootKey, CEnvironmentKey, 'PATH',
          Merge(Paths, CPATHSeparator)); 
      end;
    finally
      Paths.Free;
    end;
  end;
end;

// Called before and after installation to add custom steps.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Add or remove paths to CLRegAsm from PATH.
    ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v2.0.50727\Win32'),
      IsTaskSelected('net20path'));
    ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v4.0.30319\Win32'),
      IsTaskSelected('net40path'));
    if Is64BitInstallMode then
    begin
      ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v2.0.50727\x64'),
        IsTaskSelected('net20path_x64'));
      ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v4.0.30319\x64'),
        IsTaskSelected('net40path_x64'));
    end;
  end;
end;

// Called after a page has changed in the installation wizard.
procedure CurPageChanged(CurPageID: Integer);
begin
  // Since we're disabling the ready page, we need to change the next button
  // to 'Install' manually here. See help for the 'DisableReadyPage' setup directive.
  if CurPageID = wpSelectTasks then
    WizardForm.NextButton.Caption := SetupMessage(msgButtonInstall)
  else if CurPageID = wpFinished then
    WizardForm.NextButton.Caption := SetupMessage(msgButtonFinish)
  else
    WizardForm.NextButton.Caption := SetupMessage(msgButtonNext);
end;

// Called before and after uninstallation to add custom steps.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Remove all paths to our executables from PATH, regardless of what
    // task had been selected during installation. That's the easiest way
    // anyway and won't leave leftover paths.
    ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v2.0.50727\Win32'), False);
    ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v4.0.30319\Win32'), False);
    if Is64BitInstallMode then
    begin
      ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v2.0.50727\x64'), False);
      ModifyPATHEnvironmentVariable(ExpandConstant('{app}\v4.0.30319\x64'), False);
    end;
  end;
end;
