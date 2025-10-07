@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ---[ 0) Prepare temp files ]---
set "_TMP_DIR=%TEMP%\MapW_Tmp"
set "_PATH_FILE=%_TMP_DIR%\path.txt"
set "_SID_FILE=%_TMP_DIR%\sid.txt"
set "_FLAG_ELEV=%_TMP_DIR%\elevated.flag"
if not exist "%_TMP_DIR%" mkdir "%_TMP_DIR%" >nul 2>&1

rem =====================================================================================
rem 1) Ask user for a folder (GUI pick). This runs in user context (no admin required).
rem =====================================================================================
if /i not "%~1"=="elevated" (
  echo.
  echo Select the folder for drive FChassis data (it will be mapped to W:).

  for /f "usebackq delims=" %%I in (`
    powershell -NoProfile -STA -ExecutionPolicy Bypass ^
      "$sh=New-Object -ComObject Shell.Application; $f=$sh.BrowseForFolder(0,'Select the folder for drive FChassis data, It will be mapped to W:',0); if($f){$p=$f.Self.Path} else {exit 1}; [Console]::WriteLine($p)"
  `) do (
    set "MAP_TARGET=%%I"
  )

  if not defined MAP_TARGET (
    echo No folder selected. Exiting.
    rmdir /s /q "%_TMP_DIR%" >nul 2>&1
    exit /b 1
  )

  rem --- Normalize trailing backslash
  if "%MAP_TARGET:~-1%"=="\" set "MAP_TARGET=%MAP_TARGET:~0,-1%"

  rem --- Ensure we end up at ...\FChassis (case-insensitive, no double-append)
  for %%A in ("%MAP_TARGET%") do set "LASTSEG=%%~nxA"
  if /i not "%LASTSEG%"=="FChassis" set "MAP_TARGET=%MAP_TARGET%\FChassis"

  rem --- Create the FChassis subfolder if missing
  if not exist "%MAP_TARGET%\" (
    mkdir "%MAP_TARGET%" 2>nul
    if errorlevel 1 (
      echo Failed to create "%MAP_TARGET%".
      rmdir /s /q "%_TMP_DIR%" >nul 2>&1
      exit /b 1
    )
  )

  rem --- Persist chosen path & user SID for the elevated phase
  >"%_PATH_FILE%" echo %MAP_TARGET%
  for /f "usebackq delims=" %%S in (`
    powershell -NoProfile -ExecutionPolicy Bypass ^
      "[System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value"
  `) do (
    >"%_SID_FILE%" echo %%S
  )

  rem =========================
  rem 2) Map W: for this session
  rem =========================
  subst W: /d >nul 2>&1
  subst W: "%MAP_TARGET%"
  if errorlevel 1 (
    echo Failed to map W: to "%MAP_TARGET%".
    rmdir /s /q "%_TMP_DIR%" >nul 2>&1
    exit /b 2
  )
  echo Mapped W: -> "%MAP_TARGET%"

  rem =========================
  rem 3) Elevate to admin (UAC)
  rem =========================
  powershell -NoProfile -ExecutionPolicy Bypass ^
    "Start-Process -FilePath '%~f0' -ArgumentList 'elevated' -Verb RunAs"
  if errorlevel 1 (
    echo Elevation cancelled. Leaving W: mapped for this session only.
    exit /b 3
  )
  exit /b 0
)


rem =====================================================================================
rem [ELEVATED BRANCH] 4) Per-user persistence via HKCU\...\Run for the ORIGINAL USER
rem =====================================================================================
if not exist "%_PATH_FILE%" (
  echo Missing path file; cannot proceed.
  exit /b 10
)
if not exist "%_SID_FILE%" (
  echo Missing SID file; cannot proceed.
  exit /b 11
)

set /p "ELEV_PATH="<"%_PATH_FILE%"
set /p "ORIG_SID="<"%_SID_FILE%"

rem Write into the original user's hive (HKU\<SID>\Software\...\Run)
rem Use REG_EXPAND_SZ and run "cmd /c subst W: "path"" at every logon
set "RUNKEY=HKU\%ORIG_SID%\Software\Microsoft\Windows\CurrentVersion\Run"
reg add "%RUNKEY%" /v "MapW" /t REG_EXPAND_SZ /d "cmd.exe /c subst W: \"%ELEV_PATH%\"" /f >nul
if errorlevel 1 (
  echo Failed to write per-user Run entry under %RUNKEY%.
  echo You can add it manually:  cmd /c subst W: "%ELEV_PATH%"
  goto :AFTER_REG
)

echo Created per-user persistent mapping entry:
echo   %RUNKEY%  REG_EXPAND_SZ  "cmd /c subst W: ""%ELEV_PATH%"""

:AFTER_REG
rem =====================================================================================
rem 5) Prompt OK and run installer.exe (located next to this .bat)
rem =====================================================================================
powershell -NoProfile -ExecutionPolicy Bypass ^
  "[void][Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms');" ^
  "[System.Windows.Forms.MessageBox]::Show('Drive W: is set to: %ELEV_PATH%`n`nClick OK to start the installer.','Ready to Install',[System.Windows.Forms.MessageBoxButtons]::OK,[System.Windows.Forms.MessageBoxIcon]::Information)" >nul

set "INSTALLER=%~dp0installer.exe"
if not exist "%INSTALLER%" (
  echo Cannot find installer.exe next to this script:
  echo   %INSTALLER%
  echo Aborting.
  exit /b 20
)

echo Launching installer: "%INSTALLER%"
start "" "%INSTALLER%"

rem Cleanup temp files (keep them if you want for troubleshooting)
del /q "%_PATH_FILE%" "%_SID_FILE%" 2>nul
rmdir /s /q "%_TMP_DIR%" >nul 2>&1

exit /b 0
