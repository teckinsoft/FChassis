!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

;--------------------------------
; Build-time defines (from MSBuild /DVERSION=... /DINSTALLDIR=... /DPayloadDir=... /DInstallerOutputDir=...)
;--------------------------------
!ifndef VERSION
  !define VERSION "1.0.0"
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

!define HWND_BROADCAST 0xffff
!define WM_SETTINGCHANGE 0x001A

Name "${APPNAME} ${VERSION}"
OutFile "FChassisInstaller.exe"
InstallDir "${INSTALLDIR}"
RequestExecutionLevel admin

;--------------------------------
; Variables
;--------------------------------
Var InstalledState ; 0 = not installed, 1 = installed, 2 = newer version installed

;--------------------------------
; Pages
;--------------------------------
!include "MUI2.nsh"

!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING

!define MUI_PAGE_CUSTOMFUNCTION_PRE DirectoryPre
!insertmacro MUI_PAGE_DIRECTORY

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
; Installation detection
;--------------------------------
Function .onInit
  StrCpy $InstalledState 0 ; Default to not installed
  
  ; Check if already installed
  ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
  IfErrors notInstalled
  
  ; Check installed version
  ReadRegStr $1 HKLM "${UNINSTALL_KEY}" "DisplayVersion"
  IfErrors versionCheckDone
  
  ; Compare versions
  ${If} $1 == "${VERSION}"
    StrCpy $InstalledState 1 ; Same version installed
  ${ElseIf} $1 > "${VERSION}"
    StrCpy $InstalledState 2 ; Newer version installed
  ${Else}
    StrCpy $InstalledState 1 ; Older version installed (will upgrade)
  ${EndIf}
  
  versionCheckDone:
  Goto done
  
  notInstalled:
  StrCpy $InstalledState 0
  
  done:
FunctionEnd

Function DirectoryPre
  ${If} $InstalledState == 2
    MessageBox MB_OK|MB_ICONEXCLAMATION "A newer version of ${APPNAME} is already installed.$\nPlease uninstall it first before installing this version."
    Abort
  ${EndIf}
FunctionEnd

;--------------------------------
; Section: Install
;--------------------------------
Section "Install"
  ; Check if we need to show repair options
  ${If} $InstalledState == 1
    MessageBox MB_YESNOCANCEL|MB_ICONQUESTION \
      "${APPNAME} is already installed.$\n$\nClick $\"Yes$\" to reinstall/repair.$\nClick $\"No$\" to uninstall first.$\nClick $\"Cancel$\" to exit." \
      /SD IDYES \
      IDYES proceedWithInstall
      IDNO uninstallFirst
      IDCANCEL abortInstall
    
    uninstallFirst:
      ; Run uninstaller
      ReadRegStr $0 HKLM "${UNINSTALL_KEY}" "UninstallString"
      ExecWait '$0 _?=$INSTDIR'
      Goto proceedWithInstall
    
    abortInstall:
      Abort
    
    proceedWithInstall:
  ${EndIf}

  ; Ensure shortcuts apply to all users
  SetShellVarContext all
  
  ; Set install dir
  SetOutPath "$INSTDIR"
  
  ; Copy all files from build output (using parameter from MSBuild)
  File /r "${PayloadDir}\*.*"

  ; --- Shortcuts ---
  ; Start Menu (All Users)
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe"

  ; Desktop (All Users)
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe"

  ; Include the pre-packaged files
  File "${ThirdPartyDir}\thirdParty.zip"
  File "${ThirdPartyDir}\7z.exe"
  File "${ThirdPartyDir}\7z.dll"
  File "${ThirdPartyDir}\VC_redist.x64.exe"

  ; Extract using 7z.exe
  nsExec::ExecToLog '"$INSTDIR\7z.exe" x "$INSTDIR\thirdParty.zip" -o"$INSTDIR" -y'
  
  ; Write uninstall info with proper registry entries for repair
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${COMPANY}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\Bin\FChassis.exe,0"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 0 ; Allow repair
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "EstimatedSize" 102400 ; Approximate size in KB

  ; === Add third-party bin folders to PATH ===
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
  
  ; Set CASROOT environment variable
  WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "CASROOT" "$INSTDIR\thirdParty\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0"
  !insertmacro BroadcastEnvChange

  ; === Install VC++ Redistributable (x64) silently ===
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" "Installed"
  StrCmp $0 1 vcskip
    ExecWait '"$INSTDIR\VC_redist.x64.exe" /install /quiet /norestart'
  vcskip:
  
  ; === Map W: drive persistently to thirdParty folder ===
  ; Run the subst command during installation
  nsExec::Exec '"cmd.exe" /C subst W: "$INSTDIR\Map"'

  ; Ensure the drive persists after reboot by adding it to the registry
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive" '"cmd.exe" /C subst W: "$INSTDIR\Map"'

  ; Clean up after extraction
  Delete "$INSTDIR\thirdParty.zip"
  Delete "$INSTDIR\VC_redist.x64.exe"
  Delete "$INSTDIR\7z.exe"
  Delete "$INSTDIR\7z.dll"
SectionEnd

;--------------------------------
; Section: Uninstall
;--------------------------------
Section "Uninstall"
  ; Ensure we remove from all users
  SetShellVarContext all

  ; Remove installed files
  RMDir /r "$INSTDIR"

  ; Remove Start Menu shortcut
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Remove Desktop shortcut
  Delete "$DESKTOP\${APPNAME}.lnk"

  ; Remove registry entry
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  
  ; === Remove from PATH ===
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

  ; Remove the virtual drive during uninstallation
  nsExec::Exec '"cmd.exe" /C subst W: /D'

  ; Remove only the specific registry key for your virtual drive
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"
SectionEnd