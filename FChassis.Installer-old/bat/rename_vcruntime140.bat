@echo off
:: Check if script is running as administrator
>nul 2>&1 "%windir%\system32\cacls.exe" "%windir%\system32\config\system"
if '%errorlevel%' NEQ '0' (
    echo.
    echo This script must be run as administrator!
    echo Right-click and choose "Run as administrator".
    pause
    exit /b
)

echo Forcibly renaming DLL files in System32...
cd /d C:\Windows\System32

:: vccorlib140.dll -> vccorlib140_app.dll
if exist vccorlib140.dll (
    if exist vccorlib140_app.dll (
        del /f /q vccorlib140_app.dll
        echo Deleted existing vccorlib140_app.dll
    )
    ren vccorlib140.dll vccorlib140_app.dll
    echo Renamed vccorlib140.dll to vccorlib140_app.dll
) else (
    echo vccorlib140.dll not found.
)

:: vcruntime140.dll -> vcruntime140_app.dll
if exist vcruntime140.dll (
    if exist vcruntime140_app.dll (
        del /f /q vcruntime140_app.dll
        echo Deleted existing vcruntime140_app.dll
    )
    ren vcruntime140.dll vcruntime140_app.dll
    echo Renamed vcruntime140.dll to vcruntime140_app.dll
) else (
    echo vcruntime140.dll not found.
)

:: msvcp140.dll -> msvcp140_APP.dll
if exist msvcp140.dll (
    if exist msvcp140_APP.dll (
        del /f /q msvcp140_APP.dll
        echo Deleted existing msvcp140_APP.dll
    )
    ren msvcp140.dll msvcp140_APP.dll
    echo Renamed msvcp140.dll to msvcp140_APP.dll
) else (
    echo msvcp140.dll not found.
)

echo.
echo Done.
pause
