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
  ; Ensure shortcuts apply to all users
  SetShellVarContext all
  
  ; Set install dir
  SetOutPath "$INSTDIR"
  
  ; Copy all files from build output
  File /r "C:\FluxSDK\*.*"

  ; --- Shortcuts ---
  ; Start Menu (All Users)
  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe"

  ; Desktop (All Users)
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\Bin\FChassis.exe"

  ; NEW: Include the pre-packaged RAR and 7z.exe (must be in C:\MYFile\Bin)
  File "C:\FluxSDK\thirdParty.zip"
  File "C:\FluxSDK\7z.exe"
  File "C:\FluxSDK\7z.dll"

  ; Extract using 7z.exe
  nsExec::ExecToLog '"$INSTDIR\7z.exe" x "$INSTDIR\thirdParty.zip" -o"$INSTDIR" -y'
  
  ; Write uninstall info
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" \
      "UninstallString" "$INSTDIR\Uninstall.exe"

  ; === Add third-party bin folders to PATH ===
  EnVar::SetHKLM  ; Or SetHKCU for current user
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
  Delete "$INSTDIR\*.*"
  RMDir /r "$INSTDIR"

  ; Remove Start Menu shortcut
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  RMDir "$SMPROGRAMS\${APPNAME}"

  ; Remove Desktop shortcut
  Delete "$DESKTOP\${APPNAME}.lnk"

  ; Remove registry entry
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
  
  ; === Remove from PATH ===
  EnVar::SetHKLM  ; Or SetHKCU for current user
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
  
  ; Remove the virtual drive during uninstallation
  nsExec::Exec '"cmd.exe" /C subst W: /D'

  ; Remove only the specific registry key for your virtual drive
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MyVirtualDrive"
SectionEnd