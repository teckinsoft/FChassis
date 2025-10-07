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
Var ThirdPartyDir ; New variable for parallel thirdparty directory
Var ParentDir ; Parent directory of installation

;--------------------------------
; Pages
;--------------------------------
!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING

!define MUI_PAGE_CUSTOMFUNCTION_PRE DirectoryPre
!insertmacro MUI_PAGE_DIRECTORY

!define MUI_PAGE_CUSTOMFUNCTION_ABORT OnInstFilesAbort
!insertmacro MUI_PAGE_INSTFILES

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Helper: broadcast env change
;--------------------------------
!macro BroadcastEnvChange
  System::Call 'USER32::SendMessageTimeout(p ${HWND_BROADCAST}, i ${WM_SETTINGCHANGE}, p 0, t "Environment", i 0, i 5000, *i .r0)'
!macroend

;--------------------------------
; Version comparison function
;--------------------------------
Function CompareVersions
  Exch $0 ; version 1
  Exch
  Exch $1 ; version 2
  Push $2
  Push $3
  Push $4
  Push $5
  Push $6

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
  StrCpy $InstalledState 0 ; Default to not installed
  StrCpy $ExistingInstallDir "${INSTALLDIR}"
  StrCpy $ExistingVersion ""
  StrCpy $ExtractionCompleted 0
  
  ; Get parent directory for parallel thirdparty installation
  ${GetParent} "$INSTDIR" $ParentDir
  StrCpy $ThirdPartyDir "$ParentDir\thirdParty"

  ; Check if already installed via uninstall registry
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
  IfErrors check_install_flag
  
  ; Get existing installation directory
  ReadRegStr $ExistingInstallDir HKLM "${UNINSTALL_KEY}" "InstallLocation"
  StrCmp $ExistingInstallDir "" 0 get_version
  StrCpy $ExistingInstallDir "${INSTALLDIR}"
  
  get_version:
  ; Get installed version
  ReadRegStr $ExistingVersion HKLM "${UNINSTALL_KEY}" "DisplayVersion"
  IfErrors check_install_flag
  
  ; Compare versions
  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  
  ${If} $1 == 0
    StrCpy $InstalledState 1 ; Same version installed
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3 ; Newer version installed
  ${Else}
    StrCpy $InstalledState 2 ; Older version installed
  ${EndIf}
  
  Goto done
  
  check_install_flag:
  ; Check our custom install flag
  ReadRegStr $0 HKLM "${INSTALL_FLAG_KEY}" "Installed"
  StrCmp $0 "1" 0 done
  ReadRegStr $ExistingVersion HKLM "${INSTALL_FLAG_KEY}" "Version"
  ReadRegStr $ExistingInstallDir HKLM "${INSTALL_FLAG_KEY}" "InstallPath"
  
  ; Compare versions if we found version info
  StrCmp $ExistingVersion "" done 0
  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  
  ${If} $1 == 0
    StrCpy $InstalledState 1 ; Same version installed
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3 ; Newer version installed
  ${Else}
    StrCpy $InstalledState 2 ; Older version installed
  ${EndIf}
  
  done:
  ; Set install directory to existing installation path
  StrCpy $INSTDIR $ExistingInstallDir
FunctionEnd

Function DirectoryPre
  ${If} $InstalledState == 3
    MessageBox MB_OK|MB_ICONEXCLAMATION "A newer version ($ExistingVersion) of ${APPNAME} is already installed.$\nCannot downgrade to version ${VERSION}.$\n$\nPlease uninstall the newer version first."
    Abort
  ${EndIf}
FunctionEnd

;--------------------------------
; Multithreaded extraction function
;--------------------------------
Function ExtractThirdPartyMultithreaded
  ; Check if extraction is already done in parallel directory
  IfFileExists "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin\TKernel.dll" extraction_complete
  
  DetailPrint "Extracting thirdParty.zip using multithreaded extraction..."
  DetailPrint "This may take several minutes (90,000+ files)..."
  
  ; Show progress message
  SetDetailsPrint listonly
  DetailPrint "Extracting: Please wait patiently..."
  SetDetailsPrint both
  
  ; Use 7z with multithreading (-mmt=on) for faster extraction
  nsExec::ExecToStack '"$INSTDIR\7z.exe" x "$INSTDIR\thirdParty.zip" -o"$ThirdPartyDir" -mmt=on -y'
  Pop $0 ; Exit code
  Pop $1 ; Output
  
  ${If} $0 != 0
    DetailPrint "7-Zip extraction failed with code: $0"
    DetailPrint "Output: $1"
    MessageBox MB_OK|MB_ICONEXCLAMATION "Failed to extract third-party components. Installation may be incomplete."
  ${Else}
    StrCpy $ExtractionCompleted 1
    DetailPrint "Third-party components extraction completed successfully."
  ${EndIf}
  
  extraction_complete:
FunctionEnd

;--------------------------------
; Function to copy thirdparty files in parallel
;--------------------------------
Function CopyThirdPartyFiles
  ; Create a separate thread for file copying to improve performance
  DetailPrint "Starting parallel file copy operation..."
  
  ; Use robocopy for multithreaded copying (faster than NSIS built-in)
  nsExec::ExecToStack 'robocopy "$ThirdPartyDir" "$INSTDIR\thirdParty" /E /MT:4 /NP /NFL /NDL'
  Pop $0
  Pop $1
  
  ; Robocopy returns 0-7 for success, 8+ for errors
  ${If} $0 >= 8
    DetailPrint "Robocopy encountered errors (return code: $0)"
    DetailPrint "Falling back to standard copy method..."
    
    ; Fallback to standard copy if robocopy fails
    CopyFiles /SILENT "$ThirdPartyDir\*.*" "$INSTDIR\thirdParty"
  ${EndIf}
  
  DetailPrint "Third-party files copy completed."
FunctionEnd

;--------------------------------
; Function to handle cancellation during installation
;--------------------------------
Function OnInstFilesAbort
  ${If} ${Cmd} `MessageBox MB_YESNO|MB_ICONQUESTION "Are you sure you want to cancel the installation?" IDYES`
    ; User confirmed cancellation, perform cleanup
    MessageBox MB_OK|MB_ICONINFORMATION "Installation canceled. Cleaning up..."
    
    ; Remove installed files and directories
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
    
    ; Remove thirdparty directories (both parallel and in installation)
    RMDir /r "$ThirdPartyDir"
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
    
    ; Remove shortcuts if they were created
    SetShellVarContext all
    Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
    RMDir "$SMPROGRAMS\${APPNAME}"
    Delete "$DESKTOP\${APPNAME}.lnk"
    
    ; Remove registry entries if they were created
    DeleteRegKey HKLM "${UNINSTALL_KEY}"
    DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"
    
    ; Remove environment variables if they were added
    EnVar::SetHKLM
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
    EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
    
    ; Remove CASROOT environment variable
    DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT"
    !insertmacro BroadcastEnvChange
    
    ; Remove virtual drive mapping
    nsExec::Exec '"cmd.exe" /C subst W: /D'
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"
    
    ; Remove the installation directory if it's empty
    RMDir "$INSTDIR"
    
    MessageBox MB_OK|MB_ICONINFORMATION "Installation has been completely canceled and all changes have been reverted."
  ${Else}
    ; User changed their mind, continue installation
    Abort
  ${EndIf}
FunctionEnd

;--------------------------------
; Section: Install
;--------------------------------
Section "Install"
  SetAutoClose false ; Prevent installer from closing automatically
  
  ; Handle existing installation scenarios
  ${Switch} $InstalledState
    ${Case} 1 ; Same version installed
      MessageBox MB_YESNO|MB_ICONQUESTION \
        "${APPNAME} version $ExistingVersion is already installed.$\n$\nDo you want to repair the installation?" \
        /SD IDYES \
        IDYES proceedWithInstall
        Abort
      ${Break}
    
    ${Case} 2 ; Older version installed
      MessageBox MB_YESNO|MB_ICONQUESTION \
        "An older version ($ExistingVersion) of ${APPNAME} is already installed.$\n$\nDo you want to upgrade to version ${VERSION}?" \
        /SD IDYES \
        IDYES proceedWithInstall
        Abort
      ${Break}
    
    ${Case} 3 ; Newer version installed (should not reach here due to DirectoryPre check)
      MessageBox MB_OK|MB_ICONEXCLAMATION "Cannot install older version. Installation aborted."
      Abort
      ${Break}
  ${EndSwitch}

  proceedWithInstall:
  ; Ensure shortcuts apply to all users
  SetShellVarContext all
  
  ; Set install dir (use existing installation path)
  SetOutPath "$INSTDIR"
  
  ; Remove existing files before copying new ones (clean upgrade)
  ${If} $InstalledState >= 1
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
  ${EndIf}
  
  ; Copy all files from build output (this should include FChassis_Splash.ico)
  DetailPrint "Copying application files..."
  File /r "${PayloadDir}\*.*"

  ; --- Shortcuts ---
  DetailPrint "Creating shortcuts..."
  ; Start Menu (All Users)
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe" "" "$INSTDIR\FChassis_Splash.ico" 0

  ; Desktop (All Users)
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe" "" "$INSTDIR\FChassis_Splash.ico" 0

  ; Include the pre-packaged files (only if they exist and we're not repairing)
  ${If} $InstalledState != 1
    IfFileExists "${ThirdPartyDir}\thirdParty.zip" 0 +2
      File "${ThirdPartyDir}\thirdParty.zip"
    
    IfFileExists "${ThirdPartyDir}\7z.exe" 0 +2
      File "${ThirdPartyDir}\7z.exe"
    
    IfFileExists "${ThirdPartyDir}\7z.dll" 0 +2
      File "${ThirdPartyDir}\7z.dll"
    
    IfFileExists "${ThirdPartyDir}\VC_redist.x64.exe" 0 +2
      File "${ThirdPartyDir}\VC_redist.x64.exe"

    ; Extract using multithreaded 7z if the zip file exists
    IfFileExists "$INSTDIR\thirdParty.zip" 0 skip_extraction
      IfFileExists "$INSTDIR\7z.exe" 0 skip_extraction
        Call ExtractThirdPartyMultithreaded
        Call CopyThirdPartyFiles
  ${EndIf}
  
  skip_extraction:
  ; Write installation information to registry
  DetailPrint "Writing installation information to registry..."
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Installed" "1"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Version" "${VERSION}"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "InstallPath" "$INSTDIR"
  
  ; Write uninstall info with proper registry entries
  DetailPrint "Writing uninstall info with proper registry entries..."
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

  ; === Add third-party bin folders to PATH ===
  DetailPrint "Updating system PATH..."
  EnVar::SetHKLM
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
  EnVar::AddValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
  
  ; Set CASROOT environment variable
  WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0"
  !insertmacro BroadcastEnvChange

  ; === Install VC++ Redistributable (x64) silently ===
  ${If} $InstalledState != 1 ; Don't reinstall VC++ redist during repair
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" "Installed"
    StrCmp $0 1 vcskip
      IfFileExists "$INSTDIR\VC_redist.x64.exe" 0 vcskip
        DetailPrint "Installing Visual C++ Redistributable..."
        ExecWait '"$INSTDIR\VC_redist.x64.exe" /install /quiet /norestart' $0
        ${If} $0 != 0
          DetailPrint "VC++ Redist installation returned code: $0"
        ${EndIf}
    vcskip:
  ${EndIf}
  
  ; === Map W: drive persistently to thirdParty folder ===
  DetailPrint "Setting up virtual drive mapping..."
  nsExec::Exec '"cmd.exe" /C subst W: "$INSTDIR\Map"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive" '"cmd.exe" /C subst W: "$INSTDIR\Map"'

  ; Clean up after extraction (only during fresh install or upgrade)
  ${If} $InstalledState != 1
    Delete "$INSTDIR\thirdParty.zip"
    Delete "$INSTDIR\VC_redist.x64.exe"
    Delete "$INSTDIR\7z.exe"
    Delete "$INSTDIR\7z.dll"
  ${EndIf}
  
  DetailPrint "Installation completed successfully!"
SectionEnd

;--------------------------------
; Section: Uninstall
;--------------------------------
Section "Uninstall"
  ; Ensure we remove from all users
  SetShellVarContext all

  ; Get parent directory for parallel thirdparty directory
  ${GetParent} "$INSTDIR" $ParentDir
  StrCpy $ThirdPartyDir "$ParentDir\thirdParty"

  ; Remove installed files
  RMDir /r "$INSTDIR"

  ; Remove parallel thirdparty directory (optional - comment out if you want to keep it)
  RMDir /r "$ThirdPartyDir"

  ; Remove Start Menu shortcut
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Remove Desktop shortcut
  Delete "$DESKTOP\${APPNAME}.lnk"

  ; Remove registry entries
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"
  
  ; === Remove from PATH ===
  EnVar::SetHKLM
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
  EnVar::DeleteValue "Path" "$ThirdPartyDir\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
  
  ; Remove CASROOT environment variable
  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT"
  !insertmacro BroadcastEnvChange

  ; Remove the virtual drive during uninstallation
  nsExec::Exec '"cmd.exe" /C subst W: /D'
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"
SectionEnd