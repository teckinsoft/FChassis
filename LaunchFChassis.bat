@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

:: Define required paths for local use only
set "OCCT_PATHS=C:\FluxSDK\OCCT\opencascade-7.7.0\win64\vc14\bin;C:\FluxSDK\OCCT\draco-1.4.1-vc14-64\bin;C:\FluxSDK\OCCT\ffmpeg-3.3.4-64\bin;C:\FluxSDK\OCCT\freeimage-3.17.0-vc14-64\bin;C:\FluxSDK\OCCT\freetype-2.5.5-vc14-64\bin;C:\FluxSDK\OCCT\openvr-1.14.15-64\bin\win64;C:\FluxSDK\OCCT\qt5.11.2-vc14-64\bin;C:\FluxSDK\OCCT\rapidjson-1.1.0\bin;C:\FluxSDK\OCCT\tbb_2021.5-vc14-64\bin;C:\FluxSDK\OCCT\tcltk-86-64\bin;C:\FluxSDK\OCCT\vtk-6.1.0-vc14-64\bin;C:\FluxSDK\OCCT\opencascade-7.7.0\win64\vc14\bin"

set "NEW_PATH=%OCCT_PATHS%;C:\FluxSDK\Bin;C:\Windows\System32"

:: Temporarily prepend NEW_PATH to current PATH
set "PATH=%NEW_PATH%;%PATH%"

:: Verify PATH length (optional)
echo PATH length: %PATH:~0,8000%...
echo.

:: Change to executable directory and run FChassis.exe
cd /d "C:\FluxSDK\Bin"
echo Running FChassis.exe...
start "" "FChassis.exe"

ENDLOCAL
pause
