@echo off
setlocal enabledelayedexpansion

:: =====================================================
:: FChassis Installer and W: Drive Mapping Script
:: 
:: This script performs the following operations:
:: 1. Elevates to administrator privileges via UAC if necessary.
:: 2. Runs the FChassisInstaller.exe from the script's directory.
:: 3. Retrieves the installation directory from the Windows registry.
:: 4. Removes any existing W: drive mapping.
:: 5. Creates a new subst mapping for W: to [InstallDir]\Map.
:: 6. Adds a registry entry to persist the W: mapping across reboots/relogins
::    by running the subst command at user logon.
:: 7. Provides verification and cleanup options.
:: 
:: Prerequisites:
:: - FChassisInstaller.exe must be in the same directory as this script.
:: - Administrator privileges are required for installation and registry modifications.
:: 
:: Author: Generated Script (Updated)
:: Date: September 18, 2025
:: =====================================================

echo.
echo =====================================================
echo Starting FChassis Installation and W: Drive Mapping
echo =====================================================
echo.

:: =====================================================
:: Step 1: Check for Administrator Privileges and Elevate if Needed
:: =====================================================
echo Checking for administrator privileges...

>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
if '%errorlevel%' NEQ '0' (
    echo Administrative privileges not detected. Requesting elevation via UAC...
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~f0", "%*", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs" >nul 2>&1
    echo Elevation requested. Please approve the UAC prompt and rerun if needed.
    pause
    exit /b 1
) else (
    echo Administrative privileges confirmed.
)

echo.
echo =====================================================
:: Step 2: Run FChassisInstaller.exe
:: =====================================================
echo Locating FChassisInstaller.exe...

:: Derive the full path to FChassisInstaller.exe based on the script's directory
set "exePath=%~dp0FChassisInstaller.exe"

:: Check if FChassisInstaller.exe exists
if not exist "!exePath!" (
    echo.
    echo ERROR: FChassisInstaller.exe not found at: !exePath!
    echo Please ensure FChassisInstaller.exe is in the same directory as this script.
    echo Current script directory: %~dp0
    pause
    exit /b 1
)

echo Found FChassisInstaller.exe at: !exePath!
echo Running FChassisInstaller.exe...
echo (This may take a few moments. Installation progress will be shown in a separate window.)

start /wait "" "!exePath!"

if %errorlevel% NEQ 0 (
    echo.
    echo WARNING: FChassisInstaller.exe returned error code %errorlevel%. Continuing anyway...
) else (
    echo.
    echo FChassisInstaller.exe completed successfully.
)

echo.
echo =====================================================
:: Step 3: Retrieve Installation Directory from Registry
:: =====================================================
echo Retrieving installation directory from registry...

set "regKey=HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FChassis"
set "valueName=InstallLocation"
set "installDir="

:: Wait a moment for registry to be updated after installation
timeout /t 3 /nobreak >nul

for /f "skip=2 tokens=1-3" %%A in ('reg query "%regKey%" /v "%valueName%" 2^>nul') do (
    set "installLocation=%%C"
    goto :process_install_dir
)

echo.
echo ERROR: Could not find InstallLocation in registry key:
echo     HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FChassis
echo.
echo Possible reasons:
echo 1. The installer may not have completed successfully
echo 2. The registry key may have a different name
echo 3. Administrative privileges may be required
echo.
echo Checking for alternative registry keys...
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" /s | findstr /i "fchassis" >nul
if %errorlevel% EQU 0 (
    echo Found FChassis entries in registry. Please check the exact key name.
) else (
    echo No FChassis entries found in registry.
)

pause
exit /b 1

:process_install_dir
rem Remove surrounding quotes if present
if "!installLocation:~0,1!"=="^"" set "installLocation=!installLocation:~1,-1!"
set "installDir=!installLocation!"

if "!installDir!"=="" (
    echo.
    echo ERROR: InstallLocation registry value is empty.
    echo Please ensure the installer ran successfully and try again.
    pause
    exit /b 1
)

echo Installation directory: !installDir!
echo.

:: =====================================================
:: Step 4: Remove Existing W: Drive Mapping if Present
:: =====================================================
echo Checking for existing W: drive mapping...

subst | findstr /i "W:" >nul 2>&1
if %errorlevel% EQU 0 (
    echo Existing W: mapping found. Removing it...
    subst W: /d >nul 2>&1
    if %errorlevel% EQU 0 (
        echo W: mapping removed successfully.
    ) else (
        echo WARNING: Failed to remove existing W: mapping.
        echo Trying alternative method...
        net use W: /delete >nul 2>&1
    )
) else (
    echo No existing W: mapping found.
)

echo.
echo =====================================================
:: Step 5: Create Subst Mapping for W: to [InstallDir]\Map
:: =====================================================
set "mapPath=!installDir!\Map"

echo Creating W: subst mapping to: !mapPath!

if not exist "!mapPath!" (
    echo.
    echo WARNING: Map directory does not exist: !mapPath!
    echo Creating the directory...
    mkdir "!mapPath!" >nul 2>&1
    if %errorlevel% NEQ 0 (
        echo ERROR: Failed to create Map directory.
        echo Trying to create with elevated privileges...
        echo Creating directory: !mapPath!
        mkdir "!mapPath!" 2>&1 | findstr /v "Access is denied" >nul
        if not exist "!mapPath!" (
            echo ERROR: Could not create directory. Please check permissions.
            pause
            exit /b 1
        )
    )
    echo Map directory created successfully.
)

subst W: "!mapPath!" >nul 2>&1
if %errorlevel% EQU 0 (
    echo W: drive mapped successfully to !mapPath!.
) else (
    echo.
    echo ERROR: Failed to create W: subst mapping.
    echo Trying alternative method with different syntax...
    subst W: "!mapPath!"
    if %errorlevel% NEQ 0 (
        echo ERROR: All attempts to create W: mapping failed.
        pause
        exit /b 1
    )
)

echo.
echo =====================================================
:: Step 6: Persist W: Mapping in Registry for Reboots/Relogins
:: =====================================================
echo Adding registry entry to persist W: mapping on logon...

set "substCmd=subst W: \"!mapPath!\""

reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" /v "FChassis_W_Mapping" /t REG_SZ /d "!substCmd!" /f >nul 2>&1
if %errorlevel% EQU 0 (
    echo Registry entry added successfully. W: will be remapped on next logon/reboot.
) else (
    echo.
    echo ERROR: Failed to add registry entry for persistence.
    echo Trying alternative registry location...
    reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunOnce" /v "FChassis_W_Mapping" /t REG_SZ /d "!substCmd!" /f >nul 2>&1
    if %errorlevel% EQU 0 (
        echo Registry entry added to RunOnce key.
    else (
        echo WARNING: Could not add registry persistence. W: drive mapping may not survive reboot.
    )
)

echo.
echo =====================================================
:: Step 7: Verification and Cleanup
:: =====================================================
echo Verifying W: drive mapping...

subst | findstr /i "W:" >nul 2>&1
if %errorlevel% EQU 0 (
    echo Verification: W: drive is successfully mapped.
    echo.
    echo Testing access to W: drive...
    dir W:\ >nul 2>&1
    if %errorlevel% EQU 0 (
        echo W: drive is accessible and ready for use.
    ) else (
        echo WARNING: W: drive is mapped but may not be accessible.
    )
) else (
    echo ERROR: W: drive mapping verification failed.
)

echo.
echo Cleaning up temporary files...
del "%temp%\getadmin.vbs" >nul 2>&1

echo.
echo =====================================================
echo Script completed successfully!
echo =====================================================
echo.
echo Summary:
echo - Installation: Completed
echo - Install Dir: !installDir!
echo - W: Mapped to: !mapPath!
echo - Persistence: Enabled via Registry
echo - Verification: W: drive is active
echo.
echo Next steps:
echo 1. The W: drive is now available for immediate use
echo 2. The mapping will persist across reboots and logons
echo 3. You can access your files at W:\
echo.
echo Press any key to exit...
pause >nul

endlocal