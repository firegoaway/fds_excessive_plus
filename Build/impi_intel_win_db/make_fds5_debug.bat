@echo off
:: Отладочная сборка в отдельном каталоге (не затирает release-объекты impi_intel_win)
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
make SHELL="%ComSpec%" VPATH="../../Source/fds5" -f ..\makefile5 fds5_impi_intel_win_db
if %errorlevel% neq 0 (
    echo *** ERROR: FDS5 debug build failed ***
    exit /b 1
)
echo FDS5 Debug Build Successful!
