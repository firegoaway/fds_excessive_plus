@echo off
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
cd /d "E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng"
set I_MPI_ROOT=D:\Intel\oneAPI\mpi\latest
ifx -o fds5_impi_intel_win.exe -D_WIN32 /Qipo /O2 /I"%I_MPI_ROOT%\include\mpi" /I"%I_MPI_ROOT%\include" /wrap-margin- /Qopenmp /F1000000000 prec.obj cons.obj type.obj mesh.obj func.obj pois.obj turb.obj irad.obj radi.obj scrc.obj smvv.obj mpip.obj isob.obj devc.obj cut_face.obj part.obj vege.obj ctrl.obj ieva.obj evac.obj evac_plus.obj hvac.obj mass.obj wall.obj fire.obj velo.obj pres.obj init.obj dump.obj read.obj divg.obj main.obj "%I_MPI_ROOT%\lib\impi.lib" "D:\Intel\oneAPI\mkl\latest\lib\intel64\mkl_intel_lp64.lib" "D:\Intel\oneAPI\mkl\latest\lib\intel64\mkl_core.lib" "D:\Intel\oneAPI\mkl\latest\lib\intel64\mkl_sequential.lib"
echo LINK_EXIT_CODE=%errorlevel%
