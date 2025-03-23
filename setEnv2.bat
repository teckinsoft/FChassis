@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

REM Set all OCCT related library and include paths
setx DRACO_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\include" /M
setx DRACO_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\lib" /M
setx FFMPEG_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\include" /M
setx FFMPEG_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\lib" /M
setx FREEIMAGE_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\include" /M
setx FREEIMAGE_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\lib" /M
setx FREETYPE_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\include" /M
setx FREETYPE_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\lib" /M
setx OCCT_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\inc" /M
setx OCCT_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\opencascade-7.7.0\win64\vc14\lib" /M
setx OCCT_TCL_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\include" /M
setx OCCT_TCL_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\lib" /M
setx OPENVR_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\headers" /M
setx OPENVR_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\lib\win64" /M
setx QT_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\include" /M
setx QT_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\lib" /M
setx TBB_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\include" /M
setx TBB_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\lib" /M
setx TCLTK_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\include" /M
setx TCLTK_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\lib" /M
setx VTK_INCLUDE "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\include\vtk-6.1" /M
setx VTK_LIB "C:\OCCT\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\lib" /M
setx WINDOWS_SDK_PATH "C:\Program Files (x86)\Windows Kits\10\Lib\10.0.26100.0\um\x64" /M

echo All environment variables have been set persistently.

REM Take backup
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path > path_backup.txt
echo PATH variable backed up in the PWD

REM Get the current system PATH
for /f "tokens=2,* delims= " %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path ^| findstr Path') do set "CURRENT_PATH=%%B"

REM Paths to be added to PATH variable
set "NEW_PATHS=C:\OCCT\OpenCASCADE-7.7.0-vc14-64\draco-1.4.1-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\ffmpeg-3.3.4-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\freeimage-3.17.0-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\freetype-2.5.5-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\openvr-1.14.15-64\bin\win64"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\qt5.11.2-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\rapidjson-1.1.0\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tbb_2021.5-vc14-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\tcltk-86-64\bin"
set "NEW_PATHS=!NEW_PATHS!;C:\OCCT\OpenCASCADE-7.7.0-vc14-64\vtk-6.1.0-vc14-64\bin"

REM Append new paths only if they are not already present
for %%P in (!NEW_PATHS!) do (
    echo !CURRENT_PATH! | find /i "%%P" >nul
    if errorlevel 1 set "CURRENT_PATH=!CURRENT_PATH!;%%P"
)

REM Use reg add to persist the new PATH (appended, not replaced)
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path /t REG_EXPAND_SZ /d "!CURRENT_PATH!" /f

echo Paths successfully added to the system PATH variable persistently.
endlocal
pause