; Define installer properties
RequestExecutionLevel user ; Prevent the installer from asking for admin permission
Name "FChassis Installer"

; Include modern UI for better user experience
!include "MUI2.nsh"
!include "nsDialogs.nsh"  

; /////////////////////////////////////////////////////////////////////////////////////////////////
!define OUTPUT_PATH "..\..\FChassis-Installer" 
!define FILES_PATH "..\..\FChassis-Installer\files"
!define SRC_DIR C:\FluxSDK 
!define SRC_BIN_DIR C:\FluxSDK\Bin

!define REG_MAP_PATH "Software\TeckinSoft\FChassis"
!define REG_MAP_PATH_KEY "MapPath"

!define REG_MAP_DRIVE "Software\Microsoft\Windows\CurrentVersion\Run"
!define REG_MAP_DRIVE_KEY "TIS_FChassisMapDrive"

OutFile "${OUTPUT_PATH}\FChassis_Setup.exe" ; Sets the output installer file name
InstallDir "C:\FluxSDK" ; Sets the default installation directory

; Page setup --------------------------------------------------------
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
Page custom MapPathPage
!insertmacro MUI_PAGE_INSTFILES

; Uninstaller page --------------------------------------------------
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Variable to store user-specified map path -------------------------
Var MapPath
Var TextBox
Var FolderTextBox

; /////////////////////////////////////////////////////////////////////////////////////////////////
; Installer Sections
SectionGroup /e "Installation"
    Section "Dot Net 8.0" SecDotNet8_0
        ; Check if .NET 8.0 is already installed
        ;ReadRegStr $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Version"
        ;StrCmp $0 "" InstallDotNet8 NoInstall

        Push "Dot Net v8.0" 
        Push "dotnet-sdk-8.0.405-win-x64.exe" 
        Push "$EXEDIR\files" 
        Call CheckAndRun
    SectionEnd

    Section "Flux SDK.4 Setup" SecFluxSDKSetup
        Push "FluxSDK v4" 
        Push "Setup.FluxSDK.4.exe" 
        Push "$EXEDIR\files" 
        Call CheckAndRun
    SectionEnd

    Section "FChassis Installation" SecInstallation
        Call CopyProgramFiles
    SectionEnd

    Section "Data Folder Mapping" SecFluxDataFolderMapping
        call MapAndCopyDataFiles
        ;Push "Un Map Data Folder" 
        ;Push "FChassisMap_Setup.exe" 
        ;Push "$EXEDIR\" 
        ;Call CheckAndRun
    SectionEnd
SectionGroupEnd

; Uninstaller Section
Section "Uninstall"
    Call un.DeleteProgramFiles
    Call un.UnmapAndDeleteDataFiles

    Delete "$INSTDIR\Uninstall.exe" ; Remove the uninstaller file itself
    RMDir "$INSTDIR"                ; Remove the installation directory (if empty)
SectionEnd

; /////////////////////////////////////////////////////////////////////////////////////////////////
; Check given path/file whether exist and run, 
; otherwise shows Error message
Function CheckAndRun
    Pop $R0 ; Path
    Pop $R1 ; FileName
    Pop $R2 ; FileNameDesc

    DetailPrint "Installing $R2..."
    IfFileExists "$R0\$R1" 0 NotFound
    ExecWait '"$R0\$R1"' $0
    Goto Done

    NotFound:
    MessageBox MB_ICONSTOP "'$R0\$R1' is not found!"
    Done:
FunctionEnd

; ===================================================================
Function CopyProgramFiles
    ; Create installation directory
    SetOutPath "${SRC_BIN_DIR}"
        
    ; Copy FChassis files
    File "${SRC_BIN_DIR}\FChassis.exe"
    File "${SRC_BIN_DIR}\FChassis.dll"
    File "${SRC_BIN_DIR}\FChassis.runtimeconfig.json"
    File "${SRC_BIN_DIR}\CommunityToolkit.Mvvm.dll"
    File "${SRC_BIN_DIR}\MathNet.Numerics.dll"
    File "${FILES_PATH}\FChassis.ico"
        
    ; Create the uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Create a shortcut for FChassis.exe on the Desktop
    CreateShortCut "$DESKTOP\FChassis.lnk" "${SRC_BIN_DIR}\FChassis.exe" "" "${SRC_BIN_DIR}\FChassis.ico" 0
FunctionEnd

; ---------------------------------------------------------
Function un.DeleteProgramFiles
    ; Remove installed files
    Delete "${SRC_BIN_DIR}\FChassis.exe"
    Delete "${SRC_BIN_DIR}\FChassis.dll"
    Delete "${SRC_BIN_DIR}\FChassis.ico"
    Delete "${SRC_BIN_DIR}\FChassis.runtimeconfig.json"
    Delete "${SRC_BIN_DIR}\CommunityToolkit.Mvvm.dll"
    Delete "${SRC_BIN_DIR}\MathNet.Numerics.dll"
    
    ; Remove shortcut
    Delete "$DESKTOP\FChassis.lnk"
FunctionEnd

; ===================================================================
Function MapAndCopyDataFiles
    ; Ensure selected directory exists
    IfFileExists "$MapPath" 0 CreateMapFolder
    Goto CopyFiles
CreateMapFolder:
    CreateDirectory "$MapPath"

CopyFiles:
    ; Copy files to user-selected "map" folder
    SetOutPath "$MapPath"
    File /r "${FILES_PATH}\map\*.*" ; Copy all files in the FChassis folder recursively
        
    ; Map C:\FluxSDK\map to W: drive
    nsExec::ExecToLog 'subst W: "$MapPath"'
    DetailPrint "Mapped W: $MapPath"
        
    ; Write the subst command to run on startup
    WriteRegStr HKCU "${REG_MAP_DRIVE}" "${REG_MAP_DRIVE_KEY}" 'subst W: "$MapPath"'
    WriteRegStr HKCU "${REG_MAP_PATH}" "${REG_MAP_PATH_KEY}" '"$MapPath"'
       
FunctionEnd

; ---------------------------------------------------------
Function un.UnmapAndDeleteDataFiles
    ; Remove the map directory
    ReadRegStr $MapPath  HKCU "${REG_MAP_PATH}" '${REG_MAP_PATH_KEY}'

    ;MessageBox MB_OK "The installation will begin. The selected directory for the 'map' folder is: $MapPath"
    RMDir /r "$MapPath"
    
    ; Remove the registry entries
    DeleteRegValue HKCU "${REG_MAP_DRIVE}" "${REG_MAP_DRIVE_KEY}"
    DeleteRegValue HKCU "${REG_MAP_PATH}" "${REG_MAP_PATH_KEY}"

    ; Remove the W: drive mapping
    nsExec::ExecToLog 'subst W: /D'
FunctionEnd

; ===================================================================
; Function to ask user for "map" path
Function MapPathPage
    ; Initialize MapPath with default value
    StrCpy $MapPath "${SRC_DIR}\map"

    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
        Abort
    ${EndIf}

    ; Create label
    ${NSD_CreateLabel} 10 10 280 12u "Select the path for the 'map' folder:"
    Pop $1

    ; Create text input field
    ${NSD_CreateText} 10 30 220 12u "$MapPath"
    Pop $TextBox
    ${NSD_OnChange} $TextBox UpdateText

    ; Create Browse button
    ${NSD_CreateButton} 240 30 50 14u "Browse"
    Pop $2
    ${NSD_OnClick} $2 OpenFolderDialog

    nsDialogs::Show
      
    Return ; Exit properly to continue installation
FunctionEnd

Function UpdateText
    ${NSD_GetText} $TextBox $MapPath
FunctionEnd

; Function to open folder selection dialog
Function OpenFolderDialog
    nsDialogs::SelectFolderDialog "Select the 'map' folder location:" "$MapPath"
    Pop $MapPath

    ${NSD_SetText} $TextBox $MapPath
FunctionEnd