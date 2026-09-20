module isodefs
implicit none

CHARACTER(255), PARAMETER :: smvvid='$Id$'
CHARACTER(255), PARAMETER :: smvvrev='$Revision$'
CHARACTER(255), PARAMETER :: smvvdate='$Date$'

interface

subroutine isoheader(isofile,isolonglabel,isoshortlabel,isounits,levels,nlevels,error)

!DEC$ ATTRIBUTES C :: ISOHEADER
!DEC$ ATTRIBUTES REFERENCE :: ISOFILE,ISOLONGLABEL,ISOSHORTLABEL
!DEC$ ATTRIBUTES REFERENCE :: ISOUNITS,LEVELS,NLEVELS,ERROR

character(len=*) :: isofile
character(len=30), intent(in) :: isolonglabel, isoshortlabel, isounits
integer, intent(in) :: nlevels
integer, intent(out) :: error
real, dimension(nlevels), intent(in) :: levels

end subroutine isoheader

subroutine tisoheader(isofile,isolonglabel,isoshortlabel,isounits,levels,nlevels,error)

!DEC$ ATTRIBUTES C :: TISOHEADER
!DEC$ ATTRIBUTES REFERENCE :: ISOFILE,ISOLONGLABEL,ISOSHORTLABEL
!DEC$ ATTRIBUTES REFERENCE :: ISOUNITS,LEVELS,NLEVELS,ERROR

character(len=*) :: isofile
character(len=30), intent(in) :: isolonglabel, isoshortlabel, isounits
integer, intent(in) :: nlevels
integer, intent(out) :: error
real, dimension(nlevels), intent(in) :: levels

end subroutine tisoheader

subroutine iso2file(isofile,t,data,iblank,level,nlevels, xplt, nx, yplt, ny, zplt, nz, isooffset, reduce_triangles, error)

!DEC$ ATTRIBUTES C :: ISO2FILE
!DEC$ ATTRIBUTES REFERENCE :: ISOFILE, T, DATA, IBLANK, LEVEL
!DEC$ ATTRIBUTES REFERENCE :: NLEVELS, XPLT, NX, YPLT, NY, ZPLT, NZ 
!DEC$ ATTRIBUTES REFERENCE :: ISOOFFSET, REDUCE_TRIANGLES, ERROR

character(len=*), intent(in) :: isofile
integer, intent(in) :: nlevels, isooffset, nx, ny, nz
integer, intent(out) :: error
real, intent(in) :: t
real, dimension(nlevels), intent(in)  :: level
real, intent(in), dimension(nx*ny*nz) :: data
integer, intent(in), dimension(nx*ny*nz) :: iblank
real, intent(in), dimension(nx) :: xplt
real, intent(in), dimension(ny) :: yplt
real, intent(in), dimension(nz) :: zplt
integer, intent(in) :: reduce_triangles

end subroutine iso2file

subroutine isot2file(isofile,t,data,data2flag,data2, iblank,level,nlevels, &
                      xplt, nx, yplt, ny, zplt, nz, isooffset, reduce_triangles, error)

!DEC$ ATTRIBUTES C :: ISOT2FILE
!DEC$ ATTRIBUTES REFERENCE :: ISOFILE, T, DATA, DATA2FLAG, DATA2
!DEC$ ATTRIBUTES REFERENCE :: IBLANK, LEVEL, NLEVELS, XPLT, NX
!DEC$ ATTRIBUTES REFERENCE :: YPLT, NY, ZPLT, NZ, ISOOFFSET
!DEC$ ATTRIBUTES REFERENCE :: REDUCE_TRIANGLES, ERROR

character(len=*), intent(in) :: isofile
integer, intent(in) :: nlevels, isooffset, nx, ny, nz, data2flag
integer, intent(out) :: error
real, intent(in) :: t
real, dimension(nlevels), intent(in)  :: level
real, intent(in), dimension(nx*ny*nz) :: data, data2
integer, intent(in), dimension(nx*ny*nz) :: iblank
real, intent(in), dimension(nx) :: xplt
real, intent(in), dimension(ny) :: yplt
real, intent(in), dimension(nz) :: zplt
integer, intent(in) :: reduce_triangles

end subroutine isot2file

end interface

CONTAINS

SUBROUTINE GET_REV_smvv(MODULE_REV,MODULE_DATE)
INTEGER,INTENT(INOUT) :: MODULE_REV
CHARACTER(255),INTENT(INOUT) :: MODULE_DATE

WRITE(MODULE_DATE,'(A)') smvvrev(INDEX(smvvrev,':')+1:LEN_TRIM(smvvrev)-2)
READ (MODULE_DATE,'(I5)') MODULE_REV
WRITE(MODULE_DATE,'(A)') smvvdate

END SUBROUTINE GET_REV_smvv

end module isodefs

module compressdefs
implicit none

interface

subroutine smoke3dheader(file,is1,is2,js1,js2,ks1,ks2)

!DEC$ ATTRIBUTES C :: SMOKE3DHEADER
!DEC$ ATTRIBUTES REFERENCE :: FILE,IS1,IS2,JS1,JS2,KS1,KS2
character(len=*), intent(in) :: file
integer, intent(in) ::is1,is2,js1,js2,ks1,ks2

end subroutine smoke3dheader

subroutine smoke3dtofile(file,time,dx,extcoef,type,xyz,nx,ny,nz,HRRPUV_MAX_SMV)

!DEC$ ATTRIBUTES C :: SMOKE3DTOFILE
!DEC$ ATTRIBUTES REFERENCE :: FILE,TIME,DX,EXTCOEF,TYPE,XYZ,NX,NY,NZ,HRRPUV_MAX_SMV
character(len=*), intent(in) :: file
real, intent(in) :: time, dx, extcoef
integer, intent(in) :: nx,ny,nz
real, intent(in), dimension(nx*ny*nz) :: xyz
integer, intent(in) :: type
real, intent(in) :: HRRPUV_MAX_SMV
end subroutine smoke3dtofile

end interface
end module compressdefs

module smoke3d_defs
USE PRECISION_PARAMETERS
USE GLOBAL_CONSTANTS, ONLY: TEMP_MIN_SMV,TEMP_MAX_SMV
IMPLICIT NONE

CONTAINS

!> \brief Run-length encoding compression for SMOKE3D data (ported from FDS6 smvv.f90)

SUBROUTINE RLE_COMPRESS(BUFFER_IN,NCHARS_IN,BUFFER_OUT,NCHARS_OUT)

INTEGER, INTENT(IN) :: NCHARS_IN
CHARACTER(LEN=1), INTENT(IN), DIMENSION(NCHARS_IN) :: BUFFER_IN
CHARACTER(LEN=1), DIMENSION(:) :: BUFFER_OUT
INTEGER, INTENT(OUT) :: NCHARS_OUT

CHARACTER(LEN=1) :: MARK=CHAR(255),THISCHAR,LASTCHAR
INTEGER :: N,N2,NREPEATS

NREPEATS=1
LASTCHAR=MARK
N2=1
DO N=1,NCHARS_IN
   THISCHAR=BUFFER_IN(N)
   IF (THISCHAR == LASTCHAR) THEN
      NREPEATS=NREPEATS+1
   ELSE
      NREPEATS=1
   ENDIF
   IF (NREPEATS >=1 .AND. NREPEATS <= 3) THEN
      BUFFER_OUT(N2)=THISCHAR
      LASTCHAR=THISCHAR
   ELSE
      IF (NREPEATS == 4) THEN
         N2=N2-3
         BUFFER_OUT(N2)=MARK
         BUFFER_OUT(N2+1)=THISCHAR
         N2=N2+2
      ELSE
         N2=N2-1
      ENDIF
      BUFFER_OUT(N2)=CHAR(NREPEATS)
      IF (NREPEATS == 254) THEN
         NREPEATS=1
         LASTCHAR=MARK
      ENDIF
   ENDIF
   N2=N2+1
END DO
NCHARS_OUT=N2-1

END SUBROUTINE RLE_COMPRESS


!> \brief Write SMOKE3D compressed data using Fortran UNFORMATTED I/O
!>
!> Replaces the C-based SMOKE3DTOFILE (isob.c) to produce binary files
!> compatible with SmokeView 6.10.1 which expects Fortran UNFORMATTED records.
!>
!> \param FILENAME  Base .s3d filename (without .sz/.s3dd suffix)
!> \param STIME     Current simulation time (s)
!> \param DXX       Cell size (m)
!> \param MASS_EXT_COEF  Mass extinction coefficient (m2/kg)
!> \param DATAFLAG  1=SOOT (GAS), 2=FIRE, 3=WATER
!> \param VALS      Packed 1D array of cell-centered values
!> \param NX,NY,NZ  Mesh dimensions
!> \param HRRPUV_MAX_SMV  Maximum HRRPUV for FIRE color scaling

SUBROUTINE SMOKE3D_TO_FILE(FILENAME,STIME,DXX,MASS_EXT_COEF,DATAFLAG,VALS,NX,NY,NZ,HRRPUV_MAX_SMV,LU_BIN,LU_SZ,LU_DENS)

CHARACTER(LEN=*), INTENT(IN) :: FILENAME
REAL(FB), INTENT(IN) :: STIME,DXX,MASS_EXT_COEF
INTEGER, INTENT(IN) :: DATAFLAG,NX,NY,NZ,LU_BIN,LU_SZ,LU_DENS
REAL(FB), INTENT(IN), DIMENSION(NX*NY*NZ) :: VALS
REAL(FB), INTENT(IN) :: HRRPUV_MAX_SMV

CHARACTER(LEN=1), DIMENSION(:), ALLOCATABLE :: BUFFER_IN,BUFFER_OUT
CHARACTER(LEN=1), DIMENSION(:), ALLOCATABLE :: BUFFER_DENS_IN,BUFFER_DENS_OUT
REAL(FB) :: FACTOR,VAL_FDS,VAL_SMV,MAX_VAL,MAX_DENS_VAL
INTEGER :: I,NVALS,NCHARS_IN,NCHARS_OUT,NCHARS_DENS_OUT
CHARACTER(LEN=255) :: FN_SZ,FN_DENS
LOGICAL :: IS_GAS

NVALS = NX*NY*NZ
IF (NVALS < 1) RETURN
NCHARS_IN = NVALS

ALLOCATE(BUFFER_IN(NVALS))
ALLOCATE(BUFFER_OUT(NVALS))

IS_GAS = (DATAFLAG == 1)
MAX_VAL = 0.0_FB

IF (DATAFLAG == 1) THEN
   ! SOOT (GAS) type: Beer-Lambert absorption
   FACTOR = -REAL(MASS_EXT_COEF,FB) * DXX
   MAX_DENS_VAL = 0.0_FB
   DO I=1,NVALS
      VAL_FDS = MAX(0.0_FB, VALS(I))
      VAL_SMV = 254.0_FB * (1.0_FB - EXP(FACTOR * VAL_FDS))
      BUFFER_IN(I) = CHAR(NINT(VAL_SMV))
      MAX_VAL = MAX(VAL_SMV, MAX_VAL)
      MAX_DENS_VAL = MAX(VALS(I), MAX_DENS_VAL)
   ENDDO
   ! Prepare density buffer for .s3dd file
   ALLOCATE(BUFFER_DENS_IN(NVALS))
   ALLOCATE(BUFFER_DENS_OUT(NVALS))
   IF (MAX_DENS_VAL <= 0.0_FB) THEN
      DO I=1,NVALS
         BUFFER_DENS_IN(I) = CHAR(0)
      ENDDO
   ELSE
      DO I=1,NVALS
         VAL_FDS = 254.0_FB * (VALS(I) / MAX_DENS_VAL)
         VAL_FDS = MAX(0.0_FB, VAL_FDS)
         VAL_FDS = MIN(254.0_FB, VAL_FDS)
         BUFFER_DENS_IN(I) = CHAR(NINT(VAL_FDS))
      ENDDO
   ENDIF

ELSEIF (DATAFLAG == 2) THEN
   ! FIRE type: normalize by HRRPUV_MAX_SMV
   DO I=1,NVALS
      VAL_FDS = MIN(HRRPUV_MAX_SMV, MAX(0.0_FB, VALS(I)))
      VAL_SMV = 254.0_FB * (VAL_FDS / HRRPUV_MAX_SMV)
      BUFFER_IN(I) = CHAR(INT(VAL_SMV))
      MAX_VAL = MAX(VAL_FDS, MAX_VAL)
   ENDDO

ELSEIF (DATAFLAG == 3) THEN
   ! WATER type: droplet density
   FACTOR = 1.0_FB / (0.1_FB * 0.5_FB * DXX)
   DO I=1,NVALS
      VAL_FDS = VALS(I) - 0.003_FB
      IF (VAL_FDS < 0.0_FB) VAL_FDS = 0.0_FB
      BUFFER_IN(I) = CHAR(NINT(254.0_FB * (1.0_FB - 0.5_FB**(VAL_FDS*FACTOR))))
   ENDDO

ELSEIF (DATAFLAG == 4) THEN
   ! TEMPERATURE type: EFFECTIVE FLAME TEMPERATURE
   DO I=1,NVALS
      VAL_FDS = MIN(TEMP_MAX_SMV, MAX(TEMP_MIN_SMV, VALS(I)))
      VAL_SMV = 254.0_FB * ((VAL_FDS - TEMP_MIN_SMV) / (TEMP_MAX_SMV - TEMP_MIN_SMV))
      BUFFER_IN(I) = CHAR(INT(VAL_SMV))
      MAX_VAL = MAX(VAL_FDS, MAX_VAL)
   ENDDO

ELSE
   DEALLOCATE(BUFFER_IN)
   DEALLOCATE(BUFFER_OUT)
   RETURN
ENDIF

! RLE compress
CALL RLE_COMPRESS(BUFFER_IN,NCHARS_IN,BUFFER_OUT,NCHARS_OUT)

! Derive auxiliary filenames
FN_SZ = TRIM(FILENAME) // '.sz'
FN_DENS = TRIM(FILENAME) // 'd'  ! .s3d -> .s3dd

! ---- Write .s3d binary (Fortran UNFORMATTED) ----
OPEN(UNIT=LU_BIN, FILE=FILENAME, FORM='UNFORMATTED', STATUS='OLD', POSITION='APPEND')
WRITE(LU_BIN) STIME
WRITE(LU_BIN) NCHARS_IN, NCHARS_OUT
WRITE(LU_BIN) (BUFFER_OUT(I), I=1,NCHARS_OUT)
CLOSE(LU_BIN)

! ---- Write .sz text file ----
IF (IS_GAS) THEN
   ! GAS type: RLE compress density, write extended .sz line
   CALL RLE_COMPRESS(BUFFER_DENS_IN,NCHARS_IN,BUFFER_DENS_OUT,NCHARS_DENS_OUT)
   OPEN(UNIT=LU_SZ, FILE=FN_SZ, FORM='FORMATTED', STATUS='OLD', POSITION='APPEND')
   WRITE(LU_SZ,*) STIME, NCHARS_IN, NCHARS_OUT, MAX_VAL, NCHARS_DENS_OUT, MAX_DENS_VAL
   CLOSE(LU_SZ)
   ! Write .s3dd density binary
   OPEN(UNIT=LU_DENS, FILE=FN_DENS, FORM='UNFORMATTED', STATUS='OLD', POSITION='APPEND')
   WRITE(LU_DENS) STIME
   WRITE(LU_DENS) NCHARS_IN, NCHARS_DENS_OUT
   WRITE(LU_DENS) (BUFFER_DENS_OUT(I), I=1,NCHARS_DENS_OUT)
   CLOSE(LU_DENS)
   DEALLOCATE(BUFFER_DENS_IN)
   DEALLOCATE(BUFFER_DENS_OUT)
ELSE
   ! Non-GAS type: standard .sz line
   OPEN(UNIT=LU_SZ, FILE=FN_SZ, FORM='FORMATTED', STATUS='OLD', POSITION='APPEND')
   WRITE(LU_SZ,*) STIME, NCHARS_IN, NCHARS_OUT, MAX_VAL
   CLOSE(LU_SZ)
ENDIF

DEALLOCATE(BUFFER_IN)
DEALLOCATE(BUFFER_OUT)

END SUBROUTINE SMOKE3D_TO_FILE

end module smoke3d_defs
