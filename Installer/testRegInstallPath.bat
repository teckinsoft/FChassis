@echo off
setlocal enabledelayedexpansion

set "regKey=HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FChassis"
set "valueName=InstallLocation"

for /f "skip=2 tokens=1-3" %%A in ('reg query "%regKey%" /v "%valueName%" 2^>nul') do (
    set "installLocation=%%C"
    goto :output
)

echo Error: Registry path or value not found.
exit /b 1

:output
rem Remove surrounding quotes if present
if "!installLocation:~0,1!"=="^"" set "installLocation=!installLocation:~1,-1!"
echo InstallLocation: !installLocation!

rem You can now use the !installLocation! variable for other operations
echo.
echo The full path is: !installLocation!

endlocal