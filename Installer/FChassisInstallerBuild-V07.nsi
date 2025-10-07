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

;--------------------------------
; Build-time defines
;--------------------------------
!ifndef VERSION
  !define VERSION "1.0.4"
!endif

!ifndef INSTALLDIR
  !define INSTALLDIR "C:\FChassis"
!endif

!ifndef PayloadDir
  !define PayloadDir "C:\FluxSDK\Bin"
!endif

!ifndef ThirdPartyDir
  !define ThirdPartyDir "C:\FluxSDK"
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
  !insertmacro LogMessage "Previous version of FChassis uninstall string: $0"
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

; Function DirectoryPre
  ; !insertmacro LogMessage "=== Starting DirectoryPre function ==="
  ; ${If} $InstalledState == 3
    ; !insertmacro LogMessage "Newer version detected, showing warning message"
    ; MessageBox MB_OK|MB_ICONEXCLAMATION "A newer version ($ExistingVersion) of ${APPNAME} is already installed.$\nCannot downgrade to version ${VERSION}.$\n$\nPlease uninstall the newer version first."
    ; Abort
  ; ${EndIf}
  ; !insertmacro LogMessage "=== DirectoryPre function completed ==="
; FunctionEnd

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
  IfFileExists "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin\TKernel.dll" extraction_complete
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
    EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
    EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
    EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
    EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
    EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
    EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
    EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
    
    ; Remove CASROOT environment variable
    DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT"
    !insertmacro BroadcastEnvChange
    
    ; Remove virtual drive mapping
    !insertmacro LogMessage "Removing virtual drive mapping..."
    nsExec::Exec '"cmd.exe" /C subst W: /D'
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"
    
    ; Remove the installation directory if it's empty
    !insertmacro LogMessage "Removing installation directory..."
    RMDir "$INSTDIR"
    
    MessageBox MB_OK|MB_ICONINFORMATION "Installation has been completely canceled and all changes have been reverted."
    !insertmacro LogMessage "Installation cancellation completed"
  ${Else}
    !insertmacro LogMessage "User changed mind, continuing installation"
    ; User changed their mind, continue installation
    Abort
  ${EndIf}
  !insertmacro LogMessage "=== OnInstFilesAbort function completed ==="
FunctionEnd

;--------------------------------
; Section: Install
;--------------------------------
Section "Install"
  !insertmacro LogMessage "=== Starting Install section ==="
  SetAutoClose false ; Prevent installer from closing automatically
  
  ; Handle existing installation scenarios
  !insertmacro LogVar "InstalledState at section start" $InstalledState
  ${Switch} $InstalledState
    ${Case} 1 ; Same version installed
      !insertmacro LogMessage "Same version installed, asking for repair"
      MessageBox MB_YESNO|MB_ICONQUESTION \
        "${APPNAME} version $ExistingVersion is already installed.$\n$\nDo you want to repair the installation?" \
        /SD IDYES \
        IDYES proceedWithInstall
        !insertmacro LogMessage "User chose not to repair, aborting"
        Abort
      ${Break}
    
    ${Case} 2 ; Older version installed
      !insertmacro LogMessage "Older version installed, asking for upgrade"
      MessageBox MB_YESNO|MB_ICONQUESTION \
        "An older version ($ExistingVersion) of ${APPNAME} is already installed.$\n$\nDo you want to upgrade to version ${VERSION}?" \
        /SD IDYES \
        IDYES proceedWithInstall
        !insertmacro LogMessage "User chose not to upgrade, aborting"
        Abort
      ${Break}
    
    ${Case} 3 ; Newer version installed (should not reach here due to DirectoryPre check)
      !insertmacro LogMessage "Newer version installed, aborting"
      MessageBox MB_OK|MB_ICONEXCLAMATION "Cannot install older version. Installation aborted."
      Abort
      ${Break}
  ${EndSwitch}

  proceedWithInstall:
  !insertmacro LogMessage "Proceeding with installation"
  ; Ensure shortcuts apply to all users
  SetShellVarContext all
  !insertmacro LogMessage "SetShellVarContext: all"
  
  ; Set install dir (use existing installation path)
  SetOutPath "$INSTDIR"
  !insertmacro LogVar "SetOutPath to" $INSTDIR
  
  ; Remove existing files before copying new ones (clean upgrade)
  ${If} $InstalledState >= 1
    !insertmacro LogMessage "Removing existing files for upgrade/repair"
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
    
    ; Keep thirdParty directory if it exists (to avoid re-downloading large files)
    ; But remove specific files we want to update
    Delete "$INSTDIR\*.dll"
    Delete "$INSTDIR\*.exe"
    Delete "$INSTDIR\*.json"
    Delete "$INSTDIR\*.xml"
    Delete "$INSTDIR\*.wad"
    Delete "$INSTDIR\*.pdb"
    Delete "$INSTDIR\*.exp"
    Delete "$INSTDIR\*.lib"
    !insertmacro LogMessage "Existing files removed for upgrade"
  ${EndIf}
  
  ; Copy all files from build output (this should include FChassis_Splash.ico)
  !insertmacro LogMessage "Copying application files from ${PayloadDir}"
  DetailPrint "Copying application files..."
  File /r "${PayloadDir}\*.*"
  !insertmacro LogMessage "Application files copied"

  ; --- Shortcuts ---
  !insertmacro LogMessage "Creating shortcuts..."
  DetailPrint "Creating shortcuts..."
  ; Start Menu (All Users)
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe" "" "$INSTDIR\FChassis_Splash.ico" 0
  !insertmacro LogMessage "Start menu shortcut created"

  ; Desktop (All Users)
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe" "" "$INSTDIR\FChassis_Splash.ico" 0
  !insertmacro LogMessage "Desktop shortcut created"

  ; Include the pre-packaged files (only if they exist and we're not repairing)
  ${If} $InstalledState != 1
    !insertmacro LogMessage "Checking for pre-packaged files..."
    IfFileExists "${ThirdPartyDir}\thirdParty.zip" 0 +2
      File "${ThirdPartyDir}\thirdParty.zip"
      !insertmacro LogMessage "thirdParty.zip copied"
    
    IfFileExists "${ThirdPartyDir}\7z.exe" 0 +2
      File "${ThirdPartyDir}\7z.exe"
      !insertmacro LogMessage "7z.exe copied"
    
    IfFileExists "${ThirdPartyDir}\7z.dll" 0 +2
      File "${ThirdPartyDir}\7z.dll"
      !insertmacro LogMessage "7z.dll copied"
    
    IfFileExists "${ThirdPartyDir}\VC_redist.x64.exe" 0 +2
      File "${ThirdPartyDir}\VC_redist.x64.exe"
      !insertmacro LogMessage "VC_redist.x64.exe copied"

    ; Extract using 7z.exe if the zip file exists
    IfFileExists "$INSTDIR\thirdParty.zip" 0 skip_extraction
      IfFileExists "$INSTDIR\7z.exe" 0 skip_extraction
        !insertmacro LogMessage "Calling ExtractThirdParty function"
        Call ExtractThirdParty
  ${EndIf}
  
  skip_extraction:
  ; Write installation information to registry
  !insertmacro LogMessage "Writing installation information to registry"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Installed" "1"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Version" "${VERSION}"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "InstallPath" "$INSTDIR"
  !insertmacro LogMessage "Registry entries written to INSTALL_FLAG_KEY"
  
  ; Write uninstall info with proper registry entries
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${COMPANY}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\FChassis_Splash.ico"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 0
  WriteRegStr HKLM "${UNINSTALL_KEY}" "URLInfoAbout" "https://www.teckinsoft.in/"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "HelpLink" "https://www.teckinsoft.in/support"
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "EstimatedSize" "$0"
  !insertmacro LogMessage "Uninstall registry entries written"

  ; === Add third-party bin folders to PATH ===
  !insertmacro LogMessage "Updating system PATH environment variable"
  DetailPrint "Updating system PATH..."
  EnVar::SetHKLM
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
  EnVar::AddValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
  !insertmacro LogMessage "PATH environment variable updated"

  ; Set CASROOT environment variable
  WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0"
  !insertmacro BroadcastEnvChange
  !insertmacro LogMessage "CASROOT environment variable set"

  ; === Install VC++ Redistributable (x64) silently ===
  ${If} $InstalledState != 1 ; Don't reinstall VC++ redist during repair
    !insertmacro LogMessage "Checking if VC++ Redistributable needs installation"
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" "Installed"
    StrCmp $0 1 vcskip
      IfFileExists "$INSTDIR\VC_redist.x64.exe" 0 vcskip
        DetailPrint "Installing Visual C++ Redistributable..."
        !insertmacro LogMessage "Installing VC++ Redistributable..."
        ExecWait '"$INSTDIR\VC_redist.x64.exe" /install /quiet /norestart' $0
        ${If} $0 != 0
          DetailPrint "VC++ Redist installation returned code: $0"
          !insertmacro LogError "VC++ Redist installation failed with code: $0"
        ${Else}
          !insertmacro LogMessage "VC++ Redistributable installed successfully"
        ${EndIf}
    vcskip:
  ${EndIf}
  
  ; === Map W: drive persistently to thirdParty folder ===
  !insertmacro LogMessage "Setting up virtual drive mapping W: to $INSTDIR\Map"
  DetailPrint "Setting up virtual drive mapping..."
  nsExec::Exec '"cmd.exe" /C subst W: "$INSTDIR\Map"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive" '"cmd.exe" /C subst W: "$INSTDIR\Map"'
  !insertmacro LogMessage "Virtual drive mapping configured"

  ; Clean up after extraction (only during fresh install or upgrade)
  ${If} $InstalledState != 1
    !insertmacro LogMessage "Cleaning up temporary files..."
    Delete "$INSTDIR\thirdParty.zip"
    Delete "$INSTDIR\VC_redist.x64.exe"
    Delete "$INSTDIR\7z.exe"
    Delete "$INSTDIR\7z.dll"
    !insertmacro LogMessage "Temporary files cleaned up"
  ${EndIf}
  
  DetailPrint "Installation completed successfully!"
  !insertmacro LogMessage "=== Installation completed successfully ==="
  !insertmacro LogMessage "Installation directory: $INSTDIR"
  
  ; Close log file
  FileClose $LogFileHandle
SectionEnd

;--------------------------------
; Section: Uninstall
;--------------------------------
Section "Uninstall"
  !insertmacro LogMessage "=== Starting Uninstall section ==="
  
  ; Open log file for uninstall
  FileOpen $LogFileHandle "C:\FChassis_Install.log" a
  FileSeek $LogFileHandle 0 END
  FileWrite $LogFileHandle "=== Uninstallation started ===$\r$\n"
  
  ; Ensure we remove from all users
  SetShellVarContext all
  !insertmacro LogMessage "SetShellVarContext: all"

  ; Remove installed files
  !insertmacro LogMessage "Removing installed files from $INSTDIR"
  RMDir /r "$INSTDIR"

  ; Remove Start Menu shortcut
  !insertmacro LogMessage "Removing Start Menu shortcut"
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Remove Desktop shortcut
  !insertmacro LogMessage "Removing Desktop shortcut"
  Delete "$DESKTOP\${APPNAME}.lnk"

  ; Remove registry entries
  !insertmacro LogMessage "Removing registry entries"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"
  
  ; === Remove from PATH ===
  !insertmacro LogMessage "Removing from PATH environment variable"
  EnVar::SetHKLM
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
  EnVar::DeleteValue "Path" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
  
  ; Remove CASROOT environment variable
  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT"
  !insertmacro BroadcastEnvChange
  !insertmacro LogMessage "CASROOT environment variable removed"

  ; Remove the virtual drive during uninstallation
  !insertmacro LogMessage "Removing virtual drive mapping"
  nsExec::Exec '"cmd.exe" /C subst W: /D'
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"
  
  !insertmacro LogMessage "=== Uninstallation completed successfully ==="
  
  ; Close log file
  FileClose $LogFileHandle
SectionEnd