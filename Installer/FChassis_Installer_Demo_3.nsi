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
!include "FileFunc.nsh"

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
  SetShellVarContext all
  
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
	  
  ; === Map W: drive globally to Senior folder ===
  ; Persistent mapping for future logins
  ; WriteRegStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "W:" "\??\$INSTDIR\Senior"

  ; Simple drive mapping attempt
  DetailPrint "Opening CMD with subst W: $INSTDIR\Senior"
  ExecShell "open" "$WinDir\System32\cmd.exe" '/k subst W: "$INSTDIR\Senior"'
  
  ; Check if drive was created and inform user
  ${If} ${FileExists} "W:\"
    MessageBox MB_OK "W: drive has been successfully mapped!"
  ${Else}
    MessageBox MB_OK|MB_ICONINFORMATION "Drive mapping may require a restart.$\n$\nYou can manually map with: subst W: $\"$INSTDIR\Senior$\""
  ${EndIf}
SectionEnd

;--------------------------------
; Section: Uninstall
;--------------------------------
Section "Uninstall"
  SetShellVarContext all
  
  ExecWait 'subst W: /D'
  
  ; Remove installed files
  Delete "$INSTDIR\*.*"
  RMDir /r "$INSTDIR"

  ; Remove Start Menu shortcut
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Remove registry entry
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
  
  ; Remove persistence
  ; DeleteRegValue HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices" "W:"
SectionEnd