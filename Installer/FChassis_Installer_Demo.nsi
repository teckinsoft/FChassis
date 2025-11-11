; -------------------------------
; FChassis Installer
; -------------------------------
Unicode true
Name "FChassis"
OutFile "FChassis_Setup.exe"
InstallDir "C:\FChassis"

; Request execution level (admin required for system PATH modification)
RequestExecutionLevel admin

; Include modern UI
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

; -------------------------------
; Interface Configuration
; -------------------------------
!define MUI_ABORTWARNING
!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"

; -------------------------------
; Pages
; -------------------------------
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; -------------------------------
; Languages
; -------------------------------
!insertmacro MUI_LANGUAGE "English"

; -------------------------------
; Variables
; -------------------------------
Var BinSourcePath

; -------------------------------
; Installation Section
; -------------------------------
Section "Main Installation" SecMain

    ; Set source path for binaries
    StrCpy $BinSourcePath "C:\FluxSDK\bin"
    
    ; Verify source directory exists
    IfFileExists "$BinSourcePath\*.*" source_exists
        MessageBox MB_OK|MB_ICONEXCLAMATION "Source directory not found: $\r$\n$BinSourcePath$\r$\nPlease install FluxSDK first."
        Abort
    source_exists:

    ; Set output path for installation
    SetOutPath "$INSTDIR\Bin"
    
    ; Copy all files from FluxSDK bin to FChassis Bin
    File /r "$BinSourcePath\*.*"
    
    ; Create uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Create start menu shortcuts
    CreateDirectory "$SMPROGRAMS\FChassis"
    CreateShortcut "$SMPROGRAMS\FChassis\FChassis.lnk" "$INSTDIR\Bin\FChassis.exe"
    CreateShortcut "$SMPROGRAMS\FChassis\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
    
    ; Write registry information for uninstaller
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis" \
        "DisplayName" "FChassis"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis" \
        "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis" \
        "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis" \
        "Publisher" "Your Company Name"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis" \
        "DisplayVersion" "1.0.0"
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis" \
        "NoModify" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis" \
        "NoRepair" 1

SectionEnd

; -------------------------------
; PATH Environment Variable Section
; -------------------------------
Section "Add to PATH" SecPath
    ; Add installation bin directory to system PATH
    EnVar::SetHKCU
    EnVar::AddValue "PATH" "$INSTDIR\Bin"
    
    ; Also add to system PATH (requires admin)
    EnVar::SetHKLM
    EnVar::AddValue "PATH" "$INSTDIR\Bin"
    
    ; Broadcast environment change
    SendMessage ${HWND_BROADCAST} ${WM_WININICHANGE} 0 "STR:Environment" /TIMEOUT=5000
    
SectionEnd

; -------------------------------
; Uninstaller Section
; -------------------------------
Section "Uninstall"

    ; Remove files
    RMDir /r "$INSTDIR\Bin"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"

    ; Remove start menu shortcuts
    RMDir /r "$SMPROGRAMS\FChassis"

    ; Remove from PATH environment variable
    EnVar::SetHKCU
    EnVar::DeleteValue "PATH" "$INSTDIR\Bin"
    
    EnVar::SetHKLM
    EnVar::DeleteValue "PATH" "$INSTDIR\Bin"
    
    ; Broadcast environment change
    SendMessage ${HWND_BROADCAST} ${WM_WININICHANGE} 0 "STR:Environment" /TIMEOUT=5000

    ; Remove registry entries
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\FChassis"

SectionEnd

; -------------------------------
; Functions
; -------------------------------
Function .onInit
    ; Check if FluxSDK is installed
    IfFileExists "C:\FluxSDK\bin\*.*" flux_installed
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
            "FluxSDK not found at C:\FluxSDK\bin.$\r$\n$\r$\nPlease install FluxSDK first or place binaries in the correct location." \
            IDOK continue_install IDCANCEL abort_install
    flux_installed:
    Goto continue_install
    
    abort_install:
        Abort
    
    continue_install:
FunctionEnd

Function .onInstSuccess
    MessageBox MB_YESNO|MB_ICONQUESTION \
        "Installation completed successfully.$\r$\n$\r$\nWould you like to run FChassis now?" \
        IDNO no_run
        Exec "$INSTDIR\Bin\FChassis.exe"
    no_run:
FunctionEnd