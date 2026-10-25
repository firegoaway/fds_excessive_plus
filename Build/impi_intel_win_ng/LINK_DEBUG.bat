@echo off
echo === DEBUG LINK START %DATE% %TIME% ===
call "D:\Intel\oneAPI\setvars.bat" intel64 >nul 2>&1
cd /d "%~dp0"
set I_MPI_ROOT=D:\Intel\oneAPI\mpi\latest
set MKLLIBDIR=D:\Intel\oneAPI\mkl\latest\lib
ifx -o fds5_impi_intel_win_db.exe -D_WIN32 /Od /nologo /debug:all /Z7 /extend-source:132 /warn:unused /traceback /check:all /I"%I_MPI_ROOT%\include\mpi" /I"%I_MPI_ROOT%\include" /wrap-margin- /F1000000000 prec.obj cons.obj type.obj mesh.obj func.obj pois.obj turb.obj irad.obj radi.obj scrc.obj smvv.obj mpip.obj isob.obj devc.obj cut_face.obj part.obj vege.obj ctrl.obj ieva.obj evac.obj evac_plus.obj hvac.obj mass.obj wall.obj fire.obj velo.obj pres.obj init.obj dump.obj read.obj divg.obj main.obj "%I_MPI_ROOT%\lib\impi.lib" "%MKLLIBDIR%\mkl_intel_lp64.lib" "%MKLLIBDIR%\mkl_core.lib" "%MKLLIBDIR%\mkl_sequential.lib"
if exist fds5_impi_intel_win_db.exe (
    echo.
    echo === DEBUG LINK SUCCESSFUL ===
    echo Binary: %CD%\fds5_impi_intel_win_db.exe
) else (
    echo.
    echo *** ERROR: Debug Link failed ***
)
echo === LINK EXIT CODE: %ERRORLEVEL% ===
