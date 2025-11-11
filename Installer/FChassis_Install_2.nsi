;--------------------------------
; General Settings
;--------------------------------
!define APPNAME "FChassis"
!define COMPANY "Teckinsoft Neuronics Pvt. Ltd."
!define VERSION "1.0.0"
!define INSTALLDIR "C:\FChassis"

!define HWND_BROADCAST 0xffff
!define WM_SETTINGCHANGE 0x001A

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
  File /r "C:\FluxSDK\*.*"

  ; Create Start Menu shortcut
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\FChassis.exe"

  ; Explicitly include the pre-packaged archive
  File "C:\FluxSDK\thirdParty.zip"

  ; Extract the archive into $INSTDIR\thirdParty
  nsisunz::Unzip "$INSTDIR\thirdParty.zip" "$INSTDIR"
  Pop $0
  StrCmp $0 "success" +2
    MessageBox MB_OK "Unzip failed: $0"

  ; Delete the archive after extraction
  ; Delete "$INSTDIR\thirdParty.zip"
  
  ; Write uninstall info
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "UninstallString" "$INSTDIR\Uninstall.exe"

  ; === Add third-party bin folders to PATH ===
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
  Call AddToPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
  Call AddToPath
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

  ; === Remove from PATH ===
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
  Call un.RemoveFromPath
  Push "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"
  Call un.RemoveFromPath
SectionEnd

;--------------------------------
; === FUNCTIONS: PATH handling ===
;--------------------------------
Function AddToPath
  Exch $0
  Push $1
  ReadRegStr $1 HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
  StrCmp $1 "" AddPathEntry
  Push $1
  Push $0
  Call StrStr
  Pop $1
  StrCmp $1 "" AddPathEntry Done
  AddPathEntry:
    ReadRegStr $1 HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
    StrCpy $1 "$1;$0"
    WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path" $1
    SendMessage ${HWND_BROADCAST} ${WM_SETTINGCHANGE} 0 "STR:Environment" /TIMEOUT=5000
  Done:
  Pop $1
  Pop $0
FunctionEnd

;--------------------------------
; === UNINSTALL FUNCTIONS (with un. prefix) ===
;--------------------------------
Function un.RemoveFromPath
  Exch $0
  Push $1
  Push $2
  ReadRegStr $1 HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
  StrCpy $2 "$1"
  Push "$0;"
  Push $2
  Call un.StrReplace
  Pop $2
  Push "$0"
  Push $2
  Call un.StrReplace
  Pop $2
  WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path" $2
  SendMessage ${HWND_BROADCAST} ${WM_SETTINGCHANGE} 0 "STR:Environment" /TIMEOUT=5000
  Pop $2
  Pop $1
  Pop $0
FunctionEnd

;--------------------------------
; === Helper Functions: StrStr & StrReplace ===
;--------------------------------
Function StrStr
  Exch $R1
  Exch
  Exch $R0
  Push $R2
  Push $R3
  Push $R4
  Push $R5
  StrLen $R2 $R0
  StrLen $R3 $R1
  StrCpy $R4 0
  loop:
    StrCpy $R5 $R1 $R2 $R4
    StrCmp $R5 $R0 done
    IntOp $R4 $R4 + 1
    StrCmp $R4 $R3 done
    Goto loop
  done:
    StrCpy $R1 $R5
  Pop $R5
  Pop $R4
  Pop $R3
  Pop $R2
  Pop $R0
  Exch $R1
FunctionEnd

; === Uninstall helper versions ===
Function un.StrReplace
  Exch $R1
  Exch
  Exch $R0
  Push $R2
  Push $R3
  Push $R4
  Push $R5
  Push $R6
  StrLen $R2 $R0
  StrLen $R3 $R1
  StrCpy $R4 0
  StrCpy $R6 ""
  loop:
    StrCpy $R5 $R1 $R2 $R4
    StrCmp $R5 $R0 found
    StrCmp $R4 $R3 done
    IntOp $R4 $R4 + 1
    Goto loop
  found:
    StrCpy $R6 "$R6$R1" $R4
    StrCpy $R6 "$R6$R5" "" $R4
    StrCpy $R4 $R3
  done:
    StrCpy $R1 $R6
  Pop $R6
  Pop $R5
  Pop $R4
  Pop $R3
  Pop $R2
  Pop $R0
  Exch $R1
FunctionEnd
