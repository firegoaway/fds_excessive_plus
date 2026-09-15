@echo off
set arg1=%1

for %%I in (.) do set TARGET=%%~nxI

:: setup compiler environment
if x%arg1% == xbot goto endif1
call ..\Scripts\setup_intel_compilers.bat
:endif1

Title Building FDS5 Debug (Intel MPI) for 64 bit Windows

echo.
echo ========================================
echo  Building FDS 5.5.3 DEBUG with FDS6 Compatibility
echo ========================================
echo.

make SHELL="%ComSpec%" VPATH="../../Source/fds5" -f ..\makefile5 fds5_impi_intel_win_db
if %errorlevel% neq 0 (
    echo.
    echo *** ERROR: FDS5 debug build failed ***
    echo.
    if x%arg1% == xbot goto endif2
    pause
    goto :eof
)

echo.
echo ========================================
echo  FDS5 Debug Build Successful!
echo ========================================
echo.

if x%arg1% == xbot goto endif2
pause
:endif2
