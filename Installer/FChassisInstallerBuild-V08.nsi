!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinMessages.nsh"
!include "MUI2.nsh"
!include "nsDialogs.nsh"

; -----------------------------------------------------------------------------
; String funcs only when needed (fix for warning 6010)
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

!ifndef HWND_BROADCAST
  !define HWND_BROADCAST 0xffff
!endif
!ifndef WM_SETTINGCHANGE
  !define WM_SETTINGCHANGE 0x001A
!endif

Name "${APPNAME} ${VERSION}"
OutFile "FChassisInstaller.exe"
InstallDir "${INSTALLDIR}"
RequestExecutionLevel admin

;--------------------------------
; Variables
;--------------------------------
Var InstalledState ; 0=none, 1=same, 2=older, 3=newer
Var ExistingInstallDir
Var ExistingVersion
Var ExtractionCompleted
Var LogFileHandle
Var WantRepair ; 0=Abort, 1=Repair (from custom page)

;--------------------------------
; Pages
;--------------------------------
!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING

!define MUI_PAGE_CUSTOMFUNCTION_PRE DirectoryPre
!insertmacro MUI_PAGE_DIRECTORY

; Show a **custom page** only when same version is detected (REQ-2)
Page custom RepairAbortPre RepairAbortLeave

!define MUI_PAGE_CUSTOMFUNCTION_SHOW InstFilesShow
!define MUI_PAGE_CUSTOMFUNCTION_ABORT OnInstFilesAbort
!insertmacro MUI_PAGE_INSTFILES

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

;--------------------------------
; Logging
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
; Broadcast env change
;--------------------------------
!macro BroadcastEnvChange
  !insertmacro LogMessage "Broadcasting environment change..."
  System::Call 'USER32::SendMessageTimeout(p ${HWND_BROADCAST}, i ${WM_SETTINGCHANGE}, p 0, t "Environment", i 0, i 5000, *i .r0)'
  !insertmacro LogMessage "Environment change broadcast completed"
!macroend

;--------------------------------
; Delete installer EXE (REQ-1)
;--------------------------------
Function DeleteInstaller
  !insertmacro LogMessage "Deleting installer executable..."
  Sleep 800
  ExecWait '"cmd.exe" /C ping 127.0.0.1 -n 2 > nul & del /f /q "$EXEPATH"' ; delayed delete avoids lock
  !insertmacro LogMessage "Delete EXE command issued: $EXEPATH"
FunctionEnd

;--------------------------------
; Version compare (simple dotted ints)
;--------------------------------
Function CompareVersions
  Exch $0 ; v1
  Exch
  Exch $1 ; v2
  Push $2
  Push $3
  Push $4
  Push $5
  Push $6

  StrCpy $2 0
  StrCpy $3 0
  loop:
    StrCpy $4 $0 1 $2
    IntOp $2 $2 + 1
    StrCmp $4 "" p1done 0
    StrCmp $4 "." p1done 0
    Goto loop
  p1done:
    StrCpy $5 $0 $2
    IntOp $5 $5 - 1
    StrCpy $5 $0 $5 -$2
    IntOp $5 $5 + 1

    StrCpy $4 $1 1 $3
    IntOp $3 $3 + 1
    StrCmp $4 "" p2done 0
    StrCmp $4 "." p2done 0
    Goto p2done
  p2done:
    StrCpy $6 $1 $3
    IntOp $6 $6 - 1
    StrCpy $6 $1 $6 -$3
    IntOp $6 $6 + 1

    IntCmp $5 $6 equal v1gt v2gt
  v1gt:
    Pop $6
    Pop $5
    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Pop $0
    Push 1
    Return
  v2gt:
    Pop $6
    Pop $5
    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Pop $0
    Push -1
    Return
  equal:
    StrCmp $0 "" chk2 0
    StrCmp $1 "" v1longer 0
    Goto loop
  chk2:
    StrCmp $1 "" eq v2longer
  eq:
    Pop $6
    Pop $5
    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Pop $0
    Push 0
    Return
  v1longer:
    Pop $6
    Pop $5
    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Pop $0
    Push 1
    Return
  v2longer:
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

;--------------------------------
; Detect existing install
;--------------------------------
Function .onInit
  FileOpen $LogFileHandle "C:\FChassis_Install.log" w
  FileWrite $LogFileHandle "=== FChassis Installation Log ===$\r$\n"

  StrCpy $InstalledState 0
  StrCpy $ExistingInstallDir "${INSTALLDIR}"
  StrCpy $ExistingVersion ""
  StrCpy $ExtractionCompleted 0
  StrCpy $WantRepair 0

  !insertmacro LogMessage "Checking uninstall key..."
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
  IfErrors check_flag
  !insertmacro LogMessage "Uninstall key present"
  ReadRegStr $ExistingInstallDir HKLM "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $ExistingInstallDir == ""
    StrCpy $ExistingInstallDir "${INSTALLDIR}"
  ${EndIf}
  ReadRegStr $ExistingVersion HKLM "${UNINSTALL_KEY}" "DisplayVersion"

  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  ${If} $1 == 0
    StrCpy $InstalledState 1
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3
  ${Else}
    StrCpy $InstalledState 2
  ${EndIf}
  Goto done

check_flag:
  !insertmacro LogMessage "Uninstall key missing; checking custom flag..."
  ReadRegStr $0 HKLM "${INSTALL_FLAG_KEY}" "Installed"
  StrCmp $0 "1" 0 done
  ReadRegStr $ExistingVersion HKLM "${INSTALL_FLAG_KEY}" "Version"
  ReadRegStr $ExistingInstallDir HKLM "${INSTALL_FLAG_KEY}" "InstallPath"
  Push $ExistingVersion
  Push "${VERSION}"
  Call CompareVersions
  Pop $1
  ${If} $1 == 0
    StrCpy $InstalledState 1
  ${ElseIf} $1 == 1
    StrCpy $InstalledState 3
  ${Else}
    StrCpy $InstalledState 2
  ${EndIf}

done:
  StrCpy $INSTDIR $ExistingInstallDir
  !insertmacro LogVar "InstalledState" $InstalledState
  !insertmacro LogVar "INSTDIR" $INSTDIR
FunctionEnd

;--------------------------------
; Pre-Directory page (block downgrade) (REQ-1 for version conflict)
;--------------------------------
Function DirectoryPre
  ${If} $InstalledState == 3
    MessageBox MB_OK|MB_ICONEXCLAMATION \
      "A newer version ($ExistingVersion) is already installed.$\r$\nCannot install ${VERSION}. Please uninstall the newer version first.$\r$\nThe installer will now close."
    Call DeleteInstaller ; (REQ-1) delete self on conflict
    Abort
  ${EndIf}
FunctionEnd

;--------------------------------
; Custom page: Repair or Abort (REQ-2)
;--------------------------------
Function RepairAbortPre
  ${If} $InstalledState == 1
    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
      Abort
    ${EndIf}

    ${NSD_CreateLabel} 0 0 100% 28u "${APPNAME} ${ExistingVersion} is already installed in:$\r$\n$INSTDIR"
    Pop $1

    ${NSD_CreateLabel} 0 30u 100% 16u "Choose what to do:"
    Pop $2

    ${NSD_CreateButton} 0 52u 60u 14u "Repair"
    Pop $3
    ${NSD_OnClick} $3 RepairClicked

    ${NSD_CreateButton} 70u 52u 60u 14u "Abort"
    Pop $4
    ${NSD_OnClick} $4 AbortClicked

    nsDialogs::Show
  ${Else}
    Abort ; skip this page if not same-version case
  ${EndIf}
FunctionEnd

Function RepairClicked
  StrCpy $WantRepair 1
  SendMessage $HWNDPARENT 0x408 1 0 ; Next
FunctionEnd

Function AbortClicked
  StrCpy $WantRepair 0
  SendMessage $HWNDPARENT 0x408 0 0 ; Back -> we’ll cancel in Leave
FunctionEnd

Function RepairAbortLeave
  ${If} $InstalledState == 1
    ${If} $WantRepair == 0
      ; User chose Abort on the custom page (REQ-2)
      MessageBox MB_OK|MB_ICONINFORMATION "Installation aborted."
      Call DeleteInstaller ; (REQ-1)
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

;--------------------------------
; InstFiles page show
;--------------------------------
Function InstFilesShow
  !insertmacro LogMessage "=== InstFiles page shown ==="
FunctionEnd

;--------------------------------
; Cancel on InstFiles -> full rollback (REQ-3)
;--------------------------------
Function OnInstFilesAbort
  !insertmacro LogMessage "=== Installation abort requested ==="
  MessageBox MB_YESNO|MB_ICONQUESTION "Abort installation and rollback all changes?" IDYES doRollback IDNO noRollback
  noRollback:
    Abort
  doRollback:
    Call RollbackCleanup
    MessageBox MB_OK|MB_ICONINFORMATION "Installation canceled and rolled back."
    Call DeleteInstaller ; (REQ-1)
    Abort
FunctionEnd

;--------------------------------
; Shared rollback cleanup (used by user abort and failures) (REQ-3)
;--------------------------------
Function RollbackCleanup
  !insertmacro LogMessage "Rollback: removing files/dirs..."
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

  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\*.exe"
  Delete "$INSTDIR\*.json"
  Delete "$INSTDIR\*.xml"
  Delete "$INSTDIR\*.wad"
  Delete "$INSTDIR\*.pdb"
  Delete "$INSTDIR\*.exp"
  Delete "$INSTDIR\*.lib"
  Delete "$INSTDIR\thirdParty.zip"
  Delete "$INSTDIR\7z.exe"
  Delete "$INSTDIR\7z.dll"
  Delete "$INSTDIR\VC_redist.x64.exe"

  !insertmacro LogMessage "Rollback: removing shortcuts..."
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"

  !insertmacro LogMessage "Rollback: removing registry..."
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"

  !insertmacro LogMessage "Rollback: PATH/CASROOT..."
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
  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT"
  !insertmacro BroadcastEnvChange

  !insertmacro LogMessage "Rollback: removing virtual drive..."
  nsExec::Exec '"cmd.exe" /C subst W: /D'
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"

  RMDir "$INSTDIR"
FunctionEnd

;--------------------------------
; Fail/Abort handlers -> delete EXE too (REQ-1)
;--------------------------------
Function .onUserAbort
  !insertmacro LogMessage ".onUserAbort -> rollback + delete self"
  ; Ensure full rollback + delete installer:
  Call RollbackCleanup
  Call DeleteInstaller
FunctionEnd

Function .onInstFailed
  !insertmacro LogMessage ".onInstFailed -> rollback + delete self"
  Call RollbackCleanup
  Call DeleteInstaller
FunctionEnd

;--------------------------------
; Extract thirdParty.zip (unchanged logic)
;--------------------------------
Function ExtractThirdParty
  IfFileExists "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin\TKernel.dll" done
  DetailPrint "Extracting thirdParty.zip (may take several minutes)..."
  nsExec::ExecToStack '"$INSTDIR\7z.exe" x "$INSTDIR\thirdParty.zip" -o"$INSTDIR" -y'
  Pop $0
  Pop $1
  ${If} $0 != 0
    !insertmacro LogError "7z failed: $0"
    MessageBox MB_OK|MB_ICONEXCLAMATION "Failed to extract third-party components."
  ${Else}
    StrCpy $ExtractionCompleted 1
  ${EndIf}
done:
FunctionEnd

;--------------------------------
; INSTALL
;--------------------------------
Section "Install"
  SetAutoClose false
  !insertmacro LogVar "InstalledState at section start" $InstalledState

  ${Switch} $InstalledState
    ${Case} 1
      ; We already got explicit user choice on the custom page (REQ-2)
      ${If} $WantRepair != 1
        Abort
      ${EndIf}
    ${Case} 2
      MessageBox MB_YESNO|MB_ICONQUESTION \
        "An older version ($ExistingVersion) is installed. Upgrade to ${VERSION}?" \
        IDYES +2 IDNO cancelHere
      Goto proceed
      cancelHere:
        Call DeleteInstaller
        Abort
    ${Case} 3
      ; Should have been blocked in DirectoryPre
      MessageBox MB_OK|MB_ICONEXCLAMATION "Cannot downgrade. Aborting."
      Call DeleteInstaller
      Abort
  ${EndSwitch}

proceed:
  SetShellVarContext all
  SetOutPath "$INSTDIR"

  ${If} $InstalledState >= 1
    ; Clean out application dirs before repair/upgrade
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
    Delete "$INSTDIR\*.dll"
    Delete "$INSTDIR\*.exe"
    Delete "$INSTDIR\*.json"
    Delete "$INSTDIR\*.xml"
    Delete "$INSTDIR\*.wad"
    Delete "$INSTDIR\*.pdb"
    Delete "$INSTDIR\*.exp"
    Delete "$INSTDIR\*.lib"
  ${EndIf}

  DetailPrint "Copying application files..."
  File /r "${PayloadDir}\*.*"

  DetailPrint "Creating shortcuts..."
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe"

  ${If} $InstalledState != 1
    IfFileExists "${ThirdPartyDir}\thirdParty.zip" 0 +2
      File "${ThirdPartyDir}\thirdParty.zip"
    IfFileExists "${ThirdPartyDir}\7z.exe" 0 +2
      File "${ThirdPartyDir}\7z.exe"
    IfFileExists "${ThirdPartyDir}\7z.dll" 0 +2
      File "${ThirdPartyDir}\7z.dll"
    IfFileExists "${ThirdPartyDir}\VC_redist.x64.exe" 0 +2
      File "${ThirdPartyDir}\VC_redist.x64.exe"

    IfFileExists "$INSTDIR\thirdParty.zip" 0 +3
      IfFileExists "$INSTDIR\7z.exe" 0 +2
        Call ExtractThirdParty
  ${EndIf}

  ; Registry: flag + uninstall info
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Installed" "1"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "Version" "${VERSION}"
  WriteRegStr HKLM "${INSTALL_FLAG_KEY}" "InstallPath" "$INSTDIR"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${COMPANY}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\FChassis_Splash.ico"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 0
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "EstimatedSize" "$0"

  ; PATH + CASROOT
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
  WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0"
  !insertmacro BroadcastEnvChange

  ; Virtual drive mapping
  nsExec::Exec '"cmd.exe" /C subst W: "$INSTDIR\Map"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive" '"cmd.exe" /C subst W: "$INSTDIR\Map"'

  ${If} $InstalledState != 1
    Delete "$INSTDIR\thirdParty.zip"
    Delete "$INSTDIR\VC_redist.x64.exe"
    Delete "$INSTDIR\7z.exe"
    Delete "$INSTDIR\7z.dll"
  ${EndIf}

  DetailPrint "Installation completed successfully!"
  FileClose $LogFileHandle
SectionEnd

;--------------------------------
; UNINSTALL
;--------------------------------
Section "Uninstall"
  FileOpen $LogFileHandle "C:\FChassis_Install.log" a
  FileSeek $LogFileHandle 0 END
  FileWrite $LogFileHandle "=== Uninstallation started ===$\r$\n"

  SetShellVarContext all
  RMDir /r "$INSTDIR"
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"

  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "${INSTALL_FLAG_KEY}"

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

  DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT"
  !insertmacro BroadcastEnvChange

  nsExec::Exec '"cmd.exe" /C subst W: /D'
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"

  FileClose $LogFileHandle
SectionEnd
