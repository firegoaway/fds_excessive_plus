@echo off
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
cd /d "E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng"
del /Q *.obj *.mod *.exe 2>nul
call make_fds5.bat bot
