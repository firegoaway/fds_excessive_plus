@echo off
echo === CLEAN BUILD START %DATE% %TIME% ===
echo ========================================
echo Building FDS 5.5.3 NG (fds5ng) - R29 series
echo ========================================

call "D:\Intel\oneAPI\setvars.bat"
if errorlevel 1 (
    echo ERROR: Failed to initialize Intel oneAPI environment
    pause
    exit /b 1
)

cd /d "%~dp0"

echo Cleaning old objects...
del /Q *.obj *.mod *.exe 2>nul

echo Building via make_fds5.bat...
call make_fds5.bat

if exist fds5_impi_intel_win.exe (
    echo.
    echo === BUILD SUCCESSFUL ===
    echo Binary: %CD%\fds5_impi_intel_win.exe
    echo Checking dependencies...
    dumpbin /dependents fds5_impi_intel_win.exe | findstr /i ".dll"
) else (
    echo.
    echo *** ERROR: FDS5NG build failed ***
)

echo === BUILD EXIT CODE: %ERRORLEVEL% ===
pause
