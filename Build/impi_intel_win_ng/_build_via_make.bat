@echo off
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
set I_MPI_ROOT=D:\Intel\oneAPI\mpi\2021.17
cd /d "E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng"
echo === BUILD START %date% %time% === > build_log.txt
echo I_MPI_ROOT=%I_MPI_ROOT% >> build_log.txt
call make_fds5.bat bot >> build_log.txt 2>&1
echo BUILD_EXIT_CODE=%errorlevel% >> build_log.txt
echo === BUILD END %date% %time% === >> build_log.txt
