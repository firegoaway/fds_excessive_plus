@echo off
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
cd /d "E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng"
make SHELL="%ComSpec%" VPATH="../../Source/fds5ng" -f ..\makefile5ng fds5_impi_intel_win > build_ng.log 2>&1
echo EXIT_CODE=%errorlevel% >> build_ng.log
