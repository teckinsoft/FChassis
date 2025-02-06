::@echo off
set "BUILD_DIR=%1"
set "TARGET_BIN=%2"
set PROJECT_DIR=%cd%

    ;echo BUILD_DIR == BUILD_DIR
    ;echo TARGET_BIN == %TARGET_BIN%


echo Build Started ............................................................
    echo Copying -program files- to "files/bin"
    echo =====================================================

    set fileNames=%PROJECT_DIR%\bat\programfiles.txt
    for /f "usebackq delims=" %%f in ("%fileNames%") do (
        echo %TARGET_BIN% %%f %BUILD_DIR%\files\bin
        call :copyFile %TARGET_BIN% %%f %BUILD_DIR%\files\bin
    )
    echo =====================================================
    echo .

    cd %PROJECT_DIR%    
    "C:\Program Files (x86)\NSIS\makensis.exe" "/DPROJECT_DIR=%PROJECT_DIR%" "/DBUILD_DIR=%BUILD_DIR%" "%PROJECT_DIR%\script.nsi"
    :: Capture exit code
    set EXITCODE=%ERRORLEVEL%       

    :: Print exit code for debugging
    echo NSIS Exit Code: %EXITCODE% 
echo Build Completed ..........................................................

:: Return the same exit code
exit /b %EXITCODE% 
::GoTo :end

:copyFile
   copy "%1\%2" "%3"
   echo %2 copied successfully
   echo .
exit /b 0
:end