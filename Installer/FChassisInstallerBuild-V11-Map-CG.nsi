!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinMessages.nsh"
!include "MUI2.nsh"

; -----------------------------------------------------------------------------
; FIX (warning 6010): Only include/initialize StrStr IF it is actually used.
; -----------------------------------------------------------------------------
!macro _EnsureStrStr
  !ifndef _STRFUNC_STRSTR_READY
    !define _STRFUNC_STRSTR_READY
    !include "StrFunc.nsh"
    ${StrStr}
  !endif
!macroend

!macro StrStrFind OUT HAYSTACK NEEDLE
  !insertmacro _EnsureStrStr
  ${StrStr} ${OUT} ${HAYSTACK} ${NEEDLE}
!macroend

; Requires StrFunc helpers when we need lowercase/substring
!macro _EnsureStrFunc
  !ifndef _STRFUNC_BLOCK_READY
    !define _STRFUNC_BLOCK_READY
    !include "StrFunc.nsh"
    ${StrStr}
    ${StrCase}
  !endif
!macroend

!macro StrToLower OUT IN
  !insertmacro _EnsureStrFunc
  ${StrCase} "${IN}" "L" ${OUT}
!macroend


;--------------------------------
; Build-time defines
;--------------------------------
!ifndef VERSION
  !define VERSION "1.0.4"
!endif

!ifndef INSTALLDIR
  !define INSTALLDIR "C:\FChassis"
!endif

!ifndef FluxSDKBin
  !define FluxSDKBin "C:\FluxSDK\Bin"
!endif

!ifndef FluxSDKDir
  !define FluxSDKDir "C:\FluxSDK"
!endif

;--------------------------------
; General Settings
;--------------------------------
!define APPNAME "FChassis"
!define COMPANY "Teckinsoft Neuronics Pvt. Ltd."
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
!define INSTALL_FLAG_KEY "Software\${COMPANY}\${APPNAME}"

Name "${APPNAME} ${VERSION}"
OutFile "FChassisInstaller.exe"
InstallDir "${INSTALLDIR}"
RequestExecutionLevel admin

;--------------------------------
; Variables
;--------------------------------
Var InstalledState ; 0 = not installed, 1 = same version, 2 = older version, 3 = newer version
Var ExistingInstallDir
Var ExistingVersion
Var ExtractionCompleted
Var LogFileHandle
Var UserLocalAppDataDir

;--------------------------------
; Pages
;--------------------------------
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

;--------------------------------
; Logging Macros
;--------------------------------
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

;--------------------------------
; Helper: broadcast env change
;--------------------------------
!macro BroadcastEnvChange
  !insertmacro LogMessage "Broadcasting environment change..."
  System::Call 'USER32::SendMessageTimeout(p ${HWND_BROADCAST}, i ${WM_SETTINGCHANGE}, p 0, t "Environment", i 0, i 5000, *i .r0)'
  !insertmacro LogMessage "Environment change broadcast completed"
!macroend

;--------------------------------
; Function to delete installer on abort
;--------------------------------
Function DeleteInstaller
  !insertmacro LogMessage "Deleting installer executable..."
  ; Give it a moment to release file handles
  Sleep 1000
  ; Delete the installer using cmd to avoid locking issues
  ExecWait '"cmd.exe" /C ping 127.0.0.1 -n 2 > nul & del /f /q "$EXEPATH"'
  !insertmacro LogMessage "Installer deletion command executed: $EXEPATH"
FunctionEnd

;--------------------------------
; Function to handle early abortion (version conflicts)
;--------------------------------
Function .onGUIEnd
  ; If installation was aborted (not completed successfully), delete installer
  ${If} ${Errors}
    !insertmacro LogMessage "Installation aborted with errors, deleting installer..."
    Call DeleteInstaller
  ${EndIf}
FunctionEnd

;--------------------------------
; Function called when installation fails
;--------------------------------
Function .onInstFailed
  !insertmacro LogMessage "Installation failed, deleting installer..."
  Call DeleteInstaller
FunctionEnd

;--------------------------------
; Function called when installation succeeds  
;--------------------------------
Function .onInstSuccess
  !insertmacro LogMessage "Installation completed successfully!"

  ; ------------------------------------------------------------
  ; Normalize target: $UserLocalAppDataDir (strip trailing '\')
  ; ------------------------------------------------------------
  StrCpy $0 "$UserLocalAppDataDir"
  ${Do}
    StrCpy $1 "$0" "" -1
    StrCmp $1 "\" 0 done_norm_target
    StrCpy $0 "$0" -1
  ${Loop}
  done_norm_target:
  !insertmacro LogVar "Target LOCALAPPDATA path (normalized)" $0

  ; ------------------------------------------------------------
  ; Discover what W: currently is using QueryDosDevice
  ; ------------------------------------------------------------
  System::Call 'kernel32::QueryDosDeviceW(w "W:", w .r2, i ${NSIS_MAX_STRLEN}) i .r3'
  ${If} $3 = 0
    !insertmacro LogMessage "W: does not exist (no device mapping)."
    StrCpy $4 ""          ; $4 = current target (none)
    StrCpy $5 "NONE"      ; $5 = type
  ${Else}
    !insertmacro LogVar "QueryDosDevice(W:)" $2

    ; Detect kind
    StrCpy $5 "UNKNOWN"
    StrCpy $4 ""          ; derived target (if any)
    StrCpy $6 "$2"        ; working copy

    !insertmacro StrToLower $7 "$6"

    ; Check for SUBST prefix "\??\"
    StrCpy $8 "\??\"
    !insertmacro StrToLower $9 "$8"
    ${If} "${7}" != ""
      StrCpy $A "${7}" 4
      StrCmp $A "\??\" 0 +3
        StrCpy $5 "SUBST"
        StrCpy $4 "$6" "" 4  ; strip "\??\"
        Goto mapped_done
    ${EndIf}

    ; Check for LanmanRedirector (network)
    !insertmacro StrToLower $A "$6"
    ${If} ${StrStr} $A "\device\lanmanredirector\" $B
      StrCpy $5 "NETWORK"
      StrCpy $4 ""
      Goto mapped_done
    ${EndIf}

    ; Physical volume?
    ${If} ${StrStr} $A "\device\harddiskvolume" $B
      StrCpy $5 "PHYSICAL"
      StrCpy $4 ""
      Goto mapped_done
    ${EndIf}

    mapped_done:
    !insertmacro LogVar "W: current type" $5
    !insertmacro LogVar "W: current target (raw/derived)" $4
  ${EndIf}

  ; ------------------------------------------------------------
  ; If W: is SUBST and already points to our LOCALAPPDATA, keep it
  ; ------------------------------------------------------------
  StrCpy $B "$4"
  ${Do}
    StrCpy $C "$B" "" -1
    StrCmp $C "\" 0 done_norm_current
    StrCpy $B "$B" -1
  ${Loop}
  done_norm_current:

  !insertmacro StrToLower $D "$B"
  !insertmacro StrToLower $E "$0"

  !insertmacro LogVar "W: normalized current" $B
  !insertmacro LogVar "Target normalized" $0

  ${If} "$5" == "SUBST"
    ${If} "$D" == "$E"
      !insertmacro LogMessage "W: already SUBST-mapped to LOCALAPPDATA. No changes needed."
      ; Ensure persistence is set even if mapping already existed
      Goto set_persistence
    ${EndIf}
  ${EndIf}

  ; ------------------------------------------------------------
  ; W: exists but is wrong → remove it safely according to its kind
  ; ------------------------------------------------------------
  ${If} "$5" == "SUBST"
    !insertmacro LogMessage "Removing existing SUBST mapping on W: ..."
    nsExec::ExecToStack 'subst W: /D'
    Pop $X
    Pop $Y
    !insertmacro LogVar "SUBST delete exit" $X
    !insertmacro LogVar "SUBST delete out" $Y
  ${ElseIf} "$5" == "NETWORK"
    !insertmacro LogMessage "Removing existing NETWORK mapping on W: ..."
    nsExec::ExecToStack 'net use W: /delete /y'
    Pop $X
    Pop $Y
    !insertmacro LogVar "NET USE delete exit" $X
    !insertmacro LogVar "NET USE delete out" $Y
  ${ElseIf} "$5" == "PHYSICAL"
    !insertmacro LogError "W: is a physical/real volume. Refusing to remap. Please choose another drive letter."
    Return
  ${ElseIf} "$5" == "UNKNOWN"
    !insertmacro LogMessage "W: mapping type unknown. Attempting to delete via SUBST, then NET USE."
    nsExec::ExecToStack 'subst W: /D'
    Pop $X
    Pop $Y
    !insertmacro LogVar "SUBST delete (unknown) exit" $X
    !insertmacro LogVar "SUBST delete (unknown) out" $Y
    nsExec::ExecToStack 'net use W: /delete /y'
    Pop $X
    Pop $Y
    !insertmacro LogVar "NET USE delete (unknown) exit" $X
    !insertmacro LogVar "NET USE delete (unknown) out" $Y
  ${Else}
    !insertmacro LogMessage "W: not present. Proceeding to create mapping."
  ${EndIf}

  ; ------------------------------------------------------------
  ; Create SUBST W: -> $UserLocalAppDataDir
  ; ------------------------------------------------------------
  !insertmacro LogMessage "Creating SUBST mapping: W: -> $UserLocalAppDataDir"
  nsExec::ExecToStack 'subst W: "$UserLocalAppDataDir"'
  Pop $X
  Pop $Y
  !insertmacro LogVar "SUBST create exit" $X
  !insertmacro LogVar "SUBST create out" $Y

  ${If} $X != 0
    !insertmacro LogError "Failed to create SUBST mapping for W:. Installation succeeded, but drive mapping failed."
    Return
  ${EndIf}

  !insertmacro LogMessage "Drive mapping created: W:\ now points to $UserLocalAppDataDir"

set_persistence:
  ; ------------------------------------------------------------
  ; PERSISTENCE: Recreate mapping at each user logon via HKCU\...\Run
  ; Use REG_EXPAND_SZ so %LOCALAPPDATA% expands at logon for this user
  ; ------------------------------------------------------------
  !insertmacro LogMessage "Configuring persistent remap at logon (HKCU\\...\\Run)..."
  WriteRegExpandStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassis_MapW" '"%COMSPEC%" /c subst W: "%LOCALAPPDATA%"'
  ${If} ${Errors}
    !insertmacro LogError "Failed to write HKCU Run persistence entry."
  ${Else}
    !insertmacro LogMessage "Persistence entry created: HKCU\\...\\Run\\FChassis_MapW"
  ${EndIf}

  ; Optional: Verify what we wrote
  ReadRegStr $Z HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassis_MapW"
  !insertmacro LogVar "Run value(FChassis_MapW)" $Z

  !insertmacro LogMessage "W: mapping persistence is configured."

FunctionEnd



;--------------------------------
; Version comparison function
;--------------------------------
Function CompareVersions
  !insertmacro LogMessage "Starting CompareVersions function"
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

;--------------------------------
; Installation detection
;--------------------------------
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
  !insertmacro LogMessage "Previous version of FChassis is at ${UNINSTALL_KEY}"
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
  IfErrors check_install_flag
  !insertmacro LogMessage "Found uninstall registry key"
  !insertmacro LogVar "UninstallString" $0
  
  ; Get existing installation directory
  ReadRegStr $ExistingInstallDir HKLM "${UNINSTALL_KEY}" "InstallLocation"
  StrCmp $ExistingInstallDir "" 0 get_version
  StrCpy $ExistingInstallDir "${INSTALLDIR"
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

;--------------------------------
; Function to extract thirdParty.zip with progress feedback
;--------------------------------
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

;--------------------------------
; Function to handle cancellation during installation
;--------------------------------
Function OnInstFilesAbort
  !insertmacro LogMessage "=== Installation abort requested ==="
  ${If} ${Cmd} `MessageBox MB_YESNO|MB_ICONQUESTION "Are you sure you want to cancel the installation?" IDYES`
    !insertmacro LogMessage "User confirmed cancellation, performing cleanup"
    ; User confirmed cancellation, perform cleanup
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
  ${Else}
    !insertmacro LogMessage "User canceled the cancellation"
  ${EndIf}
FunctionEnd

;--------------------------------
; Installation Section
;--------------------------------
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

;--------------------------------
; Uninstaller Section
;--------------------------------
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