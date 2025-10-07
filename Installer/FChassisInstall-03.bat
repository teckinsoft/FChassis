@echo off
setlocal enabledelayedexpansion

:: =====================================================
:: FChassis Installer and W: Drive Mapping Script
:: 
:: This script performs the following operations:
:: 1. Elevates to administrator privileges via UAC for installation only
:: 2. Runs the FChassisInstaller.exe from the script's directory (elevated)
:: 3. Retrieves the installation directory from the Windows registry (elevated)
:: 4. Launches a user-level instance for drive mapping operations
:: 5. Removes any existing W: drive mapping (user level)
:: 6. Creates a new subst mapping for W: to [InstallDir]\Map (user level)
:: 7. Adds a registry entry to persist the W: mapping (user level)
:: 
:: Prerequisites:
:: - FChassisInstaller.exe must be in the same directory as this script.
:: - Administrator privileges are required for installation only.
:: 
:: Author: Generated Script (Updated)
:: Date: September 18, 2025
:: =====================================================

echo.
echo =====================================================
echo Starting FChassis Installation and W: Drive Mapping
echo =====================================================
echo.

:: Check if this is the user-level mapping phase
if "%~1"=="/mapping" goto :mapping_phase

:: =====================================================
:: Step 1: Check if we need elevation for installation
:: =====================================================
echo Checking if administrator privileges are needed for installation...

set "needElevation=0"
set "exePath=%~dp0FChassisInstaller.exe"

:: Check if we're already elevated
net session >nul 2>&1
if %errorlevel% EQU 0 (
    set "alreadyElevated=1"
    echo Already running with administrator privileges.
) else (
    set "alreadyElevated=0"
    echo Running at user level.
)

:: Check if installer exists and we need to run it
if exist "!exePath!" (
    echo FChassisInstaller.exe found, elevation will be requested for installation.
    set "needElevation=1"
) else (
    echo FChassisInstaller.exe not found, skipping installation phase.
    set "needElevation=0"
)

:: =====================================================
:: Step 2: Elevate for installation if needed
:: =====================================================
if "!needElevation!"=="1" if "!alreadyElevated!"=="0" (
    echo.
    echo Requesting elevation for installation...
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~f0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs" >nul 2>&1
    echo Elevation requested for installation. Please approve the UAC prompt.
    exit /b 0
)

:: =====================================================
:: Step 3: Installation Phase (runs elevated if needed)
:: =====================================================
if "!needElevation!"=="1" (
    echo.
    echo =====================================================
    echo Starting installation phase
    echo =====================================================
    echo.

    if not exist "!exePath!" (
        echo ERROR: FChassisInstaller.exe not found at: !exePath!
        pause
        exit /b 1
    )

    echo Found FChassisInstaller.exe at: !exePath!
    echo Running FChassisInstaller.exe...
    echo (This may take a few moments. Installation progress will be shown.)

    start /wait "" "!exePath!"

    if %errorlevel% NEQ 0 (
        echo.
        echo WARNING: FChassisInstaller.exe returned error code %errorlevel%.
    ) else (
        echo.
        echo FChassisInstaller.exe completed successfully.
    )
    
    :: Wait a moment for registry to be updated
    timeout /t 2 /nobreak >nul
)

:: =====================================================
:: Step 4: Retrieve installation directory from registry
:: =====================================================
echo.
echo Retrieving installation directory from registry...

set "regKey=HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FChassis"
set "valueName=InstallLocation"
set "installDir="

for /f "skip=2 tokens=1-3" %%A in ('reg query "%regKey%" /v "%valueName%" 2^>nul') do (
    set "installLocation=%%C"
    goto :process_install_dir
)

echo.
echo ERROR: FChassis installation not found or incomplete.
echo Please ensure the installer ran successfully.
pause
exit /b 1

:process_install_dir
rem Remove surrounding quotes if present
if "!installLocation:~0,1!"=="^"" set "installLocation=!installLocation:~1,-1!"
set "installDir=!installLocation!"

if "!installDir!"=="" (
    echo.
    echo ERROR: InstallLocation registry value is empty.
    echo Please ensure the installer ran successfully.
    pause
    exit /b 1
)

echo Installation directory found: !installDir!

:: Save install directory to a temporary file for the user-level instance
echo !installDir! > "%temp%\fchassis_install_dir.txt"

:: =====================================================
:: Step 5: Launch user-level instance for drive mapping
:: =====================================================
echo.
echo =====================================================
echo Launching user-level operations for drive mapping
echo =====================================================
echo.

:: Check if we're already at user level
if "!alreadyElevated!"=="1" (
    echo Dropping to user level for drive mapping...
    :: Use runas to launch as current user without elevation
    runas /user:%USERNAME% "cmd /c \"%~f0\" /mapping"
) else (
    echo Already at user level, proceeding with drive mapping...
    "%~f0" /mapping
)

echo.
echo =====================================================
echo Script completed successfully!
echo =====================================================
echo.
pause
exit /b 0

:: =====================================================
:: Mapping Phase (runs at user level)
:: =====================================================
:mapping_phase
echo.
echo =====================================================
echo Starting user-level drive mapping operations
echo =====================================================
echo.

:: Read install directory from temporary file
set "installDir="
if exist "%temp%\fchassis_install_dir.txt" (
    for /f "usebackq delims=" %%A in ("%temp%\fchassis_install_dir.txt") do set "installDir=%%A"
    del "%temp%\fchassis_install_dir.txt" >nul 2>&1
)

if "!installDir!"=="" (
    echo ERROR: Could not retrieve installation directory.
    echo Please run the script again to complete installation first.
    pause
    exit /b 1
)

echo Installation directory: !installDir!
echo.

:: =====================================================
:: Step 6: Remove Existing W: Drive Mapping if Present
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
:: =====================================================
:: Step 7: Create Subst Mapping for W: to [InstallDir]\Map
:: =====================================================
set "mapPath=!installDir!\Map"

echo Creating W: subst mapping to: !mapPath!

if not exist "!mapPath!" (
    echo.
    echo WARNING: Map directory does not exist: !mapPath!
    echo Please ensure the installation completed successfully.
    pause
    exit /b 1
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
:: =====================================================
:: Step 8: Persist W: Mapping in Registry for Reboots/Relogins
:: =====================================================
echo Adding registry entry to persist W: mapping on logon...

set "substCmd=subst W: \"!mapPath!\""

reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" /v "FChassis_W_Mapping" /t REG_SZ /d "!substCmd!" /f >nul 2>&1
if %errorlevel% EQU 0 (
    echo Registry entry added successfully. W: will be remapped on next logon.
) else (
    echo.
    echo ERROR: Failed to add registry entry for persistence.
)

echo.
:: =====================================================
:: Step 9: Verification
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
echo =====================================================
echo User-level operations completed successfully!
echo =====================================================
echo.
echo Summary:
echo - Install Dir: !installDir!
echo - W: Mapped to: !mapPath!
echo - Persistence: Enabled via Registry
echo.
echo Press any key to exit...
pause >nul
exit /b 0
exit /b 0