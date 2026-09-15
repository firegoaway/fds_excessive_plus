@echo off
set arg1=%1

:: setup Intel compiler environment directly
echo.
echo Setting up Intel OneAPI environment...
call "D:\Intel\oneAPI\setvars.bat" intel64

Title Building FDS5 (Intel MPI) for 64 bit Windows

echo.
echo ========================================
echo  Building FDS 5.5.3 with FDS6 Compatibility
echo ========================================
echo.

make SHELL="%ComSpec%" VPATH="../../Source/fds5" -f ..\makefile5 fds5_impi_intel_win
if %errorlevel% neq 0 (
    echo.
    echo *** ERROR: FDS5 build failed ***
    echo.
    if x%arg1% == xbot goto endif2
    pause
    goto :eof
)

echo.
echo ========================================
echo  FDS5 Build Successful!
echo ========================================
echo.

if x%arg1% == xbot goto endif2
pause
:endif2
