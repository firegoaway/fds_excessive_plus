!> \brief Cut-Face Module for FDS5 (Variant C+ Phase 2)
!!
!! Computes exact cut-face geometry for OBST boundaries that do not
!! align with Cartesian cell faces. Uses the original (unsnapped) OBST
!! coordinates to determine precise area fractions and centroids.
!!
!! This replaces the simple rectangle-intersection ALPHA_CF_W computation
!! with exact geometry for CUT_FACE OBSTs, improving accuracy of:
!! - NO_FLUX blending (velo.f90)
!! - Pressure gradient at wall faces
!! - Divergence in partial-volume cells

MODULE CUT_FACE_MODULE

USE PRECISION_PARAMETERS
USE MESH_POINTERS

IMPLICIT NONE

PRIVATE
PUBLIC :: CUT_FACE_DATA, COMPUTE_CUT_FACE, &
          POLYGON_AREA_2D, POLYGON_CENTROID_2D

INTEGER, PARAMETER :: MAX_CF_VERTS = 24

!> Cut-face data for a single wall cell
TYPE CUT_FACE_DATA
   INTEGER :: NVERT                    !< Number of vertices (max 24)
   REAL(EB) :: XYZVERT(3,MAX_CF_VERTS) !< Vertex coordinates
   REAL(EB) :: AREA                    !< Cut-face area
   REAL(EB) :: AREA_FRAC               !< Area fraction = AREA / cell_face_area
   REAL(EB) :: XYZCEN(3)              !< Centroid of cut-face
   REAL(EB) :: NORMAL(3)              !< Outward normal direction
   REAL(EB) :: VOLUME_FRAC            !< Volume fraction of gas in adjacent cell
END TYPE CUT_FACE_DATA

CONTAINS

!> Compute cut-face data for an OBST at a specific cell face
!!
!! \param OB_XB    Original (unsnapped) OBST coordinates: XB(1:6)
!! \param OB_CF    Whether OBST has CUT_FACE = .TRUE.
!! \param M        Mesh data (pointer)
!! \param I,J,K    Cell indices
!! \param IOR      Face normal direction (+-1, +-2, +-3)
!! \param CF       Output cut-face data
SUBROUTINE COMPUTE_CUT_FACE(OB_XB, OB_CF, I, J, K, IOR, CF)

REAL(EB), INTENT(IN) :: OB_XB(6)
LOGICAL, INTENT(IN) :: OB_CF
INTEGER, INTENT(IN) :: I, J, K, IOR
TYPE(CUT_FACE_DATA), INTENT(OUT) :: CF

! Local variables
REAL(EB) :: FACE_LO(3), FACE_HI(3)    ! Cell face bounding box
REAL(EB) :: OB_LO(3), OB_HI(3)        ! OBST bounding box
REAL(EB) :: INT_LO(3), INT_HI(3)      ! Intersection bounding box
REAL(EB) :: FACE_AREA, INT_AREA
REAL(EB) :: CELL_DX, CELL_DY, CELL_DZ
INTEGER  :: NVERT
REAL(EB) :: VERTS(3,8)

! FDS6: клампинг индексов по фактическим границам массивов координат меша.
! Фантомные wall-cell грани на стыке мешей могут приходить с I/J/K за
! пределами текущего указательного набора (X/Y/Z указатели переключаются
! в SEARCH_OTHER_MESHES) — без клампинга это выход за границы массива.
INTEGER :: IC,JC,KC, IM,JM,KM, ID,JD,KD
IC = MIN(MAX(I  ,LBOUND(X,1)),UBOUND(X,1))
IM = MIN(MAX(I-1,LBOUND(X,1)),UBOUND(X,1))
JC = MIN(MAX(J  ,LBOUND(Y,1)),UBOUND(Y,1))
JM = MIN(MAX(J-1,LBOUND(Y,1)),UBOUND(Y,1))
KC = MIN(MAX(K  ,LBOUND(Z,1)),UBOUND(Z,1))
KM = MIN(MAX(K-1,LBOUND(Z,1)),UBOUND(Z,1))
ID = MIN(MAX(I  ,LBOUND(DX,1)),UBOUND(DX,1))
JD = MIN(MAX(J  ,LBOUND(DY,1)),UBOUND(DY,1))
KD = MIN(MAX(K  ,LBOUND(DZ,1)),UBOUND(DZ,1))

! Initialize defaults
CF%NVERT    = 0
CF%AREA     = 0._EB
CF%AREA_FRAC= 1._EB
CF%XYZCEN   = 0._EB
CF%NORMAL   = 0._EB
CF%VOLUME_FRAC = 1._EB

! Determine cell face bounding box based on IOR
SELECT CASE(ABS(IOR))
CASE(1)  ! X-face: transverse in Y, Z
   FACE_LO(1) = X(IC)
   FACE_HI(1) = X(IC)
   FACE_LO(2) = Y(JM)
   FACE_HI(2) = Y(JC)
   FACE_LO(3) = Z(KM)
   FACE_HI(3) = Z(KC)
   FACE_AREA = DY(JD) * DZ(KD)
   CELL_DX = DX(ID)
   CELL_DY = DY(JD)
   CELL_DZ = DZ(KD)
   CF%NORMAL(1) = REAL(SIGN(1,IOR),EB)
CASE(2)  ! Y-face: transverse in X, Z
   FACE_LO(1) = X(IM)
   FACE_HI(1) = X(IC)
   FACE_LO(2) = Y(JC)
   FACE_HI(2) = Y(JC)
   FACE_LO(3) = Z(KM)
   FACE_HI(3) = Z(KC)
   FACE_AREA = DX(ID) * DZ(KD)
   CELL_DX = DX(ID)
   CELL_DY = DY(JD)
   CELL_DZ = DZ(KD)
   CF%NORMAL(2) = REAL(SIGN(1,IOR),EB)
CASE(3)  ! Z-face: transverse in X, Y
   FACE_LO(1) = X(IM)
   FACE_HI(1) = X(IC)
   FACE_LO(2) = Y(JM)
   FACE_HI(2) = Y(JC)
   FACE_LO(3) = Z(KC)
   FACE_HI(3) = Z(KC)
   FACE_AREA = DX(ID) * DY(JD)
   CELL_DX = DX(ID)
   CELL_DY = DY(JD)
   CELL_DZ = DZ(KD)
   CF%NORMAL(3) = REAL(SIGN(1,IOR),EB)
CASE DEFAULT
   RETURN
END SELECT

! OBST bounding box (use exact coordinates)
OB_LO(1) = OB_XB(1)
OB_HI(1) = OB_XB(2)
OB_LO(2) = OB_XB(3)
OB_HI(2) = OB_XB(4)
OB_LO(3) = OB_XB(5)
OB_HI(3) = OB_XB(6)

! Compute intersection of cell face and OBST (axis-aligned rectangles)
INT_LO(1) = MAX(FACE_LO(1), OB_LO(1))
INT_HI(1) = MIN(FACE_HI(1), OB_HI(1))
INT_LO(2) = MAX(FACE_LO(2), OB_LO(2))
INT_HI(2) = MIN(FACE_HI(2), OB_HI(2))
INT_LO(3) = MAX(FACE_LO(3), OB_LO(3))
INT_HI(3) = MIN(FACE_HI(3), OB_HI(3))

! Check if intersection is valid
SELECT CASE(ABS(IOR))
CASE(1)
   IF (INT_HI(2) <= INT_LO(2) .OR. INT_HI(3) <= INT_LO(3)) RETURN
   INT_AREA = (INT_HI(2)-INT_LO(2)) * (INT_HI(3)-INT_LO(3))
CASE(2)
   IF (INT_HI(1) <= INT_LO(1) .OR. INT_HI(3) <= INT_LO(3)) RETURN
   INT_AREA = (INT_HI(1)-INT_LO(1)) * (INT_HI(3)-INT_LO(3))
CASE(3)
   IF (INT_HI(1) <= INT_LO(1) .OR. INT_HI(2) <= INT_LO(2)) RETURN
   INT_AREA = (INT_HI(1)-INT_LO(1)) * (INT_HI(2)-INT_LO(2))
END SELECT

! Store cut-face properties
CF%AREA = INT_AREA
IF (FACE_AREA > 0._EB) CF%AREA_FRAC = INT_AREA / FACE_AREA

! Compute centroid of rectangular intersection
SELECT CASE(ABS(IOR))
CASE(1)
   CF%XYZCEN(1) = FACE_LO(1)
   CF%XYZCEN(2) = 0.5_EB * (INT_LO(2) + INT_HI(2))
   CF%XYZCEN(3) = 0.5_EB * (INT_LO(3) + INT_HI(3))
CASE(2)
   CF%XYZCEN(1) = 0.5_EB * (INT_LO(1) + INT_HI(1))
   CF%XYZCEN(2) = FACE_LO(2)
   CF%XYZCEN(3) = 0.5_EB * (INT_LO(3) + INT_HI(3))
CASE(3)
   CF%XYZCEN(1) = 0.5_EB * (INT_LO(1) + INT_HI(1))
   CF%XYZCEN(2) = 0.5_EB * (INT_LO(2) + INT_HI(2))
   CF%XYZCEN(3) = FACE_LO(3)
END SELECT

! Build vertices of intersection rectangle (for visualization/debugging)
NVERT = 4
SELECT CASE(ABS(IOR))
CASE(1)
   VERTS(1,1) = FACE_LO(1); VERTS(2,1) = INT_LO(2); VERTS(3,1) = INT_LO(3)
   VERTS(1,2) = FACE_LO(1); VERTS(2,2) = INT_HI(2); VERTS(3,2) = INT_LO(3)
   VERTS(1,3) = FACE_LO(1); VERTS(2,3) = INT_HI(2); VERTS(3,3) = INT_HI(3)
   VERTS(1,4) = FACE_LO(1); VERTS(2,4) = INT_LO(2); VERTS(3,4) = INT_HI(3)
CASE(2)
   VERTS(1,1) = INT_LO(1); VERTS(2,1) = FACE_LO(2); VERTS(3,1) = INT_LO(3)
   VERTS(1,2) = INT_HI(1); VERTS(2,2) = FACE_LO(2); VERTS(3,2) = INT_LO(3)
   VERTS(1,3) = INT_HI(1); VERTS(2,3) = FACE_LO(2); VERTS(3,3) = INT_HI(3)
   VERTS(1,4) = INT_LO(1); VERTS(2,4) = FACE_LO(2); VERTS(3,4) = INT_HI(3)
CASE(3)
   VERTS(1,1) = INT_LO(1); VERTS(2,1) = INT_LO(2); VERTS(3,1) = FACE_LO(3)
   VERTS(1,2) = INT_HI(1); VERTS(2,2) = INT_LO(2); VERTS(3,2) = FACE_LO(3)
   VERTS(1,3) = INT_HI(1); VERTS(2,3) = INT_HI(2); VERTS(3,3) = FACE_LO(3)
   VERTS(1,4) = INT_LO(1); VERTS(2,4) = INT_HI(2); VERTS(3,4) = FACE_LO(3)
END SELECT
CF%NVERT = NVERT
CF%XYZVERT(:,1:NVERT) = VERTS(:,1:NVERT)

! Compute volume fraction: fraction of cell volume that is gas (not OBST)
CALL COMPUTE_VOLUME_FRACTION(OB_XB, I, J, K, IOR, CELL_DX, CELL_DY, CELL_DZ, CF%VOLUME_FRAC)

END SUBROUTINE COMPUTE_CUT_FACE

!> Compute volume fraction (gas volume / cell volume) for a cell adjacent to OBST
SUBROUTINE COMPUTE_VOLUME_FRACTION(OB_XB, I, J, K, IOR, CDX, CDY, CDZ, VF)

REAL(EB), INTENT(IN) :: OB_XB(6)
INTEGER, INTENT(IN) :: I, J, K, IOR
REAL(EB), INTENT(IN) :: CDX, CDY, CDZ
REAL(EB), INTENT(OUT) :: VF

REAL(EB) :: CELL_LO(3), CELL_HI(3)
REAL(EB) :: OB_LO(3), OB_HI(3)
REAL(EB) :: INT_LO(3), INT_HI(3)
REAL(EB) :: CELL_VOL, OBST_VOL
INTEGER :: IC,JC,KC, IM,JM,KM

! FDS6: клампинг индексов (см. комментарий в COMPUTE_CUT_FACE)
IC = MIN(MAX(I  ,LBOUND(X,1)),UBOUND(X,1))
IM = MIN(MAX(I-1,LBOUND(X,1)),UBOUND(X,1))
JC = MIN(MAX(J  ,LBOUND(Y,1)),UBOUND(Y,1))
JM = MIN(MAX(J-1,LBOUND(Y,1)),UBOUND(Y,1))
KC = MIN(MAX(K  ,LBOUND(Z,1)),UBOUND(Z,1))
KM = MIN(MAX(K-1,LBOUND(Z,1)),UBOUND(Z,1))

! Cell bounding box
CELL_LO(1) = X(IM)
CELL_HI(1) = X(IC)
CELL_LO(2) = Y(JM)
CELL_HI(2) = Y(JC)
CELL_LO(3) = Z(KM)
CELL_HI(3) = Z(KC)

! OBST bounding box
OB_LO(1) = OB_XB(1)
OB_HI(1) = OB_XB(2)
OB_LO(2) = OB_XB(3)
OB_HI(2) = OB_XB(4)
OB_LO(3) = OB_XB(5)
OB_HI(3) = OB_XB(6)

! Cell volume
CELL_VOL = CDX * CDY * CDZ
IF (CELL_VOL <= 0._EB) THEN
   VF = 1._EB
   RETURN
ENDIF

! Intersection of cell and OBST
INT_LO(1) = MAX(CELL_LO(1), OB_LO(1))
INT_HI(1) = MIN(CELL_HI(1), OB_HI(1))
INT_LO(2) = MAX(CELL_LO(2), OB_LO(2))
INT_HI(2) = MIN(CELL_HI(2), OB_HI(2))
INT_LO(3) = MAX(CELL_LO(3), OB_LO(3))
INT_HI(3) = MIN(CELL_HI(3), OB_HI(3))

! OBST volume inside cell
IF (INT_HI(1) > INT_LO(1) .AND. INT_HI(2) > INT_LO(2) .AND. INT_HI(3) > INT_LO(3)) THEN
   OBST_VOL = (INT_HI(1)-INT_LO(1)) * (INT_HI(2)-INT_LO(2)) * (INT_HI(3)-INT_LO(3))
ELSE
   OBST_VOL = 0._EB
ENDIF

VF = MAX(0.01_EB, 1._EB - OBST_VOL / CELL_VOL)

END SUBROUTINE COMPUTE_VOLUME_FRACTION

!> Compute area of a 2D polygon (shoelace formula)
!! Vertices are given in the plane perpendicular to the specified axis.
!! \param VERTS  Vertex coordinates (3, NVERT)
!! \param NVERT  Number of vertices
!! \param IAXIS  Axis perpendicular to the polygon plane (1=X, 2=Y, 3=Z)
REAL(EB) FUNCTION POLYGON_AREA_2D(VERTS, NVERT, IAXIS)

REAL(EB), INTENT(IN) :: VERTS(:,:)
INTEGER, INTENT(IN) :: NVERT, IAXIS

REAL(EB) :: AREA_SUM
INTEGER :: I, IP, J1, J2

AREA_SUM = 0._EB

! Select the two in-plane axes
SELECT CASE(IAXIS)
CASE(1)
   J1 = 2; J2 = 3  ! YZ plane
CASE(2)
   J1 = 1; J2 = 3  ! XZ plane
CASE(3)
   J1 = 1; J2 = 2  ! XY plane
CASE DEFAULT
   POLYGON_AREA_2D = 0._EB
   RETURN
END SELECT

! Shoelace formula
DO I = 1, NVERT
   IP = MOD(I, NVERT) + 1
   AREA_SUM = AREA_SUM + VERTS(J1,I)*VERTS(J2,IP) - VERTS(J1,IP)*VERTS(J2,I)
ENDDO

POLYGON_AREA_2D = ABS(AREA_SUM) * 0.5_EB

END FUNCTION POLYGON_AREA_2D

!> Compute centroid of a 2D polygon
!! \param VERTS  Vertex coordinates (3, NVERT)
!! \param NVERT  Number of vertices
!! \param IAXIS  Axis perpendicular to the polygon plane
!! \param CEN    Output centroid (3 components)
SUBROUTINE POLYGON_CENTROID_2D(VERTS, NVERT, IAXIS, CEN)

REAL(EB), INTENT(IN) :: VERTS(:,:)
INTEGER, INTENT(IN) :: NVERT, IAXIS
REAL(EB), INTENT(OUT) :: CEN(3)

REAL(EB) :: AREA_SUM, CX, CY
INTEGER :: I, IP, J1, J2, JN
REAL(EB) :: CROSS

CEN = 0._EB
CX = 0._EB
CY = 0._EB
AREA_SUM = 0._EB

SELECT CASE(IAXIS)
CASE(1)
   J1 = 2; J2 = 3; JN = 1
CASE(2)
   J1 = 1; J2 = 3; JN = 2
CASE(3)
   J1 = 1; J2 = 2; JN = 3
CASE DEFAULT
   RETURN
END SELECT

DO I = 1, NVERT
   IP = MOD(I, NVERT) + 1
   CROSS = VERTS(J1,I)*VERTS(J2,IP) - VERTS(J1,IP)*VERTS(J2,I)
   AREA_SUM = AREA_SUM + CROSS
   CX = CX + (VERTS(J1,I) + VERTS(J1,IP)) * CROSS
   CY = CY + (VERTS(J2,I) + VERTS(J2,IP)) * CROSS
ENDDO

IF (ABS(AREA_SUM) < 1.E-12_EB) THEN
   CEN(JN) = VERTS(JN,1)
   RETURN
ENDIF

AREA_SUM = AREA_SUM * 0.5_EB
CEN(J1) = CX / (6._EB * AREA_SUM)
CEN(J2) = CY / (6._EB * AREA_SUM)
CEN(JN) = VERTS(JN,1)

END SUBROUTINE POLYGON_CENTROID_2D

END MODULE CUT_FACE_MODULE
