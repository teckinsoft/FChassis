;--------------------------------
; General Settings
;--------------------------------
!define APPNAME "FChassis"
!define COMPANY "Haleon"
!define VERSION "1.0.0"
!define INSTALLDIR "$PROGRAMFILES\${APPNAME}"

Name "${APPNAME} ${VERSION}"
OutFile "FChassisInstaller.exe"
InstallDir "${INSTALLDIR}"
RequestExecutionLevel admin  ; require admin rights

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
  File /r "C:\FluxSDK\Bin\*.*"

  ; Create Start Menu shortcut
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"

  ; Write uninstall info
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "UninstallString" "$INSTDIR\Uninstall.exe"
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
SectionEnd
