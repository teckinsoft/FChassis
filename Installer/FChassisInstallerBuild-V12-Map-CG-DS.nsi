!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinMessages.nsh"
!include "MUI2.nsh"

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
  System::Call 'USER32::SendMessageTimeout(i ${HWND_BROADCAST}, i ${WM_SETTINGCHANGE}, i 0, w "Environment", i 0, i 5000, i 0)'
  !insertmacro LogMessage "Environment change broadcast completed"
!macroend

; -----------------------------------------------------------------------------
; String manipulation functions
; -----------------------------------------------------------------------------
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

; ; -----------------------------------------------------------------------------
; ; Function called when installation succeeds  
; ; -----------------------------------------------------------------------------
; Function .onInstSuccess
  ; !insertmacro LogMessage "Installation completed successfully!"

  ; ; ------------------------------------------------------------
  ; ; Normalize target: $UserLocalAppDataDir (strip trailing '\')
  ; ; ------------------------------------------------------------
  ; StrCpy $0 $UserLocalAppDataDir
  ; !insertmacro LogVar "Original LOCALAPPDATA path" $0
  
  ; ; Simple trailing backslash removal
  ; StrCpy $1 "$0" 1 -1
  ; ${If} $1 == "\"
    ; StrCpy $0 "$0" -1
  ; ${EndIf}
  
  ; !insertmacro LogVar "Target LOCALAPPDATA path (normalized)" $0

  ; ; ------------------------------------------------------------
  ; ; Create the drive mapping using a simple approach
  ; ; ------------------------------------------------------------
  ; !insertmacro LogMessage "Creating W: drive mapping..."
  
  ; ; Remove any existing mapping first
  ; nsExec::ExecToStack 'subst W: /D'
  ; Pop $1
  ; Pop $2
  
  ; ; Create the mapping
  ; nsExec::ExecToStack 'subst W: "$0"'
  ; Pop $1
  ; Pop $2
  ; !insertmacro LogVar "SUBST exit code" $1
  ; !insertmacro LogVar "SUBST output" $2
  
  ; ${If} $1 != 0
    ; !insertmacro LogError "Failed to create SUBST mapping for W:"
  ; ${Else}
    ; !insertmacro LogMessage "Drive mapping created: W:\ now points to $0"
  ; ${EndIf}

  ; ; ------------------------------------------------------------
  ; ; Set up persistence using DOS Devices registry
  ; ; ------------------------------------------------------------
  ; !insertmacro LogMessage "Setting up persistence using DOS Devices registry..."
  ; StrCpy $1 '\??\$0'
  ; WriteRegStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "${DRIVE_VALUE}" "$1"
  ; ${If} ${Errors}
    ; !insertmacro LogError "Failed to write DOS Devices registry entry"
  ; ${Else}
    ; !insertmacro LogMessage "DOS Devices registry entry created successfully"
  ; ${EndIf}

  ; ; ------------------------------------------------------------
  ; ; Also set up user-level persistence for redundancy
  ; ; ------------------------------------------------------------
  ; !insertmacro LogMessage "Configuring user-level persistence (HKCU\\...\\Run)..."
  ; WriteRegExpandStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassis_MapW" 'cmd /c subst W: "%LOCALAPPDATA%"'
  ; ${If} ${Errors}
    ; !insertmacro LogError "Failed to write HKCU Run persistence entry."
  ; ${Else}
    ; !insertmacro LogMessage "User-level persistence entry created: HKCU\\...\\Run\\FChassis_MapW"
  ; ${EndIf}

  ; ; Notify system about changes
  ; !insertmacro BroadcastEnvChange

  ; ; Show message to user about the drive mapping
  ; MessageBox MB_OK "The W: drive has been mapped to your Local AppData directory ($0).$\n$\nThis mapping will persist after reboot."

; FunctionEnd



; -----------------------------------------------------------------------------
; Function called when installation succeeds  
; -----------------------------------------------------------------------------
Function .onInstSuccess
  !insertmacro LogMessage "Installation completed successfully!"

  ; ------------------------------------------------------------
  ; Normalize target: $UserLocalAppDataDir (strip trailing '\')
  ; ------------------------------------------------------------
  StrCpy $0 $UserLocalAppDataDir
  !insertmacro LogVar "Original LOCALAPPDATA path" $0
  
  ; Simple trailing backslash removal
  StrCpy $1 "$0" 1 -1
  ${If} $1 == "\"
    StrCpy $0 "$0" -1
  ${EndIf}
  
  !insertmacro LogVar "Target LOCALAPPDATA path (normalized)" $0

  ; ------------------------------------------------------------
  ; Create the drive mapping in user context
  ; ------------------------------------------------------------
  !insertmacro LogMessage "Creating W: drive mapping in user context..."
  
  ; Create a temporary batch file to run the subst command
  GetTempFileName $1
  FileOpen $2 "$1.bat" w
  FileWrite $2 "@echo off$\r$\n"
  FileWrite $2 "subst W: /D > nul 2>&1$\r$\n"
  FileWrite $2 "subst W: $\"$0$\"$\r$\n"
  FileWrite $2 "exit$\r$\n"
  FileClose $2
  
  ; Execute the batch file as the current user (not elevated)
  ; Use the runas command with the /savecred flag to avoid password prompts
  !insertmacro LogMessage "Executing drive mapping as current user..."
  ExecWait '"runas" /user:$USERDOMAIN\$USERNAME /savecred "$1.bat"'
  
  ; Clean up the batch file
  Delete "$1.bat"

  ; ------------------------------------------------------------
  ; Set up persistence using DOS Devices registry
  ; ------------------------------------------------------------
  !insertmacro LogMessage "Setting up persistence using DOS Devices registry..."
  StrCpy $1 '\??\$0'
  WriteRegStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "${DRIVE_VALUE}" "$1"
  ${If} ${Errors}
    !insertmacro LogError "Failed to write DOS Devices registry entry"
  ${Else}
    !insertmacro LogMessage "DOS Devices registry entry created successfully"
  ${EndIf}

  ; ------------------------------------------------------------
  ; Also set up user-level persistence for redundancy
  ; ------------------------------------------------------------
  !insertmacro LogMessage "Configuring user-level persistence (HKCU\\...\\Run)..."
  WriteRegExpandStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassis_MapW" 'cmd /c subst W: "%LOCALAPPDATA%"'
  ${If} ${Errors}
    !insertmacro LogError "Failed to write HKCU Run persistence entry."
  ${Else}
    !insertmacro LogMessage "User-level persistence entry created: HKCU\\...\\Run\\FChassis_MapW"
  ${EndIf}

  ; Notify system about changes
  !insertmacro BroadcastEnvChange

  ; Show message to user about the drive mapping
  MessageBox MB_OK "The W: drive has been mapped to your Local AppData directory ($0).$\n$\nThis mapping will persist after reboot."

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
    ; Get next part of version 1
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

    ; Get next part of version 2
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

    ; Compare the parts
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
      Push 1 ; version 1 > version 2
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
      Push -1 ; version 1 < version 2
      Return

    parts_equal:
      ; Check if both versions are complete
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
      Push 0 ; versions equal
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
      Push 1 ; version 1 > version 2
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
      Push -1 ; version 1 < version 2
      Return
FunctionEnd

; -----------------------------------------------------------------------------
; Installation detection
; -----------------------------------------------------------------------------
Function .onInit
  !insertmacro LogMessage "=== Starting .onInit function ==="
  
  ; Cache LOCALAPPDATA before UAC elevation
  StrCpy $UserLocalAppDataDir "$LOCALAPPDATA"
  !insertmacro LogVar "Cached LOCALAPPDATA" $UserLocalAppDataDir
  
  ; Initialize logging
  FileOpen $LogFileHandle "C:\FChassis_Install.log" w
  FileWrite $LogFileHandle "=== FChassis Installation Log ===$\r$\n"
  FileWrite $LogFileHandle "Started: [TIME]$\r$\n"
  !insertmacro LogMessage "Log file opened: C:\FChassis_Install.log"

  StrCpy $InstalledState 0 ; Default to not installed
  StrCpy $ExistingInstallDir "${INSTALLDIR}"
  StrCpy $ExistingVersion ""
  StrCpy $ExtractionCompleted 0

  !insertmacro LogMessage "Checking registry for existing installation..."

  ; Check if already installed via uninstall registry
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
  IfErrors check_install_flag
  !insertmacro LogMessage "Found uninstall registry key"
  !insertmacro LogVar "UninstallString" $0
  
  ; Get existing installation directory
  ReadRegStr $ExistingInstallDir HKLM "${UNINSTALL_KEY}" "InstallLocation"
  StrCmp $ExistingInstallDir "" 0 get_version
  StrCpy $ExistingInstallDir "${INSTALLDIR}"
  !insertmacro LogVar "ExistingInstallDir" $ExistingInstallDir
  
  get_version:
  ; Get installed version
  ReadRegStr $ExistingVersion HKLM "${UNINSTALL_KEY}" "DisplayVersion"
  IfErrors check_install_flag
  !insertmacro LogVar "ExistingVersion" $ExistingVersion
  
  ; Compare versions
  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  !insertmacro LogVar "Version comparison result" $1
  
  ${If} $1 == 0
    StrCpy $InstalledState 1 ; Same version installed
    !insertmacro LogMessage "Same version already installed"
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3 ; Newer version installed
    !insertmacro LogMessage "Newer version already installed"
  ${Else}
    StrCpy $InstalledState 2 ; Older version installed
    !insertmacro LogMessage "Older version already installed"
  ${EndIf}
  
  Goto done
  
  check_install_flag:
  !insertmacro LogMessage "Checking custom install flag registry..."
  ; Check our custom install flag
  ReadRegStr $0 HKLM "${INSTALL_FLAG_KEY}" "Installed"
  StrCmp $0 "1" 0 done
  !insertmacro LogMessage "Found custom install flag"
  
  ReadRegStr $ExistingVersion HKLM "${INSTALL_FLAG_KEY}" "Version"
  ReadRegStr $ExistingInstallDir HKLM "${INSTALL_FLAG_KEY}" "InstallPath"
  !insertmacro LogVar "ExistingVersion from custom key" $ExistingVersion
  !insertmacro LogVar "ExistingInstallDir from custom key" $ExistingInstallDir
  
  ; Compare versions if we found version info
  StrCmp $ExistingVersion "" done 0
  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  !insertmacro LogVar "Version comparison result (custom key)" $1
  
  ${If} $1 == 0
    StrCpy $InstalledState 1 ; Same version installed
    !insertmacro LogMessage "Same version installed (custom key)"
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3 ; Newer version installed
    !insertmacro LogMessage "Newer version installed (custom key)"
  ${Else}
    StrCpy $InstalledState 2 ; Older version installed
    !insertmacro LogMessage "Older version installed (custom key)"
  ${EndIf}
  
  done:
  ; Set install directory to existing installation path
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
    
    ; Delete installer after showing message
    !insertmacro LogMessage "Deleting installer due to version conflict..."
    Call DeleteInstaller
    
    Abort
  ${EndIf}
  !insertmacro LogMessage "=== DirectoryPre function completed ==="
FunctionEnd

Function InstFilesShow
  !insertmacro LogMessage "=== InstFiles page shown ==="
FunctionEnd

; -----------------------------------------------------------------------------
; Function to extract thirdParty.zip with progress feedback
; -----------------------------------------------------------------------------
Function ExtractThirdParty
  !insertmacro LogMessage "=== Starting ExtractThirdParty function ==="
  
  ; Check if extraction is already done
  IfFileExists "$INSTDIR\Bin\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin\TKernel.dll" extraction_complete
  !insertmacro LogMessage "ThirdParty extraction needed"
  
  DetailPrint "Extracting thirdParty.zip..."
  DetailPrint "This may take several minutes (90,000+ files)..."
  !insertmacro LogMessage "Extracting thirdParty.zip (90,000+ files, may take several minutes)"
  
  ; Show progress message
  SetDetailsPrint listonly
  DetailPrint "Extracting: Please wait patiently..."
  SetDetailsPrint both
  
  ; Extract using 7z with timeout
  !insertmacro LogMessage "Starting 7z extraction process..."
  nsExec::ExecToStack '"$INSTDIR\7z.exe" x "$INSTDIR\thirdParty.zip" -o"$INSTDIR" -y'
  Pop $0 ; Exit code
  Pop $1 ; Output
  
  ${If} $0 != 0
    !insertmacro LogError "7-Zip extraction failed with code: $0"
    !insertmacro LogError "7-Zip output: $1"
    DetailPrint "7-Zip extraction failed with code: $0"
    DetailPrint "Output: $1"
    MessageBox MB_OK|MB_ICONEXCLAMATION "Failed to extract third-party components. Installation may be incomplete."
  ${Else}
    StrCpy $ExtractionCompleted 1
    !insertmacro LogMessage "Third-party components extraction completed successfully"
    DetailPrint "Third-party components extraction completed successfully."
  ${EndIf}
  
  extraction_complete:
  !insertmacro LogMessage "ThirdParty extraction already complete or completed"
  !insertmacro LogMessage "=== ExtractThirdParty function completed ==="
FunctionEnd

; -----------------------------------------------------------------------------
; Function to handle cancellation during installation
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
  
  ; Remove individual files
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
  
  ; Remove shortcuts if they were created
  !insertmacro LogMessage "Removing shortcuts..."
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"
  
  ; Remove registry entries if they were created
  !insertmacro LogMessage "Removing registry entries..."
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"
  
  ; Remove DOS Devices registry entry
  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "${DRIVE_VALUE}"
  
  ; Remove environment variables if they were added
  !insertmacro LogMessage "Removing environment variables..."
  EnVar::SetHKLM
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb-2018.2.1-vc14-64\bin"
  
  ; Broadcast environment change
  !insertmacro BroadcastEnvChange
  
  ; Delete installer
  Call DeleteInstaller
  
  ; Close log file
  FileClose $LogFileHandle
  
  Abort
FunctionEnd

; -----------------------------------------------------------------------------
; Installation Section
; -----------------------------------------------------------------------------
Section "Main Installation" SecMain
  !insertmacro LogMessage "=== Starting Main Installation Section ==="
  
  ; Set output path to the installation directory
  SetOutPath "$INSTDIR"
  
  ; Check if we need to uninstall previous version
  ${If} $InstalledState == 1
    !insertmacro LogMessage "Same version already installed, proceeding with reinstallation"
  ${ElseIf} $InstalledState == 2
    !insertmacro LogMessage "Older version detected, proceeding with upgrade"
  ${EndIf}
  
  ; Copy main application files
  !insertmacro LogMessage "Copying main application files..."
  File /r "${FluxSDKDir}\*.*"
  
  ; Copy third-party components
  !insertmacro LogMessage "Copying third-party components..."
  File "${FluxSDKDir}\thirdParty.zip"
  File "${FluxSDKDir}\7z.exe"
  File "${FluxSDKDir}\7z.dll"
  File "${FluxSDKDir}\VC_redist.x64.exe"
  
  ; Extract third-party components
  Call ExtractThirdParty
  
  ; Install VC++ redistributable if needed
  !insertmacro LogMessage "Checking VC++ redistributable installation..."
  nsExec::ExecToStack '"$INSTDIR\VC_redist.x64.exe" /install /quiet /norestart'
  Pop $0
  !insertmacro LogVar "VC++ redistributable installation result" $0
  
  ; Create shortcuts
  !insertmacro LogMessage "Creating shortcuts..."
  SetShellVarContext all
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortCut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"
  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"
  
  ; Add to PATH environment variable
  !insertmacro LogMessage "Adding to PATH environment variable..."
  EnVar::SetHKLM
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb-2018.2.1-vc14-64\bin"
  
  ; Broadcast environment change
  !insertmacro BroadcastEnvChange
  
  ; Write registry entries for uninstallation
  !insertmacro LogMessage "Writing registry entries..."
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${COMPANY}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
  
  ; Write our custom install flag
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Installed" "1"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Version" "${VERSION}"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "InstallPath" "$INSTDIR"
  
  ; Create uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  
  !insertmacro LogMessage "=== Main Installation Section completed ==="
SectionEnd

; -----------------------------------------------------------------------------
; Uninstaller Section
; -----------------------------------------------------------------------------
Section "Uninstall"
  !insertmacro LogMessage "=== Starting Uninstallation ==="
  
  ; Remove shortcuts
  !insertmacro LogMessage "Removing shortcuts..."
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"
  
  ; Remove from PATH environment variable
  !insertmacro LogMessage "Removing from PATH environment variable..."
  EnVar::SetHKLM
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb-2018.2.1-vc14-64\bin"
  
  ; Broadcast environment change
  !insertmacro BroadcastEnvChange
  
  ; Remove registry entries
  !insertmacro LogMessage "Removing registry entries..."
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"
  
  ; Remove W: drive mapping persistence
  !insertmacro LogMessage "Removing W: drive mapping persistence..."
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassis_MapW"
  
  ; Remove DOS Devices registry entry
  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "${DRIVE_VALUE}"
  
  ; Remove W: drive mapping if it exists
  !insertmacro LogMessage "Removing W: drive mapping..."
  nsExec::ExecToStack 'subst W: /D'
  Pop $0
  Pop $1
  !insertmacro LogVar "SUBST delete exit" $0
  !insertmacro LogVar "SUBST delete out" $1
  
  ; Remove files and directories
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
  
  ; Remove individual files
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
  
  ; Remove installation directory if empty
  RMDir "$INSTDIR"
  
  !insertmacro LogMessage "=== Uninstallation completed ==="
SectionEnd