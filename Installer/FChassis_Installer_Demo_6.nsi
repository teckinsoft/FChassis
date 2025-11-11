;--------------------------------
; General Settings
;--------------------------------
!define APPNAME "FChassis"
!define COMPANY "Haleon"
!define VERSION "1.0.0"
!define INSTALLDIR "C:\FChassis"

Name "${APPNAME} ${VERSION}"
OutFile "FChassisInstaller.exe"
InstallDir "${INSTALLDIR}"
RequestExecutionLevel admin

; Includes
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

;--------------------------------
; Pages
;--------------------------------
Page directory

; Use PageEx to hook a Show callback and re-enable Cancel on InstFiles
PageEx instfiles
  PageCallbacks "" InstFilesShow ""
PageExEnd

; Do the same for the uninstaller InstFiles page
PageEx un.instfiles
  PageCallbacks "" un.InstFilesShow ""
PageExEnd

;--------------------------------
; Callbacks
;--------------------------------

; Enable Cancel button on InstFiles page (default is disabled during install progress)
Function InstFilesShow
  GetDlgItem $0 $HWNDPARENT 2   ; 2 = IDCANCEL
  EnableWindow $0 1
FunctionEnd

; Enable Cancel button on Uninstall InstFiles page as well
Function un.InstFilesShow
  GetDlgItem $0 $HWNDPARENT 2
  EnableWindow $0 1
FunctionEnd

; Ask for confirmation when Cancel is clicked during install
; Returning (no Abort) exits; Abort continues the install.
Function .onUserAbort
  MessageBox MB_ICONQUESTION|MB_YESNO "Cancel installation of ${APPNAME}?" IDYES +2
  Abort
FunctionEnd

; Ask for confirmation when Cancel is clicked during uninstall
Function un.onUserAbort
  MessageBox MB_ICONQUESTION|MB_YESNO "Cancel uninstallation of ${APPNAME}?" IDYES +2
  Abort
FunctionEnd

;--------------------------------
; Section: Install
;--------------------------------
Section "Install"
  SetOutPath "$INSTDIR"

  ; Copy all files from build output
  ; Note: Cancel is handled between operations; very long single operations may not interrupt instantly.
  File /r "C:\D drive\Projects\Fchassis\Main FChassis\FChassis\Installer\Alpha*.*"

  ; Create Start Menu shortcut
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"

  ; Write uninstall info
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "UninstallString" "$INSTDIR\Uninstall.exe"
SectionEnd

;--------------------------------
; Section: Uninstall
;--------------------------------
Section "Uninstall"
  ; Remove installed files
  Delete "$INSTDIR*.*"
  RMDir /r "$INSTDIR"

  ; Remove Start Menu shortcut
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Remove registry entry
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
SectionEnd
