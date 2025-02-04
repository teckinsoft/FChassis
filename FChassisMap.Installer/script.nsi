; Define installer properties
RequestExecutionLevel user ; Prevent the installer from asking for admin permission
Name "FChassis Map Installer"

!define OUTPUT_PATH "..\..\FChassis-Installer"
!define FILES_PATH "..\..\FChassis-Installer\files"

OutFile "${OUTPUT_PATH}\FChassisMap_Setup.exe" ; Sets the output installer file name
InstallDir "C:\FluxSDK" ; Sets the default installation directory

!define REG_MAP_PATH "Software\TeckinSoft\FChassis"
!define REG_MAP_PATH_KEY "MapPath"

!define REG_MAP_DRIVE "Software\Microsoft\Windows\CurrentVersion\Run"
!define REG_MAP_DRIVE_KEY "TIS_FChassisMapDrive"

!define MAP_DRIVE_UNINSTALL "UninstallMap"

; Include modern UI for better user experience
!include "MUI2.nsh"
!include "nsDialogs.nsh"  ; Ensure nsDialogs is included

; Page setup
Page custom GetMapPath           ; Custom page for "map" path selection
!insertmacro MUI_PAGE_INSTFILES

; Uninstaller page
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Variable to store user-specified map path
Var MAP_PATH
Var TextBox
Var FolderTextBox

; Function to ask user for "map" path
Function GetMapPath
    ; Initialize MAP_PATH with default value
    StrCpy $MAP_PATH "C:\FluxSDK\map"

    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
        Abort
    ${EndIf}

    ; Create label
    ${NSD_CreateLabel} 10 10 280 12u "Select the path for the 'map' folder:"
    Pop $1

    ; Create text input field
    ${NSD_CreateText} 10 30 220 12u "$MAP_PATH"
    Pop $TextBox
    ${NSD_OnChange} $TextBox UpdateText

    ; Create Browse button
    ${NSD_CreateButton} 240 30 50 14u "Browse"
    Pop $2
    ${NSD_OnClick} $2 OpenFolderDialog

    nsDialogs::Show
FunctionEnd

Function UpdateText
    ${NSD_GetText} $TextBox $MAP_PATH
FunctionEnd

; Function to open folder selection dialog
Function OpenFolderDialog
    nsDialogs::SelectFolderDialog "Select the 'map' folder location:" "$MAP_PATH"
    Pop $MAP_PATH

    ${NSD_SetText} $TextBox $MAP_PATH
FunctionEnd

; Installer Sections
Section "FChassis Installation" SecInstallation
    ; Ensure selected directory exists
    IfFileExists "$MAP_PATH" 0 CreateMapFolder
    Goto CopyFiles

CreateMapFolder:
    CreateDirectory "$MAP_PATH"

CopyFiles:
    ; Copy files to user-selected "map" folder
    SetOutPath "$MAP_PATH"
    File /r "${FILES_PATH}\map\*.*" ; Copy all files in the FChassis folder recursively
        
    ; Map C:\FluxSDK\map to W: drive
    nsExec::ExecToLog 'subst W: "$MAP_PATH"'
    DetailPrint "Mapped W: $MAP_PATH"
        
    ; Write the subst command to run on startup
    WriteRegStr HKCU "${REG_MAP_DRIVE}" "${REG_MAP_DRIVE_KEY}" 'subst W: "$MAP_PATH"'
    WriteRegStr HKCU "${REG_MAP_PATH}" "${REG_MAP_PATH_KEY}" '"$MAP_PATH"'
        
    ; Create the uninstaller
    WriteUninstaller "$MAP_PATH\${MAP_DRIVE_UNINSTALL}.exe"
SectionEnd


; Uninstaller Section
Section "Uninstall"
    ; Remove the map directory
    ReadRegStr $MAP_PATH  HKCU "${REG_MAP_PATH}" '${REG_MAP_PATH_KEY}'

    ;MessageBox MB_OK "The installation will begin. The selected directory for the 'map' folder is: $MAP_PATH"
    RMDir /r "$MAP_PATH"
    
    ; Remove the registry entries
    DeleteRegValue HKCU "${REG_MAP_DRIVE}" "${REG_MAP_DRIVE_KEY}"
    DeleteRegValue HKCU "${REG_MAP_PATH}" "${REG_MAP_PATH_KEY}"

    ; Remove the W: drive mapping
    nsExec::ExecToLog 'subst W: /D'

    ; Remove the uninstaller file itself
    Delete "$MAP_PATH\${MAP_DRIVE_UNINSTALL}.exe"
SectionEnd