@echo off
set MSYS_NO_PATHCONV=1
set MSYS2_ARG_CONV_EXCL=*
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
cd /d "E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng"
echo === LINK START %date% %time% === > link_log.txt
make SHELL=cmd.exe VPATH="../../Source/fds5ng" -f ..\makefile5ng fds5_impi_intel_win >> link_log.txt 2>&1
echo BUILD_EXIT_CODE=%errorlevel% >> link_log.txt
echo === LINK END %date% %time% === >> link_log.txt
