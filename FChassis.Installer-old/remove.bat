@echo off
set "TARGET_BIN=%1"

set "TARGET_BIN=C:\FluxSDK\bin"

echo Remove Started ............................................................
    echo Removing Program and dependent Files
    echo =====================================================

    call :copyFiles %TARGET_BIN%\programfiles.txt %TARGET_BIN%
    call :copyFiles %TARGET_BIN%\dependentFiles.txt %TARGET_BIN%
    echo .

echo Remove Completed ..........................................................
if not %ERRORLEVEL% equ 0 (
pause
}
exit 0

:copyFiles
    for /f "usebackq delims=" %%f in (%1) do (
        echo Deleting %2\%%f
        del "%2\%%f" 
        
        if not %ERRORLEVEL% equ 0 ( :: Alag: if condition is not working 
            echo Failed to delete file %2\%%f.
            pause
            goto :end
        )
    )
exit /b 0
:end