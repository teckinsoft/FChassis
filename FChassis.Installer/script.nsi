; Define installer properties
RequestExecutionLevel user ; Prevent the installer from asking for admin permission
Name "FChassis Installer"

!verbose 1
!define TRUE 1
!define FALSE 0

; Include modern UI for better user experience
!include "MUI2.nsh"
!include "nsDialogs.nsh"  

; /////////////////////////////////////////////////////////////////////////////////////////////////
!define TARGET_DIR C:\FluxSDK
!define TARGET_BIN_DIR ${TARGET_DIR}\bin

!define SRC_DIR "${TARGET_DIR}"
!define SRC_BIN_DIR "${SRC_DIR}\Bin"

!define REG_MAP_PATH "Software\TeckinSoft\FChassis"
!define REG_MAP_PATH_KEY "MapPath"

!define REG_MAP_DRIVE "Software\Microsoft\Windows\CurrentVersion\Run"
!define REG_MAP_DRIVE_KEY "TIS_FChassisMapDrive"

OutFile "${BUILD_DIR}\FChassis_Setup.exe"             ; Sets the output installer file name
InstallDir "${TARGET_DIR}"                                      ; Sets the default installation directory

; Page setup --------------------------------------------------------
!insertmacro MUI_PAGE_COMPONENTS
 Page custom MapPathPage
!insertmacro MUI_PAGE_INSTFILES

; Uninstaller page --------------------------------------------------
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Variable to store user-specified map path -------------------------
Var MapPath
Var TextBox
Var FolderTextBox
Var isMapDriveEnabled

Function .onInit
    !if !defined(PROJECT_DIR) || !defined(BUILD_DIR)
        MessageBox MB_OK "Either PROJECT_DIR or BUILD_DIR not defined!"
        Abort
    !endif

    StrCpy $isMapDriveEnabled ${TRUE}    
FunctionEnd

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
    SectionEnd
SectionGroupEnd

; Uninstaller Section
Section "Uninstall"
    Call un.DeleteProgramFiles
    Call un.UnmapAndDeleteDataFiles

    Delete "${TARGET_BIN_DIR}\Uninstall.exe" ; Remove the uninstaller file itself
    RMDir "$INSTDIR"                         ; Remove the installation directory (if empty)
SectionEnd

; /////////////////////////////////////////////////////////////////////////////////////////////////
; ===================================================================
; -- Check and Run --------------------------------------------------
; Check given path/file whether exist and run, 
; otherwise shows Error message
Function CheckAndRun
    Pop $R0 ; Path
    Pop $R1 ; FileName
    Pop $R2 ; FileNameDesc
    Pop $R3 ; Arg1
    Pop $R4 ; Arg2

    DetailPrint "Installing $R2..."
    IfFileExists "$R0\$R1" 0 NotFound
    ExecWait '"$R0\$R1" "$R3" "$R4"' $0
    Goto Done

    NotFound:
    MessageBox MB_ICONSTOP "'$R0\$R1' is not found!"
    Done:
FunctionEnd

; .........................................................
Function un.CheckAndRun
    Pop $R0 ; Path
    Pop $R1 ; FileName
    Pop $R2 ; FileNameDesc
    Pop $R3 ; Arg1
    Pop $R4 ; Arg2

    DetailPrint "Uninstalling $R2..."
    IfFileExists "$R0\$R1" 0 NotFound
    ExecWait '"$R0\$R1" "$R3" "$R4"' $0
    Goto Done

    NotFound:
    MessageBox MB_ICONSTOP "'$R0\$R1' is not found!"
    Done:
FunctionEnd

; ===================================================================
; -- Copy Program Files ---------------------------------------------
Function CopyProgramFiles
    ;SetOutPath "${TARGET_BIN_DIR}"

    ; Copy FChassis program files
    ;File /r "P:\Paratha\New folder\FChassis-Merge\FChassis.Installer\bat\*.*"     ; Copy bat folder having batch files recursively

    DetailPrint "Copying Prj\bat\*.*"
    CopyFiles "${PROJECT_DIR}\bat\*.*" "${TARGET_BIN_DIR}"
    DetailPrint ""
    
    DetailPrint "Copying script\files\bin\*.*" 
    CopyFiles "$EXEDIR\files\bin\*.*" "${TARGET_BIN_DIR}"
    DetailPrint ""

    DetailPrint "Copying script\files\FChassis.ico"
    CopyFiles "$EXEDIR\files\FChassis.ico" "${TARGET_BIN_DIR}"
    DetailPrint ""

    ; Create the uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Create a shortcut for FChassis.exe on the Desktop
    CreateShortCut "$DESKTOP\FChassis.lnk" "${SRC_BIN_DIR}\FChassis.exe" "" "${SRC_BIN_DIR}\FChassis.ico" 0
FunctionEnd

; .........................................................
Function un.DeleteProgramFiles
    DetailPrint ${TARGET_BIN_DIR}
    DetailPrint $EXEDIR

    Push "programfiles.txt" 
    Push "${TARGET_BIN_DIR}" 
    call un.RemoveProgramFiles

    Push "dependentFiles.txt" 
    Push "${TARGET_BIN_DIR}" 
    ;call un.RemoveProgramFiles

    Delete "${TARGET_BIN_DIR}\programFiles.txt" 
    Delete "${TARGET_BIN_DIR}\dependentFiles.txt" 

    ; Remove shortcut
    Delete "$DESKTOP\FChassis.lnk" 
    Delete "$INSTDIR\Uninstall.exe"
FunctionEnd

; ===================================================================
; -- Map Copy -------------------------------------------------------
Function MapAndCopyDataFiles
    ;StrCpy $MapPath "${SRC_DIR}\map"

    ; Ensure selected directory exists
    IfFileExists "$MapPath" 0 CreateMapFolder
    Goto CopyFiles

CreateMapFolder:
    DetailPrint "Mapping path $MapPath to W:"
    CreateDirectory "$MapPath"

CopyFiles:
    ; Copy Mapping Data files to user-selected "map" folder
    CopyFiles "$EXEDIR\files\map\*.*" "$MapPath" ;Copy all files in the FChassis data files folder recursively

    ;nsExec::ExecToLog 'subst W: /D'
        
    ; Map C:\FluxSDK\map to W: drive
    nsExec::ExecToLog 'subst W: "$MapPath"'
    DetailPrint "Mapped W: $MapPath"
        
    ; Write the subst command to run on startup
    WriteRegStr HKCU "${REG_MAP_DRIVE}" "${REG_MAP_DRIVE_KEY}" 'subst W: "$MapPath"'
    WriteRegStr HKCU "${REG_MAP_PATH}" "${REG_MAP_PATH_KEY}" '"$MapPath"'       
FunctionEnd

; ...................................................................
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
; -- Map Page -------------------------------------------------------
; Function to ask user for "map" path
Function MapPathPage
    ; Initialize MapPath with default value
    StrCpy $MapPath "${SRC_DIR}\map"

    nsDialogs::Create 1018
    Pop $0
    ${If} $0 == error
        Abort
    ${EndIf}

    call CheckMapPath
    ${If} $isMapDriveEnabled == 0
        EnableWindow $0 0
        Return
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
FunctionEnd

; ...................................................................
Function UpdateText
    ${NSD_GetText} $TextBox $MapPath
FunctionEnd

; Function to open folder selection dialog
Function OpenFolderDialog
    nsDialogs::SelectFolderDialog "Select the 'map' folder location:" "$MapPath"
    Pop $MapPath

    ${NSD_SetText} $TextBox $MapPath
FunctionEnd

; ...................................................................
Function CheckMapPath
    StrCpy $isMapDriveEnabled ${TRUE}
    SectionGetFlags ${SecFluxDataFolderMapping} $0
    IntOp $0 $0 & ${SF_SELECTED} 
    ${If} $0 = 0
        StrCpy $isMapDriveEnabled ${FALSE}
    ${EndIf}
FunctionEnd

; ===================================================================
; -- Map Page -------------------------------------------------------
Function un.RemoveProgramFiles
    Pop $R0 ; Path
    Pop $R1 ; FileName

    ;$0 - File Handle
    ;$1 - Line data
    ;$2 - Line len

    ;Open the file
    FileOpen $0 $R0\$R1 r
    ${DoWhile} $0 <> -1     ; While file is not at the end
        FileRead $0 $1      ; Read one line into $1 (the filename)
        ;Call un.TrimNewlines  ; Need to fix, check the function and fix to use
        ;Pop $1              ; Get cleaned filename

        StrLen $2 $1        ; Check if the line is not empty
        ${If} $2 > 0
            ${If} ${FileExists} "$R0\$1"
                ;;MessageBox MB_OK "Deleting File: '$R0\$1'"
                Delete "$R0\$1"
            ${Else}
                ;MessageBox MB_OK "File Not Found: '$R0\$1'"
            ${EndIf}
        ${Else}
            Goto EndLoop
        ${EndIf}
    ${Loop}
EndLoop:
    
    ; Close the file
    FileClose $0

FunctionEnd

Function un.TrimNewlines ; Need to fix
    Exch $0   ; Input string
    Push $1   ; Last character storage

    StrLen $1 $0
    ${If} $1 > 0
        StrCpy $1 $0 1 -1   ; Get last character
        ${If} $1 == "\r"
            StrCpy $0 $0 -1
        ${EndIf}
        StrCpy $1 $0 1 -1   ; Check again for \n
        ${If} $1 == "\n"
            StrCpy $0 $0 -1
        ${EndIf}
    ${EndIf}

    Exch $0  ; Return cleaned string
FunctionEnd
; -------------------------------------------------------------------