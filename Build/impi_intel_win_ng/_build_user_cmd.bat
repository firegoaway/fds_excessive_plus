@echo off
set MSYS_NO_PATHCONV=1
set MSYS2_ARG_CONV_EXCL=*
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
cd /d "E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng"
call make_fds5.bat bot
