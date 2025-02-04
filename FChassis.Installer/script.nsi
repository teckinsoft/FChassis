; Define installer properties
RequestExecutionLevel user ; Prevent the installer from asking for admin permission
Name "FChassis Installer"

!define OUTPUT_PATH "..\..\FChassis-Installer"
!define FILES_PATH "..\..\FChassis-Installer\files"

OutFile "${OUTPUT_PATH}\FChassis_Setup.exe" ; Sets the output installer file name
InstallDir "C:\FluxSDK" ; Sets the default installation directory

!define SRC_DIR C:\FluxSDK
!define SRC_BIN_DIR C:\FluxSDK\Bin

; Include modern UI for better user experience
!include "MUI2.nsh"

; Page setup
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

; Uninstaller page
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Installer Sections
SectionGroup /e "Installation"
    Section "Dot Net 8.0" SecDotNet8_0
        ; Check if .NET 8.0 is already installed
        ReadRegStr $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Version"
        StrCmp $0 "" InstallDotNet8 NoInstall

        InstallDotNet8:
        DetailPrint "Installing .NET 8.0..."
        ExecWait '"${FILES_PATH}\dotnet-sdk-8.0.405-win-x64.exe" /quiet /norestart' $1
        IfErrors 0 NoInstall
        MessageBox MB_ICONSTOP "Installation failed! Please install .NET 8.0 manually."

        NoInstall:        
        DetailPrint "Already .NET 8.0 installed..."
    SectionEnd

    Section "Flux SDK.4 Setup" SecFluxSDKSetup
        ; Run Setup.FluxSDK.4.exe from the NSIS script folder and wait for it to complete
        ExecWait '"${FILES_PATH}\Setup.FluxSDK.4.exe"'
    SectionEnd

    Section "FChassis Installation" SecInstallation
        ; Create installation directory
        SetOutPath "${SRC_BIN_DIR}"
        
        ; Copy FChassis files
        File "${SRC_BIN_DIR}\FChassis.exe"
        File "${SRC_BIN_DIR}\FChassis.dll"
        File "${SRC_BIN_DIR}\FChassis.runtimeconfig.json"
        File "${SRC_BIN_DIR}\CommunityToolkit.Mvvm.dll"
        File "${SRC_BIN_DIR}\MathNet.Numerics.dll"
        File "${FILES_PATH}\FChassis.ico"
        
        ; Copy the entire FChassis folder to C:\FluxSDK\map
        ;SetOutPath "$INSTDIR\map"
        ;File /r "${FILES_PATH}\map\*.*" ; Copy all files in the FChassis folder recursively
        
        ; Map C:\FluxSDK\map to W: drive
        ;nsExec::ExecToLog 'subst W: "$INSTDIR\map"'
        
        ; Write the subst command to run on startup
        ;WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassisMapWDrive" 'subst W: "$INSTDIR\map"'
        
        ; Create the uninstaller
        WriteUninstaller "$INSTDIR\Uninstall.exe"
    
        ; Create a shortcut for FChassis.exe on the Desktop
        CreateShortCut "$DESKTOP\FChassis.lnk" "${SRC_BIN_DIR}\FChassis.exe" "" "${SRC_BIN_DIR}\FChassis.ico" 0
    SectionEnd

    Section "Data Folder Mapping" SecFluxDataFolderMapping
        ; Run Flux Data Folder Mapoing.exe
        ExecWait '"${OUTPUT_PATH}\FChassisMap_Setup.exe"'
    SectionEnd
SectionGroupEnd

; Uninstaller Section
Section "Uninstall"
    ; Remove installed files
    Delete "${SRC_BIN_DIR}\FChassis.exe"
    Delete "${SRC_BIN_DIR}\FChassis.dll"
    Delete "${SRC_BIN_DIR}\FChassis.ico"
    Delete "${SRC_BIN_DIR}\FChassis.runtimeconfig.json"
    Delete "${SRC_BIN_DIR}\CommunityToolkit.Mvvm.dll"
    Delete "${SRC_BIN_DIR}\MathNet.Numerics.dll"
    
    ; Remove the map directory
    ;RMDir /r "$INSTDIR\map"
    
    ; Remove shortcut
    Delete "$DESKTOP\FChassis.lnk"

    ; Remove the registry entry
    ;DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassisMapWDrive"

    ; Remove the W: drive mapping
    ;nsExec::ExecToLog 'subst W: /D'

    ; Remove the uninstaller file itself
    Delete "$INSTDIR\Uninstall.exe"
    
    ; Remove the installation directory (if empty)
    RMDir "$INSTDIR"
SectionEnd