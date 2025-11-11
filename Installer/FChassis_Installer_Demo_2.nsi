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
RequestExecutionLevel admin  ; require admin rights
  
; Include String Functions
!include "LogicLib.nsh"
!include "x64.nsh"
!include "StrFunc.nsh"
${UnStrRep}

;--------------------------------
; Pages
;--------------------------------
Page directory
Page instfiles
UninstPage instfiles

;--------------------------------
; Section: Install
;--------------------------------
Section "Install"
  ; Set install dir
  SetOutPath "$INSTDIR"
  
  ; Copy all files from build output
  File /r "C:\D drive\Projects\Fchassis\Main FChassis\FChassis\Installer\Alpha\*.*"

  ; Create Start Menu shortcut
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"

  ; Write uninstall info
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "UninstallString" "$INSTDIR\Uninstall.exe"


    EnVar::SetHKLM  ; Or SetHKCU for current user
	EnVar::AddValue "Path" "$INSTDIR\Senior2"
	Pop $0  ; 0 = success

  ; Add Senior and Junior folders to system PATH
  ReadRegStr $0 HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
  ; StrCpy $0 "$0;$INSTDIR\Senior;$INSTDIR\Junior"
  MessageBox MB_OK "PATH before anything is: $0"
  StrCpy $0 "$0;$INSTDIR\Senior"
  StrCpy $0 "$0;$INSTDIR\Senior1"
  StrCpy $0 "$0;$INSTDIR\Junior"
  StrCpy $0 "$0;$INSTDIR\Junior1"
  MessageBox MB_OK "PATH value is: $0"
  WriteRegStr HKCU "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path" "$0"
SectionEnd

;--------------------------------
; Section: Uninstall
;--------------------------------
Section "Uninstall"
  ; Remove installed files
  Delete "$INSTDIR\*.*"
  RMDir /r "$INSTDIR"

  ; Remove Start Menu shortcut
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Remove registry entry
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

  ; Remove Senior and Junior folders from system PATH
  ReadRegStr $0 HKCU "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
  MessageBox MB_OK "PATH before anything is: $0"
  StrCpy $1 ";$INSTDIR\Senior"
  StrCpy $2 ";$INSTDIR\Senior1"
  StrCpy $3 ";$INSTDIR\Junior"
  StrCpy $4 ";$INSTDIR\Junior1"
  ${UnStrRep} $0 $0 "$1" ""
  ${UnStrRep} $0 $0 "$2" ""
  ${UnStrRep} $0 $0 "$3" ""
  ${UnStrRep} $0 $0 "$4" ""
  MessageBox MB_OK "PATH value is: $0"
  WriteRegStr HKCU "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path" "$0"
SectionEnd