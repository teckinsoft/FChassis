@echo off
setlocal enabledelayedexpansion
:: =============================================================
:: PURE BATCH: Map W: to any folder + make it survive reboots
:: Run ONCE as Administrator
:: Usage: MapToWDrive.bat "C:\Path\To\Folder"
:: =============================================================
title Map W: to Folder (Pure Batch)

:: ------------------- 1. Check parameter -------------------
if "%~1"=="" (
    echo.
    echo Usage: %~nx0 "C:\Path\To\Folder"
    echo.
    echo Example: %~nx0 "C:\MyProjects"
    echo Example: %~nx0 "D:\Work\Documents"
    echo.
    pause
    exit /b 1
)

set "TARGET=%~1"

:: ------------------- 2. Validate the path -------------------
echo.
echo Validating path: "%TARGET%"

:: Clean and normalize path first
if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"
for %%A in ("%TARGET%") do set "TARGET=%%~fA"

:: Check if path exists and is accessible
if not exist "%TARGET%\" (
    echo ERROR: Path does not exist or is inaccessible: "%TARGET%"
    echo.
    pause
    exit /b 1
)

:: Check if it's a directory
if not exist "%TARGET%\*" (
    echo ERROR: "%TARGET%" is not a valid folder.
    echo.
    pause
    exit /b 1
)

:: Quick check for obvious issues - no quotes allowed
if not "%TARGET:""=%"=="%TARGET%" (
    echo ERROR: Path cannot contain double quotes.
    echo.
    pause
    exit /b 1
)

echo.
echo Valid path: "%TARGET%"
echo.

:: ------------------- 3. Check and map temporarily -------------------
:: Check if W: is already mapped
subst | findstr /i "^W:" >nul
if errorlevel 1 (
    :: Not mapped, just map new
    goto :map_new
) else (
    :: Mapped, get current path
    for /f "tokens=2 delims=>" %%B in ('subst ^| findstr /i "^W:"') do (
        set "CURRENT=%%B"
        :: Clean up current path (trim spaces, remove trailing slash)
        for /f "tokens=* delims= " %%C in ("!CURRENT!") do set "CURRENT=%%C"
        if "!CURRENT:~-1!"=="\" set "CURRENT=!CURRENT:~0,-1!"
    )
    :: Compare
    if /i "!CURRENT!"=="%TARGET%" (
        echo.
        echo Path "%TARGET%" is already mapped to W:
        echo.
        goto :make_permanent
    ) else (
        :: Different, unmap old
        echo.
        echo Unmapping existing W: from "!CURRENT!"
        subst W: /D >nul
        if errorlevel 1 (
            echo ERROR: Failed to unmap W:
            pause & exit /b 1
        )
    )
)

:map_new
:: Map new
subst W: "%TARGET%" >nul
if errorlevel 1 (
    echo ERROR: Failed to map W: to "%TARGET%"
    echo Possible reasons:
    echo   - W: is in use by another device
    echo   - Path contains invalid characters
    echo   - Insufficient privileges
    echo.
    pause & exit /b 1
)
echo.
echo W: is now mapped (temporary) to "%TARGET%"
echo.

:make_permanent
:: ------------------- 4. Make PERMANENT -------------------
echo Making W: permanent across reboots...
echo.

:: Prepare registry value - critical format for DOS Devices
set "VAL=\??\%TARGET%"

:: For .reg file, we need to double backslashes
set "REGVAL=%VAL:\=\\%"

:: Write registry file (using delayed expansion to avoid % issues)
setlocal enabledelayedexpansion
>"%TEMP%\persistW.reg" echo Windows Registry Editor Version 5.00
>>"%TEMP%\persistW.reg" echo.
>>"%TEMP%\persistW.reg" echo [HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices]
>>"%TEMP%\persistW.reg" echo "W:"="!REGVAL!"
endlocal

:: Display what we're writing for debugging
echo Writing to registry: W: = "%VAL%"
echo.

:: Import registry file
reg import "%TEMP%\persistW.reg" >nul 2>&1
if errorlevel 1 (
    :: Try with elevation
    echo Need administrator rights to write to registry...
    powershell -Command "Start-Process -FilePath 'reg.exe' -ArgumentList 'import', '\"%TEMP%\persistW.reg\"' -Verb RunAs -Wait"
    if errorlevel 1 (
        echo Failed to write to registry. Run as Administrator.
        pause
        exit /b 1
    )
)

echo Registry updated successfully!
echo.
echo W: is now PERMANENTLY mapped to "%TARGET%"
echo.
echo Note: The permanent mapping will take effect after reboot.
echo For now, W: is temporarily mapped (active this session).
echo.
echo To test: Close this window, open new CMD, type: W:
echo To remove: Delete registry key: HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices "W:"
echo.
pause
exit /b 0