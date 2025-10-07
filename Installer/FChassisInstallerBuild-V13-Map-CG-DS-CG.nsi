!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinMessages.nsh"
!include "MUI2.nsh"
!include "nsDialogs.nsh"

; -----------------------------------------------------------------------------
; General Settings
; -----------------------------------------------------------------------------
!define APPNAME "FChassis"
!define VERSION "1.0.4"
!define COMPANY "Teckinsoft Neuronics Pvt. Ltd."
!define INSTALLDIR "C:\FChassis"
!define FluxSDKBin "C:\FluxSDK\Bin"
!define FluxSDKDir "C:\FluxSDK"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
!define INSTALL_FLAG_KEY "Software\${COMPANY}\${APPNAME}"
!define TARGET_DRIVE "W:"
!define DRIVE_VALUE "W:"           ; name of the value under DOS Devices

Name "${APPNAME} ${VERSION}"
OutFile "FChassisInstaller.exe"
InstallDir "${INSTALLDIR}"
RequestExecutionLevel admin

; -----------------------------------------------------------------------------
; Variables
; -----------------------------------------------------------------------------
Var InstalledState ; 0 = not installed, 1 = same version, 2 = older version, 3 = newer version
Var ExistingInstallDir
Var ExistingVersion
Var ExtractionCompleted
Var LogFileHandle
Var UserLocalAppDataDir
Var TempDriveMapping
Var UserToken
Var UserProcess

; -----------------------------------------------------------------------------
; Pages
; -----------------------------------------------------------------------------
!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING

!define MUI_PAGE_CUSTOMFUNCTION_PRE DirectoryPre
!insertmacro MUI_PAGE_DIRECTORY

!define MUI_PAGE_CUSTOMFUNCTION_SHOW InstFilesShow
!define MUI_PAGE_CUSTOMFUNCTION_ABORT OnInstFilesAbort
!insertmacro MUI_PAGE_INSTFILES

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; -----------------------------------------------------------------------------
; Logging Macros
; -----------------------------------------------------------------------------
!macro LogMessage message
  Push $0
  FileWrite $LogFileHandle "[$(^Name)] ${message}$\r$\n"
  DetailPrint "LOG: ${message}"
  Pop $0
!macroend

!macro LogVar varname varvalue
  Push $0
  FileWrite $LogFileHandle "[$(^Name)] ${varname}: ${varvalue}$\r$\n"
  DetailPrint "LOG: ${varname}: ${varvalue}"
  Pop $0
!macroend

!macro LogError message
  Push $0
  FileWrite $LogFileHandle "[$(^Name)] ERROR: ${message}$\r$\n"
  DetailPrint "ERROR: ${message}"
  Pop $0
!macroend

; -----------------------------------------------------------------------------
; Helper: broadcast env change
; -----------------------------------------------------------------------------
!macro BroadcastEnvChange
  !insertmacro LogMessage "Broadcasting environment change..."
  System::Call "USER32::SendMessageTimeout(i ${HWND_BROADCAST}, i ${WM_SETTINGCHANGE}, i 0, w 'Environment', i 0, i 5000, i 0)"
  !insertmacro LogMessage "Environment change broadcast completed"
!macroend

; -----------------------------------------------------------------------------
; String manipulation functions
; -----------------------------------------------------------------------------
!macro StrTrimTrailingSlash output input
  StrCpy ${output} "${input}"
  ${Do}
    StrCpy $0 "${output}" 1 -1
    ${If} $0 == "\"
      StrCpy ${output} "${output}" -1
    ${Else}
      ${ExitDo}
    ${EndIf}
  ${Loop}
!macroend

Function StrContains
  Exch $R0 ; substring
  Exch
  Exch $R1 ; string
  Push $R2
  Push $R3
  Push $R4
  Push $R5

  StrCpy $R2 0
  StrLen $R3 $R0
  StrLen $R4 $R1
  StrCpy $R5 0

  ${Do}
    StrCpy $R2 $R1 $R3 $R5
    ${If} $R2 == $R0
      StrCpy $R0 1
      ${ExitDo}
    ${EndIf}
    ${If} $R5 >= $R4
      StrCpy $R0 0
      ${ExitDo}
    ${EndIf}
    IntOp $R5 $R5 + 1
  ${Loop}

  Pop $R5
  Pop $R4
  Pop $R3
  Pop $R2
  Pop $R1
  Exch $R0
FunctionEnd

!macro StrContains result string substring
  Push "${string}"
  Push "${substring}"
  Call StrContains
  Pop "${result}"
!macroend

; =============================================================================
; Run a command line as the active console (shell) user (unelevated).
; In:  stack top => command line (without wrapping "cmd /c")
; Out: stack top <= "OK" or "ERROR: <reason>"
; =============================================================================
Function RunAsActiveConsoleUser
  Exch $0            ; $0 = command to run (no leading "cmd /c")
  Push $1
  Push $2
  Push $3
  Push $4
  Push $5
  Push $6
  Push $7
  Push $8
  Push $9
  Push $R0           ; result string

  StrCpy $R0 "OK"

  ; 1) Get active console session id -> r1 -> $1
  System::Call "kernel32::WTSGetActiveConsoleSessionId() i .r1"
  System::Store L $1 r1
  ${If} $1 = -1
    StrCpy $R0 "ERROR: No active console session"
    Goto done
  ${EndIf}

  ; 2) Query user token for that session -> r2
  System::Call "wtsapi32::WTSQueryUserToken(i r1, *p .r2) i .r8"
  System::Store L $8 r8
  ${If} $8 = 0
    StrCpy $R0 "ERROR: WTSQueryUserToken failed"
    Goto done
  ${EndIf}

  ; 3) Duplicate PRIMARY token -> r3
  System::Call "advapi32::DuplicateTokenEx(p r2, i 0x02000000, p 0, i 2, i 1, *p .r3) i .r8"
  System::Store L $8 r8
  ${If} $8 = 0
    StrCpy $R0 "ERROR: DuplicateTokenEx failed"
    System::Call "kernel32::CloseHandle(p r2)"
    Goto done
  ${EndIf}

  ; 4) Build user environment (optional) -> r4
  System::Call "userenv::CreateEnvironmentBlock(*p .r4, p r3, i 0) i .r8"
  System::Store L $8 r8
  ${If} $8 = 0
    ; Fall back to NULL env if creation fails
    System::Call "kernel32::SetLastError(i 0)"
    ; r4 remains 0
  ${EndIf}

  ; 5) STARTUPINFO & PROCESS_INFORMATION buffers
  System::Call "*(&i4 68, i0, i0, i0, i0, i0, i0, i0, i0, i0, i0, p0, p0, p0, p0, p0) p .r5"
  StrCpy $6 '"/c $0"'
  System::Call "*(i,i,i,i) p .r7"

  ; 6) Launch unelevated
  System::Call "advapi32::CreateProcessWithTokenW(p r3, i 1, w 'C:\Windows\System32\cmd.exe', w r6, i 0x400, p r4, w 'C:\', p r5, p r7) i .r8"
  System::Store L $8 r8
  ${If} $8 = 0
    System::Call "kernel32::GetLastError() i .r8"
    System::Store L $8 r8
    StrCpy $R0 "ERROR: CreateProcessWithTokenW failed (code $8)"
  ${Else}
    ; Unpack PROCESS_INFORMATION and close handles
    System::Call "*$7(i .r8, i .r9, i .r0, i .r1)"
    System::Call "kernel32::CloseHandle(p r9)" ; hThread
    System::Call "kernel32::CloseHandle(p r8)" ; hProcess
  ${EndIf}

  ; 7) Cleanup
  System::Call "userenv::DestroyEnvironmentBlock(p r4)"
  System::Call "kernel32::CloseHandle(p r3)"
  System::Call "kernel32::CloseHandle(p r2)"

done:
  Pop $R0    ; <-- get result into temp
  ; restore pushed temporaries (in reverse)
  Pop $9
  Pop $8
  Pop $7
  Pop $6
  Pop $5
  Pop $4
  Pop $3
  Pop $2
  Pop $1
  ; replace the original argument on stack with the result
  Exch $R0
FunctionEnd

; -----------------------------------------------------------------------------
; MapDriveForUser
; In: stack top = explicit path OR empty to use %LOCALAPPDATA% (of active user)
; -----------------------------------------------------------------------------
Function MapDriveForUser
  Pop $0             ; $0 = target path (may be empty to use %LOCALAPPDATA%)
  Push $1
  Push $2
  Push $3
  Push $4

  StrCpy $1 $0       ; working copy

  ; Normalize explicit target path (remove trailing '\')
  ${If} $1 != ""
    StrCpy $2 "$1" 1 -1
    ${If} $2 == "\"
      StrCpy $1 "$1" -1
    ${EndIf}
    !insertmacro LogMessage "Creating user-level ${TARGET_DRIVE} mapping to explicit path: $1"
  ${Else}
    !insertmacro LogMessage "Creating user-level ${TARGET_DRIVE} mapping using user's %LOCALAPPDATA%."
  ${EndIf}

  ; Remove any mapping in that user's session
  Push 'subst ${TARGET_DRIVE} /d'
  Call RunAsActiveConsoleUser
  Pop $4
  !insertmacro LogVar "RunAsActiveConsoleUser (delete)" $4

  ; Also remove mapping in this elevated session (no-op if none)
  nsExec::ExecToStack 'subst ${TARGET_DRIVE} /d'
  Pop $2
  Pop $3

  ; Create mapping in user session
  ${If} $1 != ""
    Push 'subst ${TARGET_DRIVE} "$1"'
  ${Else}
    Push 'subst ${TARGET_DRIVE} "%LOCALAPPDATA%"'
  ${EndIf}
  Call RunAsActiveConsoleUser
  Pop $4
  !insertmacro LogVar "RunAsActiveConsoleUser (create)" $4

  ; Do NOT create elevated mapping (avoids admin LOCALAPPDATA confusion)

  ; Notify shell (harmless for SUBST)
  System::Call "user32::SendMessageTimeoutW(p 0xffff, i ${WM_SETTINGCHANGE}, p 0, w 'Environment', i 0x2, i 5000, *i .r0)"
  System::Call "shell32::SHChangeNotify(i 0x00000100, i 0x0005, t '${TARGET_DRIVE}\\', p 0)"

  ; Quick visibility check from installer context (may not reflect user session)
  IfFileExists "${TARGET_DRIVE}\*.*" +2 0
    !insertmacro LogMessage "${TARGET_DRIVE} appears mounted (installer context)."

  Pop $4
  Pop $3
  Pop $2
  Pop $1
FunctionEnd

; -----------------------------------------------------------------------------
; Function to delete installer on abort
; -----------------------------------------------------------------------------
Function DeleteInstaller
  !insertmacro LogMessage "Deleting installer executable..."
  Sleep 1000
  ExecWait '"cmd.exe" /C ping 127.0.0.1 -n 2 > nul & del /f /q "$EXEPATH"'
  !insertmacro LogMessage "Installer deletion command executed: $EXEPATH"
FunctionEnd

; -----------------------------------------------------------------------------
; Function to handle early abortion (version conflicts)
; -----------------------------------------------------------------------------
Function .onGUIEnd
  ${If} ${Errors}
    !insertmacro LogMessage "Installation aborted with errors, deleting installer..."
    Call DeleteInstaller
  ${EndIf}
FunctionEnd

; -----------------------------------------------------------------------------
; Function called when installation fails
; -----------------------------------------------------------------------------
Function .onInstFailed
  !insertmacro LogMessage "Installation failed, deleting installer..."
  Call DeleteInstaller
FunctionEnd

; -----------------------------------------------------------------------------
; Function called when installation succeeds
; -----------------------------------------------------------------------------
Function .onInstSuccess
  !insertmacro LogMessage "Installation completed successfully!"

  ; Normalize target: $UserLocalAppDataDir (strip trailing '\')
  !insertmacro StrTrimTrailingSlash $0 $UserLocalAppDataDir
  !insertmacro LogVar "Target LOCALAPPDATA path (normalized)" $0

  ; Optional: log which user we are mapping for (from the user's session)
  Push 'echo USER=%USERNAME% ^& echo USERPROFILE=%USERPROFILE% ^& echo LOCALAPPDATA=%LOCALAPPDATA% >> "C:\FChassis_Install.log"'
  Call RunAsActiveConsoleUser
  Pop $1
  !insertmacro LogVar "WhoAmI (user session)" $1

  ; Remove any existing SUBST mapping for W: (installer session)
  !insertmacro LogMessage "Removing any existing SUBST mapping for W: (installer session)..."
  nsExec::ExecToStack 'subst W: /D'
  Pop $1
  Pop $2
  !insertmacro LogVar "SUBST delete exit" $1
  !insertmacro LogVar "SUBST delete out" $2

  ; Create user-level W: drive mapping to the REAL user's %LOCALAPPDATA%
  !insertmacro LogMessage "Creating user-level W: -> (user session %LOCALAPPDATA%)"
  Push ""                 ; <-- empty => expand %LOCALAPPDATA% in the user session
  Call MapDriveForUser
  !insertmacro LogMessage "Drive mapping created (user session)."

  ; PERSISTENCE: Write autorun under the real user's HKCU via that user's token
  !insertmacro LogMessage "Configuring user-level persistence (HKCU\\...\\Run) under real user hive..."
  ; Clean any accidental admin-HKCU value
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassis_MapW"
  ; Now write in the user's hive using their token
  Push 'reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v FChassis_MapW /t REG_EXPAND_SZ /d "cmd /c subst W: ""%LOCALAPPDATA%""" /f'
  Call RunAsActiveConsoleUser
  Pop $1
  !insertmacro LogVar "Set HKCU Run via user token" $1

  !insertmacro LogMessage "W: mapping persistence configured."
  MessageBox MB_OK "The W: drive has been mapped to your Local AppData directory ($UserLocalAppDataDir).$\n$\nThis mapping will be restored at your next sign-in."
FunctionEnd

; -----------------------------------------------------------------------------
; Version comparison function
; -----------------------------------------------------------------------------
Function CompareVersions
  Exch $0 ; version 1
  Exch
  Exch $1 ; version 2
  Push $2
  Push $3
  Push $4
  Push $5
  Push $6

  !insertmacro LogVar "Comparing version" $0
  !insertmacro LogVar "With version" $1

  StrCpy $2 0 ; index for version 1
  StrCpy $3 0 ; index for version 2

  compare_loop:
    StrCpy $4 $0 1 $2
    IntOp $2 $2 + 1
    StrCmp $4 "" get_part1_done 0
    StrCmp $4 "." get_part1_done 0
    Goto compare_loop

  get_part1_done:
    StrCpy $5 $0 $2
    IntOp $5 $5 - 1
    StrCpy $5 $0 $5 -$2
    IntOp $5 $5 + 1

    StrCpy $4 $1 1 $3
    IntOp $3 $3 + 1
    StrCmp $4 "" get_part2_done 0
    StrCmp $4 "." get_part2_done 0
    Goto get_part2_done

  get_part2_done:
    StrCpy $6 $1 $3
    IntOp $6 $6 - 1
    StrCpy $6 $1 $6 -$3
    IntOp $6 $6 + 1

    IntCmp $5 $6 parts_equal part1_greater part2_greater

    part1_greater:
      !insertmacro LogMessage "Version $0 is greater than $1"
      Pop $6
      Pop $5
      Pop $4
      Pop $3
      Pop $2
      Pop $1
      Pop $0
      Push 1
      Return

    part2_greater:
      !insertmacro LogMessage "Version $0 is less than $1"
      Pop $6
      Pop $5
      Pop $4
      Pop $3
      Pop $2
      Pop $1
      Pop $0
      Push -1
      Return

    parts_equal:
      StrCmp $0 "" check_version2 0
      StrCmp $1 "" version1_longer 0
      Goto compare_loop

  check_version2:
    StrCmp $1 "" versions_equal version2_longer

  versions_equal:
      !insertmacro LogMessage "Versions are equal"
      Pop $6
      Pop $5
      Pop $4
      Pop $3
      Pop $2
      Pop $1
      Pop $0
      Push 0
      Return

  version1_longer:
      !insertmacro LogMessage "Version $0 is longer than $1 (greater)"
      Pop $6
      Pop $5
      Pop $4
      Pop $3
      Pop $2
      Pop $1
      Pop $0
      Push 1
      Return

  version2_longer:
      !insertmacro LogMessage "Version $1 is longer than $0 (greater)"
      Pop $6
      Pop $5
      Pop $4
      Pop $3
      Pop $2
      Pop $1
      Pop $0
      Push -1
      Return
FunctionEnd

; -----------------------------------------------------------------------------
; Installation detection
; -----------------------------------------------------------------------------
Function .onInit
  !insertmacro LogMessage "=== Starting .onInit function ==="

  ; Cache LOCALAPPDATA before we do anything else
  StrCpy $UserLocalAppDataDir "$LOCALAPPDATA"
  ; Open log ASAP, then log cached path
  FileOpen $LogFileHandle "C:\FChassis_Install.log" w
  FileWrite $LogFileHandle "=== FChassis Installation Log ===$\r$\n"
  FileWrite $LogFileHandle "Started: [TIME]$\r$\n"
  !insertmacro LogMessage "Log file opened: C:\FChassis_Install.log"
  !insertmacro LogVar "Cached LOCALAPPDATA" $UserLocalAppDataDir

  StrCpy $InstalledState 0
  StrCpy $ExistingInstallDir "${INSTALLDIR}"
  StrCpy $ExistingVersion ""
  StrCpy $ExtractionCompleted 0

  !insertmacro LogMessage "Checking registry for existing installation..."

  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
  IfErrors check_install_flag
  !insertmacro LogMessage "Found uninstall registry key"
  !insertmacro LogVar "UninstallString" $0

  ReadRegStr $ExistingInstallDir HKLM "${UNINSTALL_KEY}" "InstallLocation"
  StrCmp $ExistingInstallDir "" 0 get_version
  StrCpy $ExistingInstallDir "${INSTALLDIR}"
  !insertmacro LogVar "ExistingInstallDir" $ExistingInstallDir

get_version:
  ReadRegStr $ExistingVersion HKLM "${UNINSTALL_KEY}" "DisplayVersion"
  IfErrors check_install_flag
  !insertmacro LogVar "ExistingVersion" $ExistingVersion

  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  !insertmacro LogVar "Version comparison result" $1

  ${If} $1 == 0
    StrCpy $InstalledState 1
    !insertmacro LogMessage "Same version already installed"
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3
    !insertmacro LogMessage "Newer version already installed"
  ${Else}
    StrCpy $InstalledState 2
    !insertmacro LogMessage "Older version already installed"
  ${EndIf}
  Goto done

check_install_flag:
  !insertmacro LogMessage "Checking custom install flag registry..."
  ReadRegStr $0 HKLM "${INSTALL_FLAG_KEY}" "Installed"
  StrCmp $0 "1" 0 done
  !insertmacro LogMessage "Found custom install flag"

  ReadRegStr $ExistingVersion HKLM "${INSTALL_FLAG_KEY}" "Version"
  ReadRegStr $ExistingInstallDir HKLM "${INSTALL_FLAG_KEY}" "InstallPath"
  !insertmacro LogVar "ExistingVersion from custom key" $ExistingVersion
  !insertmacro LogVar "ExistingInstallDir from custom key" $ExistingInstallDir

  StrCmp $ExistingVersion "" done 0
  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  !insertmacro LogVar "Version comparison result (custom key)" $1

  ${If} $1 == 0
    StrCpy $InstalledState 1
    !insertmacro LogMessage "Same version installed (custom key)"
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3
    !insertmacro LogMessage "Newer version installed (custom key)"
  ${Else}
    StrCpy $InstalledState 2
    !insertmacro LogMessage "Older version installed (custom key)"
  ${EndIf}

done:
  StrCpy $INSTDIR $ExistingInstallDir
  !insertmacro LogVar "Final INSTDIR set to" $INSTDIR
  !insertmacro LogVar "InstalledState" $InstalledState

  !insertmacro LogMessage "=== .onInit function completed ==="
FunctionEnd

Function DirectoryPre
  !insertmacro LogMessage "=== Starting DirectoryPre function ==="
  ${If} $InstalledState == 3
    !insertmacro LogMessage "Newer version detected, showing warning message"
    MessageBox MB_OK|MB_ICONEXCLAMATION "A newer version ($ExistingVersion) of ${APPNAME} is already installed.$\nCannot downgrade to version ${VERSION}.$\n$\nPlease uninstall the newer version first."
    !insertmacro LogMessage "Deleting installer due to version conflict..."
    Call DeleteInstaller
    Abort
  ${EndIf}
  !insertmacro LogMessage "=== DirectoryPre function completed ==="
FunctionEnd

Function InstFilesShow
  !insertmacro LogMessage "=== InstFiles page shown==="
FunctionEnd

; -----------------------------------------------------------------------------
; Extract thirdParty.zip with progress feedback
; -----------------------------------------------------------------------------
Function ExtractThirdParty
  !insertmacro LogMessage "=== Starting ExtractThirdParty function ==="

  IfFileExists "$INSTDIR\Bin\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin\TKernel.dll" extraction_complete
  !insertmacro LogMessage "ThirdParty extraction needed"

  DetailPrint "Extracting thirdParty.zip..."
  DetailPrint "This may take several minutes (90,000+ files)..."
  !insertmacro LogMessage "Extracting thirdParty.zip (90,000+ files, may take several minutes)"

  SetDetailsPrint listonly
  DetailPrint "Extracting: Please wait patiently..."
  SetDetailsPrint both

  !insertmacro LogMessage "Starting 7z extraction process..."
  nsExec::ExecToStack '"$INSTDIR\7z.exe" x "$INSTDIR\thirdParty.zip" -o"$INSTDIR" -y'
  Pop $0
  Pop $1

  ${If} $0 != 0
    !insertmacro LogError "7-Zip extraction failed with code: $0"
    !insertmacro LogError "7-Zip output: $1"
    DetailPrint "7-Zip extraction failed with code: $0"
    DetailPrint "Output: $1"
    MessageBox MB_OK|MB_ICONEXCLAMATION "Failed to extract third-party components. Installation may be incomplete."
  ${Else}
    StrCpy $ExtractionCompleted 1
    !insertmacro LogMessage "Third-party components extraction completed successfully."
    DetailPrint "Third-party components extraction completed successfully."
  ${EndIf}

extraction_complete:
  !insertmacro LogMessage "ThirdParty extraction already complete or completed"
  !insertmacro LogMessage "=== ExtractThirdParty function completed ==="
FunctionEnd

; -----------------------------------------------------------------------------
; Handle cancellation during installation
; -----------------------------------------------------------------------------
Function OnInstFilesAbort
  !insertmacro LogMessage "=== Installation abort requested ==="
  MessageBox MB_YESNO|MB_ICONQUESTION "Are you sure you want to cancel the installation?" IDYES +2
  Return

  !insertmacro LogMessage "User confirmed cancellation, performing cleanup"
  MessageBox MB_OK|MB_ICONINFORMATION "Installation canceled. Cleaning up..."

  ; Remove installed files and directories
  !insertmacro LogMessage "Removing installed files and directories..."
  RMDir /r "$INSTDIR\Bin"
  RMDir /r "$INSTDIR\cs"
  RMDir /r "$INSTDIR\de"
  RMDir /r "$INSTDIR\es"
  RMDir /r "$INSTDIR\fr"
  RMDir /r "$INSTDIR\hoops"
  RMDir /r "$INSTDIR\it"
  RMDir /r "$INSTDIR\ja"
  RMDir /r "$INSTDIR\ko"
  RMDir /r "$INSTDIR\pl"
  RMDir /r "$INSTDIR\pt-BR"
  RMDir /r "$INSTDIR\ru"
  RMDir /r "$INSTDIR\runtimes"
  RMDir /r "$INSTDIR\tr"
  RMDir /r "$INSTDIR\zh-Hans"
  RMDir /r "$INSTDIR\zh-Hant"

  !insertmacro LogMessage "Removing individual files..."
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\*.exe"
  Delete "$INSTDIR\*.json"
  Delete "$INSTDIR\*.xml"
  Delete "$INSTDIR\*.wad"
  Delete "$INSTDIR\*.pdb"
  Delete "$INSTDIR\*.exp"
  Delete "$INSTDIR\*.lib"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\thirdParty.zip"
  Delete "$INSTDIR\7z.exe"
  Delete "$INSTDIR\7z.dll"
  Delete "$INSTDIR\VC_redist.x64.exe"

  ; Remove shortcuts
  !insertmacro LogMessage "Removing shortcuts..."
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"

  ; Remove registry entries
  !insertmacro LogMessage "Removing registry entries..."
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"

  ; Remove DOS Devices entry
  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "${DRIVE_VALUE}"

  ; Remove env PATH additions
  !insertmacro LogMessage "Removing environment variables..."
  EnVar::SetHKLM
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb-2018.2.1-vc14-64\bin"

  !insertmacro BroadcastEnvChange

  ; Delete installer
  Call DeleteInstaller

  ; Close log
  FileClose $LogFileHandle

  Abort
FunctionEnd

; -----------------------------------------------------------------------------
; Installation Section
; -----------------------------------------------------------------------------
Section "Main Installation" SecMain
  !insertmacro LogMessage "=== Starting Main Installation Section ==="

  SetOutPath "$INSTDIR"

  ${If} $InstalledState == 1
    !insertmacro LogMessage "Same version already installed, proceeding with reinstallation"
  ${ElseIf} $InstalledState == 2
    !insertmacro LogMessage "Older version detected, proceeding with upgrade"
  ${EndIf}

  !insertmacro LogMessage "Copying main application files..."
  File /r "${FluxSDKDir}\*.*"

  !insertmacro LogMessage "Copying third-party components..."
  File "${FluxSDKDir}\thirdParty.zip"
  File "${FluxSDKDir}\7z.exe"
  File "${FluxSDKDir}\7z.dll"
  File "${FluxSDKDir}\VC_redist.x64.exe"

  Call ExtractThirdParty

  !insertmacro LogMessage "Checking VC++ redistributable installation..."
  nsExec::ExecToStack '"$INSTDIR\VC_redist.x64.exe" /install /quiet /norestart'
  Pop $0
  !insertmacro LogVar "VC++ redistributable installation result" $0

  !insertmacro LogMessage "Creating shortcuts..."
  SetShellVarContext all
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"
  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"

  !insertmacro LogMessage "Adding to PATH environment variable..."
  EnVar::SetHKLM
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb-2018.2.1-vc14-64\bin"

  !insertmacro BroadcastEnvChange

  !insertmacro LogMessage "Writing registry entries..."
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${COMPANY}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1

  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Installed" "1"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Version" "${VERSION}"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "InstallPath" "$INSTDIR"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  !insertmacro LogMessage "=== Main Installation Section completed ==="
SectionEnd

; -----------------------------------------------------------------------------
; Uninstaller Section
; -----------------------------------------------------------------------------
Section "Uninstall"
  !insertmacro LogMessage "=== Starting Uninstallation ==="

  !insertmacro LogMessage "Removing shortcuts..."
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"

  !insertmacro LogMessage "Removing from PATH environment variable..."
  EnVar::SetHKLM
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb-2018.2.1-vc14-64\bin"

  !insertmacro BroadcastEnvChange

  !insertmacro LogMessage "Removing registry entries..."
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"

  !insertmacro LogMessage "Removing W: drive mapping persistence..."
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassis_MapW"

  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "${DRIVE_VALUE}"

  !insertmacro LogMessage "Removing W: drive mapping..."
  nsExec::ExecToStack 'subst W: /D'
  Pop $0
  Pop $1
  !insertmacro LogVar "SUBST delete exit" $0
  !insertmacro LogVar "SUBST delete out" $1

  !insertmacro LogMessage "Removing files and directories..."
  RMDir /r "$INSTDIR\Bin"
  RMDir /r "$INSTDIR\cs"
  RMDir /r "$INSTDIR\de"
  RMDir /r "$INSTDIR\es"
  RMDir /r "$INSTDIR\fr"
  RMDir /r "$INSTDIR\hoops"
  RMDir /r "$INSTDIR\it"
  RMDir /r "$INSTDIR\ja"
  RMDir /r "$INSTDIR\ko"
  RMDir /r "$INSTDIR\pl"
  RMDir /r "$INSTDIR\pt-BR"
  RMDir /r "$INSTDIR\ru"
  RMDir /r "$INSTDIR\runtimes"
  RMDir /r "$INSTDIR\tr"
  RMDir /r "$INSTDIR\zh-Hans"
  RMDir /r "$INSTDIR\zh-Hant"
  RMDir /r "$INSTDIR\thirdParty"

  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\*.exe"
  Delete "$INSTDIR\*.json"
  Delete "$INSTDIR\*.xml"
  Delete "$INSTDIR\*.wad"
  Delete "$INSTDIR\*.pdb"
  Delete "$INSTDIR\*.exp"
  Delete "$INSTDIR\*.lib"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\thirdParty.zip"
  Delete "$INSTDIR\7z.exe"
  Delete "$INSTDIR\7z.dll"
  Delete "$INSTDIR\VC_redist.x64.exe"

  RMDir "$INSTDIR"

  !insertmacro LogMessage "=== Uninstallation completed ==="
SectionEnd
