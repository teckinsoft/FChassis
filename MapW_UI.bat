@echo off
setlocal

:: =============================================================
::  PURE BATCH: Map W: to any folder + make it survive reboots
::  Run ONCE as Administrator
:: =============================================================

title Map W: to Folder (Pure Batch)

:: ------------------- 1. Pick folder -------------------
echo.
echo Select the folder you want as W:
for /f "usebackq delims=" %%A in (`powershell -noprofile -command ^
    "($f=(New-Object -ComObject Shell.Application).BrowseForFolder(0,'Pick folder for W:',0,0)).Self.Path" ^
    ^| findstr /v "^$"`) do set "TARGET=%%A"

if "%TARGET%"=="" (
    echo.
    echo You cancelled or picked nothing.
    pause & exit /b 1
)

:: Clean trailing slash
if "%TARGET:~-1%"=="\" set "TARGET=%TARGET:~0,-1%"
echo.
echo You chose: "%TARGET%"
echo.

:: ------------------- 2. Temp map -------------------
subst W: "%TARGET%" >nul
if errorlevel 1 (
    echo ERROR: W: is already in use:
    subst | findstr /i "W:"
    echo.
    echo Delete it first:  subst W: /D
    pause & exit /b 1
)
echo W: is now mapped (temporary)
echo.

:: ------------------- 3. Make PERMANENT -------------------
echo Making W: permanent across reboots...
echo.

:: Build registry command (pure batch)
set "KEY=HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices"
set "VAL=\??\%TARGET%"

:: Double every backslash
set "REGVAL="
for %%P in ("%VAL%") do (
    set "PART=%%~P"
    setlocal enabledelayedexpansion
    set "REGVAL=!REGVAL!!PART:\=\\!"
    endlocal&set "REGVAL=%REGVAL%"
)

:: Write to registry (elevated)
 >"%TEMP%\persistW.reg" echo Windows Registry Editor Version 5.00
>>"%TEMP%\persistW.reg" echo.
>>"%TEMP%\persistW.reg" echo [%KEY%]
>>"%TEMP%\persistW.reg" echo "W:"="%REGVAL%"

:: Run reg.exe elevated
powershell -command "Start-Process reg.exe -Verb runAs -ArgumentList 'import','%TEMP%\persistW.reg'"

echo.
echo If you clicked YES on UAC, W: is now PERMANENT.
echo.
echo Test: close this window, open new CMD, type  W:
echo.
pause
exit /b 0