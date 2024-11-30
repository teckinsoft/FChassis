RequestExecutionLevel user  ; Prevent the installer from asking for admin permission

Name "FChassis Installer" 
 
Outfile "..\FChassis_Setup.exe"  ; Sets the output installer file name
InstallDir "C:\FluxSDK"   ; Sets the default installation directory

; Page setup
Page components
Page directory
Page instfiles

; Installer Sections
SectionGroup /e "Installation" ;
    Section "Flux SDK.4 Setup" SecFluxSDKSetup
        ; Run Setup.FluxSDK.4.exe from the NSIS script folder and wait for it to complete
        ExecWait '"files\Setup.FluxSDK.4.exe"'
    SectionEnd
 
    Section "FChassis Installation" SecInstallation
        SetOutPath "$INSTDIR\bin"
        File "C:\FluxSDK\bin\FChassis.exe"
        File "C:\FluxSDK\bin\FChassis.dll"
        File "C:\FluxSDK\bin\FChassis.runtimeconfig.json"
        File "files\FChassis.ico"
        
        ; Copy the entire FChassis folder to C:\FlushSDK\map
        SetOutPath "$INSTDIR"  
        File /r "files\map"  ; Copy all files in the FChassis folder recursively
        
        ; Map C:\FlushSDK\map to W: drive
        nsExec::ExecToLog 'subst W: "$INSTDIR\map"'
		
		; write the subst command to run on startup
WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassisMapWDrive" 'subst W: "$INSTDIR\map"'
        
        ; Create the uninstaller
        WriteUninstaller "$INSTDIR\Uninstall.exe"
    
        ; Create a shortcut for FChassis.exe on the Desktop
        CreateShortCut "$DESKTOP\FChassis.lnk" "$INSTDIR\bin\FChassis.exe" "" "C:\FluxSDK\bin\FChassis.ico" 0
    SectionEnd

SectionGroupEnd
 
; Uninstaller Section
Section "Uninstall"
    ; Remove installed files
    Delete "$INSTDIR\bin\FChassis.exe"
    Delete "$INSTDIR\bin\FChassis.dll"
	Delete "$INSTDIR\bin\FChassis.ico"
	Delete "$INSTDIR\bin\FChassis.runtimeconfig.json"
 
    ; Remove the installation directory (if empty)
    RMDir /r "$INSTDIR\map"
 	
    ; Remove shortcut
    Delete "$DESKTOP\FChassis.lnk" 

	; Remove the registry entry
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FChassisMapWDrive"	

    ; Remove the W: drive mapping
    nsExec::ExecToLog 'subst W: /D'

    ; Remove the uninstaller file itself
    Delete "$INSTDIR\Uninstall.exe" 
SectionEnd
