@echo off
set arg1=%1

:: Setup Intel compiler environment (skip if already set, e.g. when the caller
:: ran "D:\Intel\oneAPI\setvars.bat" before invoking this script)
if defined I_MPI_ROOT if defined MKLROOT goto env_ok
echo.
echo Setting up Intel OneAPI environment...
call "D:\Intel\oneAPI\setvars.bat" intel64
:env_ok

Title Building FDS5NG (Intel MPI) for 64 bit Windows

echo.
echo ========================================
echo  Building FDS 5.5.3 NG (fds5ng) - R29 series
echo ========================================
echo.

make SHELL="%ComSpec%" VPATH="../../Source/fds5ng" -f ..\makefile5ng fds5_impi_intel_win
if %errorlevel% neq 0 (
    echo.
    echo *** ERROR: FDS5NG build failed ***
    echo.
    if x%arg1% == xbot goto endif2
    pause
    goto :eof
)

echo.
echo ========================================
echo  FDS5NG Build Successful!
echo ========================================
echo.

if x%arg1% == xbot goto endif2
pause
:endif2
