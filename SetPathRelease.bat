:: This script is for the end release user to set paths for running FChassis.exe
:: << This IS NOT FOR developers >>
:: -----------------------------------------------------------------------------
@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

:: Check if script is running as administrator
>nul 2>&1 "%windir%\system32\cacls.exe" "%windir%\system32\config\system"
if '%errorlevel%' NEQ '0' (
    echo.
    echo This script must be run as administrator!
    echo Right-click and choose "Run as administrator".
    pause
    exit /b
)

:: Backup current PATH
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path > path_backup.txt
echo PATH variable backed up in path_backup.txt

:: Get current system PATH
for /f "tokens=2,* delims= " %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path ^| findstr Path') do set "CURRENT_PATH=%%B"

:: Define new paths to add
set "NEW_PATHS=C:\FluxSDK\OCCT\draco-1.4.1-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\ffmpeg-3.3.4-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\freeimage-3.17.0-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\freetype-2.5.5-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\openvr-1.14.15-64\bin\win64"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\qt5.11.2-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\rapidjson-1.1.0\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\tbb_2021.5-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\tcltk-86-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\vtk-6.1.0-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\FluxSDK\OCCT\opencascade-7.7.0\win64\vc14\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\Windows\System32"

:: Append new paths if they're not already present
for %%P in (!NEW_PATHS!) do (
    echo !CURRENT_PATH! | find /i "%%P" >nul
    if errorlevel 1 set "CURRENT_PATH=!CURRENT_PATH!;%%P"
)

:: Persist updated PATH
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path /t REG_EXPAND_SZ /d "!CURRENT_PATH!" /f
echo Paths successfully updated persistently.

:: DLL file renaming
cd /d C:\Windows\System32
echo Renaming DLL files in System32...

:: Array of old and new DLL names
set "DLLS=vccorlib140.dll:vccorlib140_app.dll vcruntime140.dll:vcruntime140_app.dll msvcp140.dll:msvcp140_APP.dll"

for %%D in (%DLLS%) do (
    for /f "tokens=1,2 delims=:" %%X in ("%%D") do (
        if exist %%X (
            if exist %%Y (
                del /f /q %%Y
                echo Deleted existing %%Y
            )
            ren %%X %%Y
            echo Renamed %%X to %%Y
        ) else (
            echo %%X not found.
        )
    )
)

echo.
echo All operations completed successfully.
endlocal
pause
