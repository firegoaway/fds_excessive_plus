! R29b EVAC+: индивидуально-поточная модель эвакуации по Методике МЧС 1140
! (приложения N 2, 6, 7, 8). Проектная документация: fds5/evac+_docs/.
! Движок не зависит от FDS+Evac (evac.f90 не модифицируется); используются
! только обвязка EVA5 (dump.f90) и часы T_EVAC (main.f90, EVAC_MAIN_LOOP).
! Namelist-группы: &EVPL (управление), &EVHR (источники), &EVSG (участки),
! &EVEX (выходы), &EVCP (параметры классов). Включение: &MISC EVAC_PLUS=T.
! Этап 2 (R29b): блуждающий шаг с дисперсией скоростей (13); локальная
! плотность (8) для неоднородных потоков; слияния с долями участия П8.8;
! очереди и скопления с q_Dmax; диагностика t_з (15), t_ск (16); контроль
! кумулятивной ошибки плотностей (разд. 9 мат. модели).
! Этап 3 (R29c): расчётный протокол движка (&EVPL PROTOCOL_DT ->
! CHID_evac_prot.csv: фактические подстановки N, D, V, q, ширина по участкам
! и границам на контрольные моменты времени — исходные данные модуля вывода
! аналитических решений); балансные счётчики входа/выхода по участкам;
! итоговая статистика по классам мобильности.
! R29d: режим разуплотнения (П6, абзац после П6.13): агенты, покидающие
! скопление или очередь, движутся с ограничением V <= V_qmax = a*V0 (вид пути
! текущего участка); ограничение снимается при локальной плотности ниже D0 и
! расстоянии до ближайшего впереди идущего более RELAX_LEN (&EVPL, м).
! R30: этап 4 — OpenMP по агентам в фазе скоростей/перемещений: плотности
! читаются из снимка позиций AG_SOLD на начало шага (синхронное обновление
! состояния, разд. 8 мат. модели); постановки в очереди границ — отдельная
! последовательная фаза в порядке слотов (детерминизм при любом числе
! потоков); блуждающий режим исполняется в один поток (детерминизм
! генератора RANDOM_NUMBER). СКО скоростей свободного движения в
! стохастическом режиме без явного SIGMA_V0 — коэффициент вариации
! Cv = 0.15 (М0) / 0.20 (М1-М4), sigma = Cv*V0 (разд. 10 п. 7 модели),
! с кинематическим усечением +-3 сигма и пределом ходьбы 150 м/мин
! (разд. 10 п. 8). Время шага (SYSTEM_CLOCK) и момент последнего выхода по
! классам — в CHID_evac.out.
! R31: решения 22-25 от 24.09.2026. NLOCAL по умолчанию 8 (решение 22).
! Именованные классы подкатегорий П2.7: M1D (дошкольники), M1C (дети с
! ограниченными возможностями), M1G (глухие/слабослышащие), M1B (беременные),
! M2S (слепые с тростью); пользовательские классы — &EVCP DEFINE=.TRUE.
! (решение 23; имена классов источников разрешаются после чтения &EVCP).
! Ускорение этапа 4 (решение 24): списки движущихся агентов звеньев — CSR,
! упорядочены по положению, поиск NLOCAL ближайших вперёд идущих завершается
! досрочно; счётные фазы параллельны: целочисленные счётчики — атомарные
! (порядок сложения целых не влияет на результат), LK_FA — суммы,
! сгруппированные по классам в фиксированном порядке, суммы фазы 6 —
! фиксированные отрезки слотов (число отрезков не зависит от числа потоков);
! детерминизм при любом числе потоков сохранён.
! R32: этап 6 — пакетный стохастический режим (решение 15, разд. 8 модели,
! приложение N 8 Методики, заключительный абзац): при MODE=WANDER расчётное
! время эвакуации — квантиль P=0.999 (&EVPL PROB) эмпирического распределения
! по серии RUNS независимых прогонов (&EVPL RUNS; по умолчанию 100 в WANDER,
! 1 в FIXED; RUNS>1 при FIXED — ошибка ввода). Оркестрация — серия внутри
! одного процесса: по завершении текущего прогона состояние модуля
! сбрасывается (EVP_RESET_FOR_RUN, без выделений памяти в шаге), зерно
! генератора = SEED+(номер прогона-1), остальные прогоны выполняются внутри
! одного вызова шага по локальным часам от T_BEGIN; побочные выводы по шагам
! (EVA5/CSV/протокол) покрывают прогон 1. Квантиль — k-я порядковая
! статистика, k = ceil(P*N) (стандартная эмпирическая обратная F_N; при
! N=100, P=0.999 — максимум выборки). Вывод: CHID_evac_dist.csv (полная
! выборка + блок Summary) и блок сводки в CHID_evac.out; протокольное число —
! только квантиль, медиана и P=0.95 — справочно. RUNS=1 в WANDER —
! предупреждение (одиночный прогон не является оценкой по П8).
MODULE EVACPLUS
   USE PRECISION_PARAMETERS
   USE GLOBAL_CONSTANTS
   USE MESH_VARIABLES
   USE TYPES
   USE COMP_FUNCTIONS, ONLY: CHECKREAD, SHUTDOWN, GET_FILE_NUMBER, SECOND, CHECK_XB
   USE MEMORY_FUNCTIONS, ONLY: CHKMEMERR
!$ USE OMP_LIB   ! активен только при сборке с OpenMP (этап 4)
   IMPLICIT NONE
   PRIVATE
   PUBLIC :: READ_EVAC_PLUS, EVAC_PLUS_INITIALIZE, EVAC_PLUS_STEP, EVAC_PLUS_DUMP, &
             EVAC_PLUS_INIT_DUMPS, EVAC_PLUS_DUMP_CSV, EVAC_PLUS_FINAL_STATS, GET_REV_EVACP

   INTEGER, PARAMETER :: EVP_NCLASS = 16    ! 1..7 = М0-1..М0-7; 8=М1; 9=М2; 10=М3; 11=М4;
                                            ! 12=M1D; 13=M1C; 14=M1G; 15=M1B; 16=M2S (подкатегории П2.7)
   INTEGER, PARAMETER :: EVP_NCLASS_MAX = 32 ! ёмкость таблицы классов (встроенные + пользовательские)
   INTEGER :: EVP_NCLSUSED = 0              ! число определённых классов (>= EVP_NCLASS после BUILD)
   INTEGER, PARAMETER :: EVP_NKIND = 6      ! 1 горизонт; 2 проём; 3 лестн. вниз; 4 лестн. вверх; 5 пандус вниз; 6 пандус вверх
   INTEGER, PARAMETER :: EVP_KIND_EXIT = 7  ! внутренний тип сегмента-выхода
   INTEGER, PARAMETER :: EVP_MAXPRE = 16    ! максимум предшественников у сегмента
    REAL(EB), PARAMETER :: EVP_PI = 4._EB*ATAN(1._EB)
    REAL(EB), PARAMETER :: EVP_DMAX_M2 = 0.9_EB   ! максимальная плотность потока, м2/м2
    REAL(EB), PARAMETER :: EVP_ROOM_EPS = 0.92_EB ! допуск заполнения звена при входе
    REAL(EB), PARAMETER :: EVP_LINK_LEN = 1.0_EB  ! длина элементарного звена, м
    REAL(EB), PARAMETER :: EVP_WANDER_XI = 0.5_EB ! размах равномерного смещения интервала обновления (разд. 8 модели)
     REAL(EB), PARAMETER :: EVP_EPS_BAL = 0.05_EB  ! порог контроля кумулятивной ошибки плотностей (разд. 9 модели)
     REAL(EB), PARAMETER :: EVP_VMAX = 150.0_EB    ! физический предел скорости ходьбы, м/мин (разд. 10 п. 8 модели)
      INTEGER, PARAMETER :: EVP_NPPS = 30000          ! лимит агентов в кадре EVA5
      INTEGER, PARAMETER :: EVP_NCHUNK = 8            ! фиксированное число отрезков слотов фазы 6 (R31)

   ! Параметры классов мобильности: таблицы П2.6/П2.7 (V0 м/мин; a; D0 чел./м2; f м2/чел.)
   TYPE EVP_CLASS_TYPE
      REAL(EB) :: V0(EVP_NKIND) = 0._EB
      REAL(EB) :: AV(EVP_NKIND) = 0._EB
      REAL(EB) :: D0(EVP_NKIND) = 0._EB
      REAL(EB) :: SIG(EVP_NKIND) = 0._EB
      REAL(EB) :: F = 0._EB
      LOGICAL :: DEF(EVP_NKIND) = .FALSE.
      CHARACTER(10) :: NAME = ''
   END TYPE EVP_CLASS_TYPE
   TYPE(EVP_CLASS_TYPE) :: CLS(EVP_NCLASS_MAX)

   ! Таблица П2.8: предельные интенсивности, м/мин (для проёма q_Dmax зависит от ширины)
   REAL(EB) :: EVP_QMAX(EVP_NKIND) = (/16.5_EB,19.6_EB,16.0_EB,11.0_EB,16.0_EB,11.0_EB/)
   REAL(EB) :: EVP_QDMAXF(EVP_NKIND) = (/13.5_EB,8.5_EB,7.2_EB,9.9_EB,7.2_EB,9.9_EB/)

    ! Состояние модуля
    LOGICAL :: EVP_ON = .FALSE., EVP_READY = .FALSE., EVP_DUMPS_OPEN = .FALSE.
    INTEGER :: EVP_MODE = 0               ! 0 — постоянный шаг; 1 — блуждающий (разд. 8 модели)
   REAL(EB) :: EVP_DT = 0.1_EB
   REAL(EB) :: EVP_SIGMA_V0 = 0._EB
   INTEGER :: EVP_SEED = 42
    INTEGER :: EVP_NLOCAL = 8             ! n ближайших вперёд идущих для локальной плотности (8); 0 — счётная; 8 по умолчанию (решение 22)

    INTEGER :: EVP_NSG = 0, EVP_NSGTOT = 0, EVP_NEX = 0, EVP_NHR = 0, EVP_NLK = 0
    INTEGER :: EVP_NAGMAX = 0, EVP_NAG = 0, EVP_NALIVE = 0, EVP_NEVAC = 0, EVP_ILABEL = 0
    INTEGER :: EVP_FREE = 0
    REAL(EB) :: EVP_TLAST = -1._EB, EVP_TSTEP = 0._EB
    INTEGER :: LU_EVPCSV = 0, LU_EVPOUT = 0

    ! Контроль кумулятивной ошибки плотностей (разд. 9 модели)
     INTEGER :: EVP_NRESYNC = 0, EVP_ECNT = 0
     REAL(EB) :: EVP_ESUM = 0._EB
     REAL(EB) :: EVP_RELAX = 5.0_EB   ! порог разуплотнения: дистанция до впереди идущего, м (R29d)

     ! Расчётный протокол движка (этап 3): интервал, момент, файловый номер
     REAL(EB) :: EVP_PROTDT = 0._EB
     REAL(EB) :: EVP_TPROT = 0._EB
     INTEGER :: LU_EVPROT = 0

      ! Статистика по классам: порождено/эвакуировано/последний выход
       INTEGER :: EVP_CLSGEN(EVP_NCLASS_MAX) = 0
       INTEGER :: EVP_CLSEVAC(EVP_NCLASS_MAX) = 0
       REAL(EB) :: EVP_CLSTLAST(EVP_NCLASS_MAX) = -1._EB

       ! Время работы движка (этап 4): суммарное стеновое время шагов, число шагов
       REAL(EB) :: EVP_TWALL = 0._EB
       INTEGER :: EVP_NSTEPS = 0
       INTEGER(8) :: EVP_CLKRATE = 0
       REAL(EB) :: EVP_TPH(8) = 0._EB   ! время фаз шага (R31): 1 фазы 0-2 счёт/плотн./списки;
                                        ! 2 фаза 3а движение; 3 фаза 3б; 4 фаза 4; 5 фаза 5;
                                        ! 6 фазы 6+7 контроль/протокол; 7-8 резерв

      ! Пакетный стохастический режим (этап 6, решение 15): серия независимых
      ! прогонов внутри одного процесса; зерно прогона r = SEED+(r-1)
       INTEGER, PARAMETER :: EVP_RUNSMAX = 100000     ! предел &EVPL RUNS
       REAL(EB), PARAMETER :: EVP_BATCH_TMAX = 86400._EB ! предохранитель внутреннего прогона, с
       INTEGER :: EVP_RUNS = 1            ! число прогонов пакета (&EVPL RUNS)
       REAL(EB) :: EVP_PROB = 0.999_EB    ! квантиль распределения (&EVPL PROB)
       INTEGER :: EVP_BATCHRUN = 1        ! текущий прогон (1 — визуализируемый)
       INTEGER :: EVP_RUNSDONE = 0        ! число завершённых прогонов
       LOGICAL :: EVP_BATCHDONE = .FALSE., EVP_DISTWRIT = .FALSE.
       LOGICAL :: EVP_OUTRUN = .TRUE.     ! вывод разрешён (только прогон 1)
       LOGICAL :: EVP_PROTFIN = .FALSE.   ! финальный блок протокола записан
       INTEGER :: LU_EVPDIST = 0          ! файловый номер CHID_evac_dist.csv
       REAL(EB), ALLOCATABLE :: EVP_TSAMP(:), EVP_TSORT(:)  ! выборка и сортированная копия
       ! Сводка пакета (для evac.out): квантиль, границы, среднее, СКО, справочные квантили
       REAL(EB) :: EVP_SQ = -1._EB, EVP_SMIN = 0._EB, EVP_SMAX = 0._EB
       REAL(EB) :: EVP_SMEAN = 0._EB, EVP_SSIG = 0._EB, EVP_SMED = -1._EB, EVP_SQ95 = -1._EB
       INTEGER :: EVP_QK = 0

     ! Упорядоченные по положению списки движущихся агентов звеньев (CSR, R31):
     ! AG_ORDER — слоты агентов по звеньям по возрастанию AG_S (при равенстве
     ! AG_S — по убыванию слота); AG_POS — номер агента в списке своего звена;
     ! LK_MOV — число движущихся (состояние 0) на звене; LK_OFF — начало среза
     ! звена; поиск NLOCAL ближайших вперёд идущих завершается досрочно
      INTEGER, ALLOCATABLE :: AG_ORDER(:), AG_POS(:)
      INTEGER, ALLOCATABLE :: LK_MOV(:), LK_OFF(:), LK_CUR(:)
      INTEGER, ALLOCATABLE :: LK_MC(:,:)   ! счётчики (звено, класс) для LK_FA (R31)
      REAL(EB), ALLOCATABLE :: EVP_WSV(:,:), EVP_WSFV(:,:) ! суммы фазы 6 по отрезкам слотов (R31)

    ! Контроль баланса (разд. 9 модели): накопленная площадь проекций на звене,
    ! максимум расхождения плотностей, флаг ресинхронизации скорости
    REAL(EB), ALLOCATABLE :: LK_FA(:)
    REAL(EB), ALLOCATABLE :: LK_DERR(:)
    LOGICAL, ALLOCATABLE :: LK_SYNC(:)
    REAL(EB), ALLOCATABLE :: LK_SV(:), LK_SFV(:)  ! рабочие суммы скоростей фазы 6
    INTEGER, ALLOCATABLE :: EVP_XCNT(:)           ! ушедшие через выходы за шаг, по звеньям (R31)

    ! Сегменты (пользовательские 1..EVP_NSG; выходы EVP_NSG+1..EVP_NSGTOT)
    CHARACTER(60), ALLOCATABLE :: SG_ID(:)
    REAL(EB), ALLOCATABLE :: SG_X1(:), SG_Y1(:), SG_X2(:), SG_Y2(:), SG_Z(:), SG_B(:), SG_LEN(:)
    INTEGER, ALLOCATABLE :: SG_KIND(:), SG_NXT(:,:), SG_LSTART(:), SG_NLINK(:), SG_NPRE(:), SG_PRE(:,:)
    INTEGER, ALLOCATABLE :: SG_NIN(:), SG_NOUT(:), SG_NPLACE(:)  ! баланс по участкам (этап 3)
    INTEGER, ALLOCATABLE :: SG_NEXIT(:)                          ! эвакуировано с участка (этап 3)
    INTEGER, ALLOCATABLE :: SG_NGONE(:)                          ! передано на следующий участок (этап 3)

   ! Текстовые ссылки NEXT до разрешения в индексы
   CHARACTER(60), ALLOCATABLE :: EVP_NEXTTXT(:,:)

    ! Выходы
    CHARACTER(60), ALLOCATABLE :: EX_ID(:)
    INTEGER, ALLOCATABLE :: EX_SG(:), EX_CNT(:)
    REAL(EB), ALLOCATABLE :: EX_TLAST(:)
    LOGICAL, ALLOCATABLE :: EX_IONLY(:)

   ! Элементарные звенья (базовая длина 1 м)
   INTEGER, ALLOCATABLE :: LK_SEG(:), LK_NEXT(:), LK_KIND(:), LK_CNT(:)
   REAL(EB), ALLOCATABLE :: LK_X0(:), LK_Y0(:), LK_UX(:), LK_UY(:), LK_LEN(:), LK_B(:), LK_D(:)
   LOGICAL, ALLOCATABLE :: LK_LAST(:)

    ! Границы (очереди): индекс границы равен индексу сегмента, в который она ведёт
    INTEGER, ALLOCATABLE :: BND_QHEAD(:), BND_QTAIL(:), BND_QLEN(:)
    LOGICAL, ALLOCATABLE :: BND_LEFT(:), BND_CROWD(:)
    REAL(EB), ALLOCATABLE :: BND_RESID(:)   ! переносимый остаток лимита (6), м2
    REAL(EB), ALLOCATABLE :: BND_CR(:,:)    ! кредиты взвешенного round-robin (П8.8)

    ! Диагностика границ: t_з (15), t_ск (16)
    INTEGER, ALLOCATABLE :: BND_NPASS(:), BND_QMAX(:), BND_NEP(:)
    REAL(EB), ALLOCATABLE :: BND_TWSUM(:), BND_TMAXW(:)  ! сумма и максимум ожидания, с
    REAL(EB), ALLOCATABLE :: BND_ACROWD(:)  ! площадь проекций, пропущенная за эпизоды скопления, м2
    REAL(EB), ALLOCATABLE :: BND_TSK_SIM(:) ! суммарное время существования скопления (измеренное), с
    REAL(EB), ALLOCATABLE :: BND_TSK_EST(:) ! суммарная оценка t_ск по формуле (16), с
    REAL(EB), ALLOCATABLE :: BND_TCR0(:)    ! момент образования текущего скопления, с
    REAL(EB), ALLOCATABLE :: BND_QD(:)      ! интенсивность q_Dmax текущего эпизода скопления, м/мин

     ! Агенты (SoA); AG_QNEXT — цепочка очереди либо свободного списка
     REAL(EB), ALLOCATABLE :: AG_X(:), AG_Y(:), AG_V(:), AG_S(:), AG_LAT(:), AG_TOUT(:)
     REAL(EB), ALLOCATABLE :: AG_TNEXT(:), AG_TW(:)  ! момент следующего обновления; накопленное ожидание, с
     INTEGER, ALLOCATABLE :: AG_LINK(:), AG_CLASS(:), AG_XIT(:), AG_STATE(:), AG_ID(:), AG_QNEXT(:)
     INTEGER, ALLOCATABLE :: AG_DIS(:)  ! признак режима разуплотнения (R29d): 1 — ограничение V <= V_qmax
     REAL(EB), ALLOCATABLE :: AG_SOLD(:)  ! снимок позиций на начало шага (синхронность, разд. 8 модели)
     INTEGER, ALLOCATABLE :: AG_PEND(:)   ! отложенная постановка в очередь границы (фаза 3а): 1 — достиг конца
     REAL(EB), ALLOCATABLE :: AG_POVER(:) ! переносимый остаток пути (9) отложенной постановки
     INTEGER, ALLOCATABLE :: AG_PNSG(:)   ! сегмент-адресат отложенной постановки

   ! Источники (&EVHR)
   CHARACTER(60), ALLOCATABLE :: HR_ID(:), HR_CLS(:), HR_EXITID(:)
   REAL(EB), ALLOCATABLE :: HR_X1(:), HR_X2(:), HR_Y1(:), HR_Y2(:), HR_TPRE(:)
   INTEGER, ALLOCATABLE :: HR_N(:), HR_IC(:), HR_IX(:), HR_ISTATE(:)

   ! Маршрутизация: расстояние до выхода (м) и следующая вершина по номеру выхода
   REAL(EB), ALLOCATABLE :: EVP_DEXIT(:,:)
   INTEGER, ALLOCATABLE :: EVP_NH(:,:)

       CHARACTER(255) :: MESSAGE
        INTEGER, PARAMETER :: EVP_REVNUM = 297
        CHARACTER(255), PARAMETER :: EVP_REVDATE = '(24.09.2026)'

CONTAINS

! ---------------------------------------------------------------------------
! Чтение входных данных; вызывается из read.f90 после READ_EVAC
! ---------------------------------------------------------------------------

SUBROUTINE READ_EVAC_PLUS
    INTEGER :: IOS, N, NSG, NEX, NHR, NSOURCE, NTOT, SEED, NLOCAL, RUNS
    REAL(EB) :: DT, SIGMA_V0, PROTOCOL_DT, RELAX_LEN, CV, PROB
    CHARACTER(30) :: MODE
    LOGICAL :: OLD_ENGINE
    NAMELIST /EVPL/ DT, MODE, SEED, SIGMA_V0, NLOCAL, PROTOCOL_DT, RELAX_LEN, RUNS, PROB

   IF (.NOT. EVAC_PLUS) THEN
      ! Группы EVAC+ без включения — явная диагностика (молчаливый пропуск недопустим)
      CALL EVP_SCAN_GROUP('EVPL', NSOURCE)
      IF (NSOURCE > 0) CALL SHUTDOWN('ERROR: &EVPL group requires &MISC EVAC_PLUS=.TRUE.')
      CALL EVP_SCAN_GROUP('EVHR', NSOURCE)
      IF (NSOURCE > 0) CALL SHUTDOWN('ERROR: &EVHR group requires &MISC EVAC_PLUS=.TRUE.')
      CALL EVP_SCAN_GROUP('EVSG', NSOURCE)
      IF (NSOURCE > 0) CALL SHUTDOWN('ERROR: &EVSG group requires &MISC EVAC_PLUS=.TRUE.')
      CALL EVP_SCAN_GROUP('EVEX', NSOURCE)
      IF (NSOURCE > 0) CALL SHUTDOWN('ERROR: &EVEX group requires &MISC EVAC_PLUS=.TRUE.')
      CALL EVP_SCAN_GROUP('EVCP', NSOURCE)
      IF (NSOURCE > 0) CALL SHUTDOWN('ERROR: &EVCP group requires &MISC EVAC_PLUS=.TRUE.')
      RETURN
   ENDIF

   EVP_ON = .TRUE.

   ! Проверки режима
   IF (.NOT. ANY(EVACUATION_GRID)) CALL SHUTDOWN('ERROR: EVAC_PLUS requires an evacuation mesh (&MESH EVAC_HUMANS=.TRUE.)')
   IF (COUNT(EVACUATION_GRID) > 1) CALL SHUTDOWN('ERROR: EVAC+ first version supports one evacuation mesh only')
   IF (RESTART) CALL SHUTDOWN('ERROR: EVAC_PLUS does not support RESTART')
   IF (NO_EVACUATION) CALL SHUTDOWN('ERROR: EVAC_PLUS is incompatible with NO_EVACUATION')

   ! Совместное использование двух движков запрещено
   OLD_ENGINE = .FALSE.
   CALL EVP_SCAN_GROUP('PERS', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('EVAC', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('EXIT', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('DOOR', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('ENTR', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('CORR', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('STRS', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('EVHO', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('EVSS', N); IF (N > 0) OLD_ENGINE = .TRUE.
   CALL EVP_SCAN_GROUP('EDEV', N); IF (N > 0) OLD_ENGINE = .TRUE.
   IF (OLD_ENGINE) CALL SHUTDOWN('ERROR: EVAC_PLUS cannot be combined with FDS+Evac groups (&PERS,&EVAC,&EXIT,&DOOR,...)')

      ! &EVPL — параметры движка
      DT = 0.1_EB
      MODE = 'FIXED'
      SEED = 42
      SIGMA_V0 = 0._EB
      NLOCAL = 8
      PROTOCOL_DT = 0._EB
      RELAX_LEN = 5.0_EB
      RUNS = -1                 ! признак «не задано» — по умолчанию зависит от режима
      PROB = 0.999_EB
   REWIND(LU_INPUT)
   CALL CHECKREAD('EVPL', LU_INPUT, IOS)
   IF (IOS == 0) THEN
      READ(LU_INPUT, EVPL, END=10, ERR=11, IOSTAT=IOS)
      11 IF (IOS > 0) CALL SHUTDOWN('ERROR: Problem with EVPL line')
   ENDIF
   10 REWIND(LU_INPUT)
    IF (DT < 0.05_EB .OR. DT > 0.25_EB) CALL SHUTDOWN('ERROR: EVPL DT must be within [0.05,0.25] s')
    IF (NLOCAL < 0 .OR. NLOCAL > 32) CALL SHUTDOWN('ERROR: EVPL NLOCAL must be within [0,32]')
    IF (PROTOCOL_DT < 0._EB .OR. (PROTOCOL_DT > 0._EB .AND. PROTOCOL_DT < DT)) &
       CALL SHUTDOWN('ERROR: EVPL PROTOCOL_DT must be 0 or at least DT')
    IF (RELAX_LEN < 0._EB .OR. RELAX_LEN > 100._EB) &
       CALL SHUTDOWN('ERROR: EVPL RELAX_LEN must be within [0,100] m')
    EVP_PROTDT = PROTOCOL_DT
    EVP_RELAX = RELAX_LEN
   EVP_SEED = SEED
   EVP_SIGMA_V0 = SIGMA_V0
   EVP_NLOCAL = NLOCAL
     SELECT CASE (TRIM(MODE))
     CASE ('FIXED')
        EVP_MODE = 0
     CASE ('WANDER')
        EVP_MODE = 1
     CASE DEFAULT
        CALL SHUTDOWN('ERROR: EVPL MODE must be FIXED or WANDER')
     END SELECT
     ! Пакетный стохастический режим (этап 6, решение 15): в WANDER по
     ! умолчанию нормативная серия 100 прогонов (П8); в FIXED серия
     ! невозможна — детерминированный одиночный прогон оценке по квантилю
     ! не подлежит
     IF (RUNS < 0) THEN
        IF (EVP_MODE == 1) THEN
           RUNS = 100
        ELSE
           RUNS = 1
        ENDIF
     ENDIF
     IF (RUNS < 1 .OR. RUNS > EVP_RUNSMAX) CALL SHUTDOWN('ERROR: EVPL RUNS must be within [1,100000]')
     IF (PROB <= 0._EB .OR. PROB > 1._EB) CALL SHUTDOWN('ERROR: EVPL PROB must be within (0,1]')
     IF (RUNS > 1 .AND. EVP_MODE == 0) &
        CALL SHUTDOWN('ERROR: EVPL RUNS>1 requires MODE=WANDER (single deterministic run is not subject to quantile estimate)')
     EVP_RUNS = RUNS
     EVP_PROB = PROB
     ALLOCATE(EVP_TSAMP(EVP_RUNS)); ALLOCATE(EVP_TSORT(EVP_RUNS))
     EVP_TSAMP = -1._EB
    EVP_DT = DT
   ! Часы эвакуации: единый постоянный шаг; предфаза старого движка отключена
   EVAC_DT_FLOWFIELD = EVP_DT
   EVAC_DT_STEADY_STATE = EVP_DT
   EVAC_TIME_ITERATIONS = 0

   ! Подсчёт групп
   CALL EVP_SCAN_GROUP('EVSG', NSG)
   CALL EVP_SCAN_GROUP('EVEX', NEX)
   CALL EVP_SCAN_GROUP('EVHR', NHR)
   IF (NSG < 1) CALL SHUTDOWN('ERROR: EVAC+ requires at least one &EVSG segment')
   IF (NEX < 1) CALL SHUTDOWN('ERROR: EVAC+ requires at least one &EVEX exit')
   EVP_NSG = NSG
   EVP_NEX = NEX
   EVP_NHR = NHR
   EVP_NSGTOT = NSG + NEX

   ALLOCATE(SG_ID(EVP_NSGTOT))
   ALLOCATE(SG_X1(EVP_NSGTOT)); ALLOCATE(SG_Y1(EVP_NSGTOT))
   ALLOCATE(SG_X2(EVP_NSGTOT)); ALLOCATE(SG_Y2(EVP_NSGTOT))
   ALLOCATE(SG_Z(EVP_NSGTOT)); ALLOCATE(SG_B(EVP_NSGTOT)); ALLOCATE(SG_LEN(EVP_NSGTOT))
   ALLOCATE(SG_KIND(EVP_NSGTOT)); ALLOCATE(SG_NXT(EVP_NSGTOT,2))
   ALLOCATE(SG_LSTART(EVP_NSGTOT)); ALLOCATE(SG_NLINK(EVP_NSGTOT))
   ALLOCATE(SG_NPRE(EVP_NSGTOT)); ALLOCATE(SG_PRE(EVP_NSGTOT,EVP_MAXPRE))
   ALLOCATE(EVP_NEXTTXT(2,EVP_NSGTOT))
    ALLOCATE(EX_ID(EVP_NEX)); ALLOCATE(EX_SG(EVP_NEX)); ALLOCATE(EX_CNT(EVP_NEX)); ALLOCATE(EX_TLAST(EVP_NEX))
    ALLOCATE(EX_IONLY(EVP_NEX))
   IF (EVP_NHR > 0) THEN
      ALLOCATE(HR_ID(EVP_NHR)); ALLOCATE(HR_CLS(EVP_NHR)); ALLOCATE(HR_EXITID(EVP_NHR))
      ALLOCATE(HR_X1(EVP_NHR)); ALLOCATE(HR_X2(EVP_NHR)); ALLOCATE(HR_Y1(EVP_NHR)); ALLOCATE(HR_Y2(EVP_NHR))
      ALLOCATE(HR_TPRE(EVP_NHR)); ALLOCATE(HR_N(EVP_NHR)); ALLOCATE(HR_IC(EVP_NHR))
      ALLOCATE(HR_IX(EVP_NHR)); ALLOCATE(HR_ISTATE(EVP_NHR))
   ENDIF
    SG_ID = 'null'; SG_KIND = 0; SG_NXT = 0; SG_LSTART = 0; SG_NLINK = 0; SG_NPRE = 0
    EVP_NEXTTXT = 'null'; EX_CNT = 0; EX_TLAST = -1._EB; EX_IONLY = .FALSE.

    CALL EVP_BUILD_CLASSES
    CALL EVP_READ_EVSG
    CALL EVP_READ_EVEX
    CALL EVP_READ_EVHR
    CALL EVP_READ_EVCP
    CALL EVP_RESOLVE_CLASSES
     ! СКО скоростей свободного движения (П8.3): нормативные значения в Методике
     ! отсутствуют. &EVPL/&EVCP SIGMA_V0 — абсолютное значение, м/мин. В
     ! стохастическом режиме (MODE=WANDER) без явного задания SIGMA_V0
     ! применяется коэффициент вариации Cv = sigma(V0)/V0 (разд. 10 п. 7
     ! модели): 0.15 для групп М0, 0.20 для МГН (М1-М4)
      IF (EVP_SIGMA_V0 > 0._EB) THEN
         DO N = 1, EVP_NCLSUSED
            WHERE (CLS(N)%SIG == 0._EB) CLS(N)%SIG = EVP_SIGMA_V0
         ENDDO
      ELSEIF (EVP_MODE == 1) THEN
         DO N = 1, EVP_NCLSUSED
            CV = 0.15_EB
            IF (N >= 8) CV = 0.20_EB
            WHERE (CLS(N)%SIG == 0._EB .AND. CLS(N)%V0 > 0._EB) CLS(N)%SIG = CV*CLS(N)%V0
         ENDDO
      ENDIF
    CALL EVP_RESOLVE_NEXT
    REWIND(LU_INPUT)

   ! Ёмкость агентов: суммарная численность источников + запас
   NTOT = 0
   DO N = 1, EVP_NHR
      NTOT = NTOT + HR_N(N)
   ENDDO
   EVP_NAGMAX = NTOT + 16

END SUBROUTINE READ_EVAC_PLUS


! Подсчёт вхождений группы NAME4 в файле ввода
SUBROUTINE EVP_SCAN_GROUP(NAME4, NCOUNT)
   CHARACTER(4), INTENT(IN) :: NAME4
   INTEGER, INTENT(OUT) :: NCOUNT
   INTEGER :: IOS
   NCOUNT = 0
   REWIND(LU_INPUT)
   DO
      CALL CHECKREAD(NAME4, LU_INPUT, IOS)
      IF (IOS == 1) EXIT
      NCOUNT = NCOUNT + 1
      READ(LU_INPUT, *, END=10)
   ENDDO
10 REWIND(LU_INPUT)
END SUBROUTINE EVP_SCAN_GROUP


SUBROUTINE EVP_READ_EVSG
   REAL(EB) :: XB(6), WIDTH
   CHARACTER(60) :: ID, NEXT(2)
   CHARACTER(30) :: KIND
   INTEGER :: IOS, N, K, J
   NAMELIST /EVSG/ ID, XB, WIDTH, KIND, NEXT

   N = 0
   REWIND(LU_INPUT)
   EVP_SG_LOOP: DO
      CALL CHECKREAD('EVSG', LU_INPUT, IOS)
      IF (IOS == 1) EXIT EVP_SG_LOOP
      ID = 'null'; XB = 0._EB; WIDTH = 1.0_EB; KIND = 'HORIZONTAL'; NEXT = 'null'
      READ(LU_INPUT, EVSG, END=20, ERR=21, IOSTAT=IOS)
      21 IF (IOS > 0) CALL SHUTDOWN('ERROR: Problem with EVSG line')
      N = N + 1
      CALL CHECK_XB(XB)
      SG_ID(N) = TRIM(ID)
      SG_X1(N) = XB(1); SG_Y1(N) = XB(3); SG_X2(N) = XB(2); SG_Y2(N) = XB(4); SG_Z(N) = XB(5)
      SG_B(N) = WIDTH
      SG_KIND(N) = EVP_KIND_INDEX(KIND)
      SG_LEN(N) = SQRT((SG_X2(N)-SG_X1(N))**2 + (SG_Y2(N)-SG_Y1(N))**2)
      IF (WIDTH < 0.7_EB) THEN
         WRITE(MESSAGE,'(A,A,A,F6.2)') 'ERROR: EVSG ',TRIM(ID),': WIDTH below 0.7 m: ',WIDTH
         CALL SHUTDOWN(MESSAGE)
      ENDIF
      IF (SG_KIND(N) /= 2 .AND. SG_LEN(N) < 0.05_EB) THEN
         WRITE(MESSAGE,'(A,A,A,F6.2)') 'ERROR: EVSG ',TRIM(ID),': zero length allowed for DOOR only, length = ',SG_LEN(N)
         CALL SHUTDOWN(MESSAGE)
      ENDIF
      EVP_NEXTTXT(1,N) = TRIM(NEXT(1))
      EVP_NEXTTXT(2,N) = TRIM(NEXT(2))
   ENDDO EVP_SG_LOOP
20 CONTINUE

   DO K = 1, EVP_NSG
      DO J = K+1, EVP_NSG
         IF (TRIM(SG_ID(K)) == TRIM(SG_ID(J))) THEN
            WRITE(MESSAGE,'(A,A)') 'ERROR: EVSG duplicate ID: ',TRIM(SG_ID(K))
            CALL SHUTDOWN(MESSAGE)
         ENDIF
      ENDDO
   ENDDO
   REWIND(LU_INPUT)
END SUBROUTINE EVP_READ_EVSG


SUBROUTINE EVP_READ_EVEX
   REAL(EB) :: XB(6), WIDTH
   CHARACTER(60) :: ID
   LOGICAL :: COUNT_ONLY
   INTEGER :: IOS, N, K, J, SGN
   NAMELIST /EVEX/ ID, XB, WIDTH, COUNT_ONLY

   N = 0
   REWIND(LU_INPUT)
   EVP_EX_LOOP: DO
      CALL CHECKREAD('EVEX', LU_INPUT, IOS)
      IF (IOS == 1) EXIT EVP_EX_LOOP
      ID = 'null'; XB = 0._EB; WIDTH = 1.0_EB; COUNT_ONLY = .FALSE.
      READ(LU_INPUT, EVEX, END=20, ERR=21, IOSTAT=IOS)
      21 IF (IOS > 0) CALL SHUTDOWN('ERROR: Problem with EVEX line')
      N = N + 1
      CALL CHECK_XB(XB)
      SGN = EVP_NSG + N
      SG_ID(SGN) = TRIM(ID)
      SG_X1(SGN) = XB(1); SG_Y1(SGN) = XB(3); SG_X2(SGN) = XB(2); SG_Y2(SGN) = XB(4); SG_Z(SGN) = XB(5)
      SG_B(SGN) = WIDTH
      SG_KIND(SGN) = EVP_KIND_EXIT
      SG_LEN(SGN) = 0._EB
      IF (WIDTH < 0.7_EB) THEN
         WRITE(MESSAGE,'(A,A,A,F6.2)') 'ERROR: EVEX ',TRIM(ID),': WIDTH below 0.7 m: ',WIDTH
         CALL SHUTDOWN(MESSAGE)
      ENDIF
      EX_ID(N) = TRIM(ID)
      EX_SG(N) = SGN
      EX_IONLY(N) = COUNT_ONLY   ! выход без ограничения пропускной способности (только счёт)
   ENDDO EVP_EX_LOOP
20 CONTINUE

   DO K = 1, EVP_NEX
      DO J = K+1, EVP_NEX
         IF (TRIM(EX_ID(K)) == TRIM(EX_ID(J))) THEN
            WRITE(MESSAGE,'(A,A)') 'ERROR: EVEX duplicate ID: ',TRIM(EX_ID(K))
            CALL SHUTDOWN(MESSAGE)
         ENDIF
      ENDDO
   ENDDO
   REWIND(LU_INPUT)
END SUBROUTINE EVP_READ_EVEX


SUBROUTINE EVP_READ_EVHR
   REAL(EB) :: XB(6), T_PRE
   CHARACTER(60) :: ID, EXIT_ID
   CHARACTER(30) :: CLASS
   INTEGER :: IOS, J, N
   NAMELIST /EVHR/ ID, XB, N, CLASS, T_PRE, EXIT_ID

   J = 0
   REWIND(LU_INPUT)
   EVP_HR_LOOP: DO
      CALL CHECKREAD('EVHR', LU_INPUT, IOS)
      IF (IOS == 1) EXIT EVP_HR_LOOP
      ID = 'null'; XB = 0._EB; N = 0; CLASS = 'M0-3'; T_PRE = 0._EB; EXIT_ID = 'null'
      READ(LU_INPUT, EVHR, END=20, ERR=21, IOSTAT=IOS)
      21 IF (IOS > 0) CALL SHUTDOWN('ERROR: Problem with EVHR line')
      J = J + 1
      IF (N < 1) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVHR ',TRIM(ID),': N must be positive'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
      CALL CHECK_XB(XB)
      IF (XB(2)-XB(1) < 0.01_EB .OR. XB(4)-XB(3) < 0.01_EB) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVHR ',TRIM(ID),': degenerate placement rectangle'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
      HR_ID(J) = TRIM(ID)
      HR_X1(J) = XB(1); HR_X2(J) = XB(2); HR_Y1(J) = XB(3); HR_Y2(J) = XB(4)
      HR_N(J) = N
      HR_CLS(J) = TRIM(CLASS)
      HR_EXITID(J) = TRIM(EXIT_ID)
      HR_TPRE(J) = T_PRE
      ! HR_IC — отложенное разрешение имени класса (EVP_RESOLVE_CLASSES):
      ! пользовательские классы регистрируются строками &EVCP ниже по файлу
      HR_IX(J) = 0
      HR_ISTATE(J) = 0
   ENDDO EVP_HR_LOOP
20 CONTINUE
   REWIND(LU_INPUT)
END SUBROUTINE EVP_READ_EVHR


SUBROUTINE EVP_READ_EVCP
   REAL(EB) :: V0, A, D0, F, SIGMA_V0
   CHARACTER(30) :: CLASS, KIND
   LOGICAL :: DEFINE
   INTEGER :: IOS, IC, IK, IPASS
   NAMELIST /EVCP/ CLASS, KIND, V0, A, D0, F, SIGMA_V0, DEFINE

   ! Два прохода (R31): проход 1 регистрирует пользовательские классы
   ! (DEFINE=.TRUE.), проход 2 применяет параметры — порядок строк &EVCP
   ! (в том числе относительно &EVHR) не влияет на результат
   DO IPASS = 1, 2
      REWIND(LU_INPUT)
      EVP_CP_LOOP: DO
         CALL CHECKREAD('EVCP', LU_INPUT, IOS)
         IF (IOS == 1) EXIT EVP_CP_LOOP
         CLASS = 'M0-3'; KIND = 'HORIZONTAL'; V0 = -1._EB; A = -1._EB; D0 = -1._EB
         F = -1._EB; SIGMA_V0 = 0._EB; DEFINE = .FALSE.
         READ(LU_INPUT, EVCP, END=20, ERR=21, IOSTAT=IOS)
         21 IF (IOS > 0) CALL SHUTDOWN('ERROR: Problem with EVCP line')
         IF (IPASS == 1) THEN
            IF (DEFINE) CALL EVP_REGISTER_CLASS(TRIM(CLASS))
            CYCLE
         ENDIF
         IC = EVP_CLASS_INDEX(TRIM(CLASS))
         IK = EVP_KIND_INDEX(TRIM(KIND))
         IF (V0 <= 0._EB .OR. A <= 0._EB .OR. D0 <= 0._EB) THEN
            WRITE(MESSAGE,'(A,A,A,A)') 'ERROR: EVCP ',TRIM(CLASS),' ',TRIM(KIND),': V0, A, D0 must be positive'
            CALL SHUTDOWN(MESSAGE)
         ENDIF
         CLS(IC)%V0(IK) = V0
         CLS(IC)%AV(IK) = A
         CLS(IC)%D0(IK) = D0
         CLS(IC)%SIG(IK) = SIGMA_V0
         CLS(IC)%DEF(IK) = .TRUE.
         IF (F > 0._EB) CLS(IC)%F = F
      ENDDO EVP_CP_LOOP
   ENDDO
 20 CONTINUE
   REWIND(LU_INPUT)
END SUBROUTINE EVP_READ_EVCP


! Регистрация пользовательского класса мобильности (&EVCP DEFINE=.TRUE.):
! имя 1-10 символов (латиница, цифры, дефис, подчёркивание), без совпадений
! со встроенными и уже определёнными; повторная регистрация — без действия;
! параметры задаются строками &EVCP (в том числе со строки DEFINE)
SUBROUTINE EVP_REGISTER_CLASS(CLSSTR)
   CHARACTER(*), INTENT(IN) :: CLSSTR
   INTEGER :: N, C

   IF (LEN_TRIM(CLSSTR) == 0 .OR. LEN_TRIM(CLSSTR) > 10) THEN
      WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVCP ',TRIM(CLSSTR),': custom class name must be 1-10 characters'
      CALL SHUTDOWN(MESSAGE)
   ENDIF
   DO N = 1, LEN_TRIM(CLSSTR)
      C = IACHAR(CLSSTR(N:N))
      IF (.NOT. ((C >= 65 .AND. C <= 90) .OR. (C >= 97 .AND. C <= 122) .OR. &
                 (C >= 48 .AND. C <= 57) .OR. C == 45 .OR. C == 95)) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVCP ',TRIM(CLSSTR),': custom class name allows A-Z, a-z, 0-9, - and _ only'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
   ENDDO
   IF (EVP_CLASS_LOOKUP(CLSSTR) > 0) RETURN
   IF (EVP_NCLSUSED >= EVP_NCLASS_MAX) THEN
      WRITE(MESSAGE,'(A,I0,A)') 'ERROR: EVCP too many custom classes (max ',EVP_NCLASS_MAX-EVP_NCLASS,')'
      CALL SHUTDOWN(MESSAGE)
   ENDIF
   EVP_NCLSUSED = EVP_NCLSUSED + 1
   CLS(EVP_NCLSUSED)%V0 = 0._EB
   CLS(EVP_NCLSUSED)%AV = 0._EB
   CLS(EVP_NCLSUSED)%D0 = 0._EB
   CLS(EVP_NCLSUSED)%SIG = 0._EB
   CLS(EVP_NCLSUSED)%F = 0._EB
   CLS(EVP_NCLSUSED)%DEF = .FALSE.
   CLS(EVP_NCLSUSED)%NAME = TRIM(CLSSTR)
END SUBROUTINE EVP_REGISTER_CLASS


! Отложенное разрешение имён классов источников (после &EVCP) и контроль F:
! каждому &EVHR классу требуется положительная площадь проекции f
SUBROUTINE EVP_RESOLVE_CLASSES
   INTEGER :: N

   DO N = 1, EVP_NHR
      HR_IC(N) = EVP_CLASS_INDEX(TRIM(HR_CLS(N)))
      IF (CLS(HR_IC(N))%F <= 0._EB) THEN
         WRITE(MESSAGE,'(A,A,A,A,A)') 'ERROR: EVHR ',TRIM(HR_ID(N)),': class ',TRIM(HR_CLS(N)), &
            ' has no F (set &EVCP F=...)'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
   ENDDO
END SUBROUTINE EVP_RESOLVE_CLASSES


! Разбор текстовых ссылок NEXT в индексы сегментов/выходов; построение предшественников
SUBROUTINE EVP_RESOLVE_NEXT
   INTEGER :: S, J, T, N
   LOGICAL :: FOUND

   DO S = 1, EVP_NSG
      DO J = 1, 2
         IF (TRIM(EVP_NEXTTXT(J,S)) == 'null') THEN
            SG_NXT(S,J) = 0
            CYCLE
         ENDIF
         FOUND = .FALSE.
         DO T = 1, EVP_NSGTOT
            IF (TRIM(EVP_NEXTTXT(J,S)) == TRIM(SG_ID(T))) THEN
               SG_NXT(S,J) = T
               FOUND = .TRUE.
               EXIT
            ENDIF
         ENDDO
         IF (.NOT. FOUND) THEN
            WRITE(MESSAGE,'(A,A,A,A)') 'ERROR: EVSG ',TRIM(SG_ID(S)),': unknown NEXT ID ',TRIM(EVP_NEXTTXT(J,S))
            CALL SHUTDOWN(MESSAGE)
         ENDIF
      ENDDO
   ENDDO

   ! NEXT(1) обязателен для каждого пользовательского сегмента (кроме выходов)
   DO S = 1, EVP_NSG
      IF (SG_NXT(S,1) == 0) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVSG ',TRIM(SG_ID(S)),': dead end (NEXT(1) is not set)'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
      IF (SG_NXT(S,1) == SG_NXT(S,2)) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVSG ',TRIM(SG_ID(S)),': NEXT(1) equals NEXT(2)'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
      IF (SG_NXT(S,1) == S .OR. SG_NXT(S,2) == S) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVSG ',TRIM(SG_ID(S)),': NEXT refers to the segment itself'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
   ENDDO

   ! Предшественники (для слияний)
   DO S = 1, EVP_NSGTOT
      DO J = 1, 2
         T = SG_NXT(S,J)
         IF (T == 0) CYCLE
         IF (SG_NPRE(T) >= EVP_MAXPRE) THEN
            WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVSG ',TRIM(SG_ID(T)),': too many predecessors'
            CALL SHUTDOWN(MESSAGE)
         ENDIF
         SG_NPRE(T) = SG_NPRE(T) + 1
         SG_PRE(T,SG_NPRE(T)) = S
      ENDDO
   ENDDO

   ! Явное указание выхода источником: HR_EXITID -> индекс выхода
   DO N = 1, EVP_NHR
      IF (TRIM(HR_EXITID(N)) == 'null') CYCLE
      FOUND = .FALSE.
      DO S = 1, EVP_NEX
         IF (TRIM(HR_EXITID(N)) == TRIM(EX_ID(S))) THEN
            HR_IX(N) = S
            FOUND = .TRUE.
            EXIT
         ENDIF
      ENDDO
      IF (.NOT. FOUND) THEN
         WRITE(MESSAGE,'(A,A,A,A)') 'ERROR: EVHR ',TRIM(HR_ID(N)),': unknown EXIT_ID ',TRIM(HR_EXITID(N))
         CALL SHUTDOWN(MESSAGE)
      ENDIF
   ENDDO

END SUBROUTINE EVP_RESOLVE_NEXT


! Классы мобильности по таблицам П2.6/П2.7 (01_MATH_MODEL.md, разд. 3)
SUBROUTINE EVP_BUILD_CLASSES
   INTEGER :: I

   DO I = 1, EVP_NCLASS
      CLS(I)%V0 = 0._EB; CLS(I)%AV = 0._EB; CLS(I)%D0 = 0._EB; CLS(I)%SIG = 0._EB
      CLS(I)%F = 0._EB; CLS(I)%DEF = .FALSE.
   ENDDO
    CLS(1:EVP_NCLASS)%NAME = (/ 'M0-1','M0-2','M0-3','M0-4','M0-5','M0-6','M0-7', &
                                'M1  ','M2  ','M3  ','M4  ', &
                                'M1D ','M1C ','M1G ','M1B ','M2S ' /)

   ! М0-1
   CLS(1)%V0(1:4) = (/ 92.6_EB, 92.6_EB, 92.4_EB, 65.9_EB/)
   CLS(1)%AV(1:4) = (/0.284_EB,0.350_EB,0.338_EB,0.289_EB/)
   CLS(1)%D0(1:4) = (/ 0.75_EB, 1.20_EB, 0.94_EB, 0.84_EB/)
   CLS(1)%DEF(1:4) = .TRUE.
   CLS(1)%F = 0.06_EB
    ! М0-2: горизонтальный путь a = 0,308 — erratum П2.6 (в первоисточнике
    ! 0,508 блокирует достижение q_max 16,5; решение владельца от 23.09.2026)
    ! М0-2
   CLS(2)%V0(1:4) = (/120.0_EB,120.0_EB,129.0_EB, 76.8_EB/)
   CLS(2)%AV(1:4) = (/0.308_EB,0.308_EB,0.353_EB,0.305_EB/)
   CLS(2)%D0(1:4) = (/ 0.72_EB, 0.53_EB, 0.58_EB, 0.67_EB/)
   CLS(2)%DEF(1:4) = .TRUE.
   CLS(2)%F = 0.09_EB
   ! М0-3
   CLS(3)%V0(1:4) = (/100.0_EB,100.0_EB,100.0_EB, 60.0_EB/)
   CLS(3)%AV(1:4) = (/0.295_EB,0.295_EB,0.400_EB,0.305_EB/)
   CLS(3)%D0(1:4) = (/ 0.51_EB, 0.65_EB, 0.89_EB, 0.67_EB/)
   CLS(3)%DEF(1:4) = .TRUE.
   CLS(3)%F = 0.10_EB
   ! М0-4
   CLS(4)%V0(1:4) = (/ 93.8_EB, 93.8_EB, 93.8_EB, 57.5_EB/)
   CLS(4)%AV(1:4) = (/0.353_EB,0.371_EB,0.394_EB,0.375_EB/)
   CLS(4)%D0(1:4) = (/ 0.56_EB, 0.64_EB, 0.75_EB, 0.66_EB/)
   CLS(4)%DEF(1:4) = .TRUE.
   CLS(4)%F = 0.09_EB
   ! М0-5
   CLS(5)%V0(1:4) = (/ 91.4_EB, 91.8_EB, 90.0_EB, 56.1_EB/)
   CLS(5)%AV(1:4) = (/0.357_EB,0.366_EB,0.410_EB,0.379_EB/)
   CLS(5)%D0(1:4) = (/ 0.58_EB, 0.62_EB, 0.83_EB, 0.68_EB/)
   CLS(5)%DEF(1:4) = .TRUE.
   CLS(5)%F = 0.121_EB
   ! М0-6
   CLS(6)%V0(1:4) = (/ 69.6_EB, 72.1_EB, 61.7_EB, 43.5_EB/)
   CLS(6)%AV(1:4) = (/0.385_EB,0.318_EB,0.394_EB,0.400_EB/)
   CLS(6)%D0(1:4) = (/ 0.71_EB, 0.41_EB, 0.75_EB, 0.78_EB/)
   CLS(6)%DEF(1:4) = .TRUE.
   CLS(6)%F = 0.127_EB
   ! М0-7
   CLS(7)%V0(1:4) = (/ 45.02_EB, 50.0_EB, 30.0_EB, 30.0_EB/)
   CLS(7)%AV(1:4) = (/0.425_EB,0.253_EB,0.367_EB,0.414_EB/)
   CLS(7)%D0(1:4) = (/ 0.86_EB, 0.18_EB, 0.62_EB, 0.88_EB/)
   CLS(7)%DEF(1:4) = .TRUE.
   CLS(7)%F = 0.121_EB
    ! М1 (пожилые): все виды пути (полная П2.7)
    CLS(8)%V0(1) = 80.0_EB;  CLS(8)%AV(1) = 0.295_EB; CLS(8)%D0(1) = 0.51_EB
    CLS(8)%V0(2) = 80.0_EB;  CLS(8)%AV(2) = 0.295_EB; CLS(8)%D0(2) = 0.65_EB
    CLS(8)%V0(3) = 70.0_EB;  CLS(8)%AV(3) = 0.400_EB; CLS(8)%D0(3) = 0.89_EB
    CLS(8)%V0(4) = 60.0_EB;  CLS(8)%AV(4) = 0.305_EB; CLS(8)%D0(4) = 0.67_EB
    CLS(8)%DEF(1:4) = .TRUE.
    CLS(8)%F = 0.125_EB
    ! М2 (пожилые немощные): все шесть видов пути (полная П2.7)
    CLS(9)%V0(1) = 25.0_EB;  CLS(9)%AV(1) = 0.428_EB; CLS(9)%D0(1) = 0.96_EB
    CLS(9)%V0(2) = 20.0_EB;  CLS(9)%AV(2) = 0.456_EB; CLS(9)%D0(2) = 1.02_EB
    CLS(9)%V0(3) = 20.0_EB;  CLS(9)%AV(3) = 0.505_EB; CLS(9)%D0(3) = 1.26_EB
    CLS(9)%V0(4) = 20.0_EB;  CLS(9)%AV(4) = 0.338_EB; CLS(9)%D0(4) = 0.56_EB
    CLS(9)%V0(5) = 25.0_EB;  CLS(9)%AV(5) = 0.353_EB; CLS(9)%D0(5) = 0.58_EB
    CLS(9)%V0(6) = 15.0_EB;  CLS(9)%AV(6) = 0.368_EB; CLS(9)%D0(6) = 0.72_EB
    CLS(9)%DEF(1:6) = .TRUE.
    CLS(9)%F = 0.200_EB
    ! М3 (поражение опорно-двигательного аппарата): все виды пути (полная П2.7);
    ! f = 0,200 (одна опора), две опоры — &EVCP F=0.300
    CLS(10)%V0(1) = 44.0_EB; CLS(10)%AV(1) = 0.414_EB; CLS(10)%D0(1) = 0.77_EB
    CLS(10)%V0(2) = 38.0_EB; CLS(10)%AV(2) = 0.345_EB; CLS(10)%D0(2) = 0.57_EB
    CLS(10)%V0(3) = 24.0_EB; CLS(10)%AV(3) = 0.422_EB; CLS(10)%D0(3) = 0.96_EB
    CLS(10)%V0(4) = 14.0_EB; CLS(10)%AV(4) = 0.313_EB; CLS(10)%D0(4) = 0.74_EB
    CLS(10)%DEF(1:4) = .TRUE.
    CLS(10)%F = 0.200_EB
    ! М4 (кресло-коляска): П2.7 нормирует только горизонтальный путь и пандусы;
    ! проём и лестницы для М4 в первоисточнике отсутствуют
    CLS(11)%V0(1) = 60.0_EB; CLS(11)%AV(1) = 0.400_EB; CLS(11)%D0(1) = 0.141_EB
   CLS(11)%V0(5) = 60.0_EB; CLS(11)%AV(5) = 0.400_EB; CLS(11)%D0(5) = 0.141_EB
   CLS(11)%V0(6) = 40.0_EB; CLS(11)%AV(6) = 0.420_EB; CLS(11)%D0(6) = 0.156_EB
   CLS(11)%DEF(1) = .TRUE.; CLS(11)%DEF(5) = .TRUE.; CLS(11)%DEF(6) = .TRUE.
   CLS(11)%F = 0.96_EB
    ! Подкатегории П2.7 (решение 23): 4 вида пути; пандусы не нормированы
    ! (абзац после П2.7) — DEF(5:6) = .FALSE., при необходимости — &EVCP
    ! M1D — дошкольники (дети 3-7 лет); f = 0,03 (п. 10 П2)
    CLS(12)%V0(1:4) = (/ 60.0_EB, 60.0_EB, 47.0_EB, 47.0_EB/)
    CLS(12)%AV(1:4) = (/0.275_EB,0.350_EB,0.190_EB,0.275_EB/)
    CLS(12)%D0(1:4) = (/ 0.78_EB, 1.20_EB, 0.64_EB, 0.76_EB/)
    CLS(12)%DEF(1:4) = .TRUE.
    CLS(12)%F = 0.03_EB
    ! M1C — дети с ограниченными возможностями; f = 0,15
    CLS(13)%V0(1:4) = (/ 51.0_EB, 47.0_EB, 23.0_EB, 20.0_EB/)
    CLS(13)%AV(1:4) = (/0.290_EB,0.300_EB,0.210_EB,0.300_EB/)
    CLS(13)%D0(1:4) = (/ 0.60_EB, 0.67_EB, 0.63_EB, 0.69_EB/)
    CLS(13)%DEF(1:4) = .TRUE.
    CLS(13)%F = 0.15_EB
    ! M1G — глухие и слабослышащие молодого и среднего возраста; f = 0,125
    ! (пожилые немощные, в том числе глухие и слабослышащие, f = 0,200 — &EVCP F)
    CLS(14)%V0(1:4) = (/ 82.0_EB, 82.0_EB, 82.0_EB, 54.0_EB/)
    CLS(14)%AV(1:4) = (/0.301_EB,0.328_EB,0.380_EB,0.344_EB/)
    CLS(14)%D0(1:4) = (/ 0.58_EB, 0.73_EB, 0.91_EB, 0.72_EB/)
    CLS(14)%DEF(1:4) = .TRUE.
    CLS(14)%F = 0.125_EB
    ! M1B — беременные женщины; f = 0,15
    CLS(15)%V0(1:4) = (/56.42_EB,49.47_EB,42.35_EB,31.25_EB/)
    CLS(15)%AV(1:4) = (/0.404_EB,0.427_EB,0.336_EB,0.411_EB/)
    CLS(15)%D0(1:4) = (/0.991_EB,1.033_EB,0.786_EB,1.312_EB/)
    CLS(15)%DEF(1:4) = .TRUE.
    CLS(15)%F = 0.15_EB
    ! M2S — слепые и слабовидящие с тростью (М2 по П2.2); f = 0,4
    CLS(16)%V0(1:4) = (/ 26.0_EB, 17.0_EB, 21.0_EB, 18.0_EB/)
    CLS(16)%AV(1:4) = (/0.371_EB,0.271_EB,0.519_EB,0.387_EB/)
    CLS(16)%D0(1:4) = (/ 0.73_EB, 0.77_EB, 0.97_EB, 0.82_EB/)
    CLS(16)%DEF(1:4) = .TRUE.
    CLS(16)%F = 0.4_EB
    EVP_NCLSUSED = EVP_NCLASS

END SUBROUTINE EVP_BUILD_CLASSES


! ---------------------------------------------------------------------------
! Инициализация: звенья, маршруты, границы, агенты; вызывается из main.f90
! ---------------------------------------------------------------------------

SUBROUTINE EVAC_PLUS_INITIALIZE(NM)
   INTEGER, INTENT(IN) :: NM
   INTEGER :: S, K, N, NSD, KLOC, NL, QS(1)
   INTEGER, ALLOCATABLE :: VIS(:), BFSQ(:)
   INTEGER, ALLOCATABLE :: SEED_ARR(:)
   REAL(EB) :: UX, UY, XX, YY, LCOV

   IF (EVP_READY) RETURN
   IF (.NOT. EVP_ON) RETURN
   IF (.NOT. (EVACUATION_ONLY(NM) .AND. EVACUATION_GRID(NM))) RETURN
   IF (USE_MPI .AND. MYID /= PROCESS(NM)) RETURN

   ! Воспроизводимость: фиксированное зерно генератора
   CALL RANDOM_SEED(SIZE=NSD)
   ALLOCATE(SEED_ARR(NSD))
   SEED_ARR = EVP_SEED + (/ (K-1, K=1,NSD) /)
   CALL RANDOM_SEED(PUT=SEED_ARR)
   DEALLOCATE(SEED_ARR)

   ! Звенья: разбиение сегментов на отрезки базовой длины 1 м
   EVP_NLK = 0
   DO S = 1, EVP_NSG
      IF (SG_LEN(S) >= 0.05_EB) EVP_NLK = EVP_NLK + MAX(1, NINT(SG_LEN(S)/EVP_LINK_LEN))
   ENDDO
   ALLOCATE(LK_SEG(EVP_NLK)); ALLOCATE(LK_NEXT(EVP_NLK)); ALLOCATE(LK_KIND(EVP_NLK))
   ALLOCATE(LK_X0(EVP_NLK)); ALLOCATE(LK_Y0(EVP_NLK))
   ALLOCATE(LK_UX(EVP_NLK)); ALLOCATE(LK_UY(EVP_NLK))
    ALLOCATE(LK_LEN(EVP_NLK)); ALLOCATE(LK_B(EVP_NLK)); ALLOCATE(LK_D(EVP_NLK))
     ALLOCATE(LK_CNT(EVP_NLK)); ALLOCATE(LK_LAST(EVP_NLK))
     ALLOCATE(LK_MOV(EVP_NLK)); ALLOCATE(LK_OFF(EVP_NLK)); ALLOCATE(LK_CUR(EVP_NLK))
     ALLOCATE(LK_MC(EVP_NLK,EVP_NCLASS_MAX))
     ALLOCATE(EVP_WSV(EVP_NCHUNK,EVP_NLK)); ALLOCATE(EVP_WSFV(EVP_NCHUNK,EVP_NLK))
     ALLOCATE(LK_FA(EVP_NLK)); ALLOCATE(LK_DERR(EVP_NLK)); ALLOCATE(LK_SYNC(EVP_NLK))
     ALLOCATE(LK_SV(EVP_NLK)); ALLOCATE(LK_SFV(EVP_NLK)); ALLOCATE(EVP_XCNT(EVP_NLK))
     LK_CNT = 0; LK_D = 0._EB; LK_MOV = 0; LK_OFF = 0; LK_MC = 0
     LK_FA = 0._EB; LK_DERR = 0._EB; LK_SYNC = .FALSE.

   KLOC = 0
   DO S = 1, EVP_NSG
      IF (SG_LEN(S) < 0.05_EB) THEN
         SG_LSTART(S) = 0; SG_NLINK(S) = 0
         CYCLE
      ENDIF
      NL = MAX(1, NINT(SG_LEN(S)/EVP_LINK_LEN))
      SG_LSTART(S) = KLOC + 1
      SG_NLINK(S) = NL
      UX = 0._EB; UY = 0._EB
      IF (SG_LEN(S) > 0._EB) THEN
         UX = (SG_X2(S)-SG_X1(S))/SG_LEN(S)
         UY = (SG_Y2(S)-SG_Y1(S))/SG_LEN(S)
      ENDIF
      DO N = 1, NL
         KLOC = KLOC + 1
         XX = SG_X1(S) + UX*SG_LEN(S)*REAL(N-1,EB)/REAL(NL,EB)
         YY = SG_Y1(S) + UY*SG_LEN(S)*REAL(N-1,EB)/REAL(NL,EB)
         LK_SEG(KLOC) = S
         LK_KIND(KLOC) = SG_KIND(S)
         LK_X0(KLOC) = XX; LK_Y0(KLOC) = YY
         LK_UX(KLOC) = UX; LK_UY(KLOC) = UY
         LK_LEN(KLOC) = SG_LEN(S)/REAL(NL,EB)
         LK_B(KLOC) = SG_B(S)
         LK_NEXT(KLOC) = KLOC + 1
         LK_LAST(KLOC) = (N == NL)
      ENDDO
      LK_NEXT(KLOC) = 0
   ENDDO

   ! Маршруты: расстояние до каждого выхода и следующая вершина (Беллман-Форд)
   ALLOCATE(EVP_DEXIT(EVP_NSGTOT,EVP_NEX))
   ALLOCATE(EVP_NH(EVP_NSGTOT,EVP_NEX))
   EVP_DEXIT = HUGE(1._EB)
   EVP_NH = 0
   DO N = 1, EVP_NEX
      EVP_DEXIT(EX_SG(N),N) = 0._EB
   ENDDO
   CALL EVP_BUILD_ROUTES
   DO S = 1, EVP_NSG
      QS = MINLOC(EVP_DEXIT(S,1:EVP_NEX))
      IF (EVP_DEXIT(S,QS(1)) >= HUGE(1._EB)) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVSG ',TRIM(SG_ID(S)),': no route to any exit'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
   ENDDO

   ! Проверка (класс, вид пути) по всем достижимым сегментам каждого источника
   ALLOCATE(VIS(EVP_NSGTOT)); ALLOCATE(BFSQ(EVP_NSGTOT))
   DO N = 1, EVP_NHR
      CALL EVP_SOURCE_SEGMENT(N, S)
      VIS = 0
      CALL EVP_BFS(S, VIS, BFSQ)
      DO S = 1, EVP_NSGTOT
         IF (VIS(S) == 0) CYCLE
         IF (SG_LEN(S) < 0.05_EB .OR. SG_KIND(S) == EVP_KIND_EXIT) CYCLE
         IF (.NOT. CLS(HR_IC(N))%DEF(SG_KIND(S))) THEN
            WRITE(MESSAGE,'(A,A,A,A,A,A)') 'ERROR: EVHR ',TRIM(HR_ID(N)),': class ',TRIM(CLS(HR_IC(N))%NAME), &
               ' has no parameters for path kind of segment ',TRIM(SG_ID(S))
            CALL SHUTDOWN(MESSAGE)
         ENDIF
      ENDDO
   ENDDO
   DEALLOCATE(VIS); DEALLOCATE(BFSQ)

   ! Покрытие источников звеньями (сухой прогон размещения)
   DO N = 1, EVP_NHR
      CALL EVP_SOURCE_COVERAGE(N, LCOV)
      IF (LCOV < 0.01_EB) THEN
         WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVHR ',TRIM(HR_ID(N)),': rectangle covers no segment links'
         CALL SHUTDOWN(MESSAGE)
      ENDIF
   ENDDO

    ! Границы и агенты
    ALLOCATE(BND_QHEAD(EVP_NSGTOT)); ALLOCATE(BND_QTAIL(EVP_NSGTOT)); ALLOCATE(BND_QLEN(EVP_NSGTOT))
    ALLOCATE(BND_LEFT(EVP_NSGTOT)); ALLOCATE(BND_CROWD(EVP_NSGTOT))
    ALLOCATE(BND_RESID(EVP_NSGTOT)); ALLOCATE(BND_CR(EVP_NSGTOT,EVP_MAXPRE))
    ALLOCATE(BND_NPASS(EVP_NSGTOT)); ALLOCATE(BND_QMAX(EVP_NSGTOT)); ALLOCATE(BND_NEP(EVP_NSGTOT))
    ALLOCATE(BND_TWSUM(EVP_NSGTOT)); ALLOCATE(BND_TMAXW(EVP_NSGTOT)); ALLOCATE(BND_ACROWD(EVP_NSGTOT))
     ALLOCATE(BND_TSK_SIM(EVP_NSGTOT)); ALLOCATE(BND_TSK_EST(EVP_NSGTOT)); ALLOCATE(BND_TCR0(EVP_NSGTOT))
     ALLOCATE(BND_QD(EVP_NSGTOT))
     ALLOCATE(SG_NIN(EVP_NSGTOT)); ALLOCATE(SG_NOUT(EVP_NSGTOT)); ALLOCATE(SG_NPLACE(EVP_NSGTOT))
     ALLOCATE(SG_NEXIT(EVP_NSGTOT)); ALLOCATE(SG_NGONE(EVP_NSGTOT))
     SG_NIN = 0; SG_NOUT = 0; SG_NPLACE = 0; SG_NEXIT = 0; SG_NGONE = 0
     EVP_CLSGEN = 0; EVP_CLSEVAC = 0
    BND_QHEAD = 0; BND_QTAIL = 0; BND_QLEN = 0; BND_LEFT = .FALSE.; BND_CROWD = .FALSE.
    BND_RESID = 0._EB; BND_CR = 0._EB
    BND_NPASS = 0; BND_QMAX = 0; BND_NEP = 0
    BND_TWSUM = 0._EB; BND_TMAXW = 0._EB; BND_ACROWD = 0._EB
    BND_TSK_SIM = 0._EB; BND_TSK_EST = 0._EB; BND_TCR0 = 0._EB; BND_QD = 0._EB
    ALLOCATE(AG_X(EVP_NAGMAX)); ALLOCATE(AG_Y(EVP_NAGMAX)); ALLOCATE(AG_V(EVP_NAGMAX))
    ALLOCATE(AG_S(EVP_NAGMAX)); ALLOCATE(AG_LAT(EVP_NAGMAX)); ALLOCATE(AG_TOUT(EVP_NAGMAX))
    ALLOCATE(AG_TNEXT(EVP_NAGMAX)); ALLOCATE(AG_TW(EVP_NAGMAX))
     ALLOCATE(AG_LINK(EVP_NAGMAX)); ALLOCATE(AG_CLASS(EVP_NAGMAX)); ALLOCATE(AG_XIT(EVP_NAGMAX))
     ALLOCATE(AG_STATE(EVP_NAGMAX)); ALLOCATE(AG_ID(EVP_NAGMAX)); ALLOCATE(AG_QNEXT(EVP_NAGMAX))
     ALLOCATE(AG_ORDER(EVP_NAGMAX)); ALLOCATE(AG_POS(EVP_NAGMAX))
     ALLOCATE(AG_DIS(EVP_NAGMAX))
     ALLOCATE(AG_SOLD(EVP_NAGMAX)); ALLOCATE(AG_PEND(EVP_NAGMAX))
     ALLOCATE(AG_POVER(EVP_NAGMAX)); ALLOCATE(AG_PNSG(EVP_NAGMAX))
     AG_STATE = 2; AG_TNEXT = 0._EB; AG_TW = 0._EB; AG_DIS = 0; AG_PEND = 0
   EVP_NAG = EVP_NAGMAX
   EVP_FREE = 0
   DO K = EVP_NAGMAX, 1, -1
      AG_QNEXT(K) = EVP_FREE
      EVP_FREE = K
   ENDDO
   EVP_NALIVE = 0; EVP_NEVAC = 0; EVP_ILABEL = 0; EVP_TLAST = -1._EB

    ! Источники с T_PRE <= T_BEGIN размещаются сразу
    DO N = 1, EVP_NHR
       IF (HR_ISTATE(N) == 0 .AND. HR_TPRE(N) <= T_BEGIN + 1.E-10_EB) CALL EVP_PLACE_SOURCE(N, T_BEGIN)
    ENDDO

   EVP_READY = .TRUE.

END SUBROUTINE EVAC_PLUS_INITIALIZE


SUBROUTINE EVP_BUILD_ROUTES
   INTEGER :: IT, S, J, T, N
   REAL(EB) :: BEST, CAND
   LOGICAL :: CHANGED

   DO N = 1, EVP_NEX
      DO IT = 1, EVP_NSGTOT
         CHANGED = .FALSE.
         DO S = 1, EVP_NSG
            BEST = EVP_DEXIT(S,N)
            CAND = HUGE(1._EB)
            DO J = 1, 2
               T = SG_NXT(S,J)
               IF (T == 0) CYCLE
               IF (EVP_DEXIT(T,N) < HUGE(1._EB)) THEN
                  IF (SG_LEN(S) + EVP_DEXIT(T,N) < CAND) THEN
                     CAND = SG_LEN(S) + EVP_DEXIT(T,N)
                  ENDIF
               ENDIF
            ENDDO
            IF (CAND < BEST) THEN
               EVP_DEXIT(S,N) = CAND
               CHANGED = .TRUE.
               ! следующая вершина — аргмин
               DO J = 1, 2
                  T = SG_NXT(S,J)
                  IF (T == 0) CYCLE
                  IF (EVP_DEXIT(T,N) < HUGE(1._EB)) THEN
                     IF (ABS(SG_LEN(S) + EVP_DEXIT(T,N) - CAND) < 1.E-9_EB) EVP_NH(S,N) = T
                  ENDIF
               ENDDO
            ENDIF
         ENDDO
         IF (.NOT. CHANGED) EXIT
      ENDDO
   ENDDO
END SUBROUTINE EVP_BUILD_ROUTES


! Обход вперёд от сегмента S по ссылкам NEXT
SUBROUTINE EVP_BFS(S, VIS, BFSQ)
   INTEGER, INTENT(IN) :: S
   INTEGER, INTENT(INOUT) :: VIS(:), BFSQ(:)
   INTEGER :: IH, IT, J, T

   VIS = 0
   IH = 1; IT = 1
   VIS(S) = 1; BFSQ(1) = S
   DO WHILE (IH <= IT)
      T = BFSQ(IH)
      IH = IH + 1
      DO J = 1, 2
         IF (SG_NXT(T,J) == 0) CYCLE
         IF (VIS(SG_NXT(T,J)) == 0) THEN
            IT = IT + 1
            BFSQ(IT) = SG_NXT(T,J)
            VIS(SG_NXT(T,J)) = 1
         ENDIF
      ENDDO
   ENDDO
END SUBROUTINE EVP_BFS


! Сегмент размещения источника: первый сегмент, звенья которого покрыты прямоугольником
SUBROUTINE EVP_SOURCE_SEGMENT(N, S)
   INTEGER, INTENT(IN) :: N
   INTEGER, INTENT(OUT) :: S
   INTEGER :: K
   REAL(EB) :: T0, T1

   S = 0
   DO K = 1, EVP_NLK
      CALL EVP_CLIP_LINK(K, HR_X1(N), HR_X2(N), HR_Y1(N), HR_Y2(N), T0, T1)
      IF (T1 > T0) THEN
         S = LK_SEG(K)
         RETURN
      ENDIF
   ENDDO
END SUBROUTINE EVP_SOURCE_SEGMENT


! Суммарная длина покрытия прямоугольника звеньями
SUBROUTINE EVP_SOURCE_COVERAGE(N, LCOV)
   INTEGER, INTENT(IN) :: N
   REAL(EB), INTENT(OUT) :: LCOV
   INTEGER :: K
   REAL(EB) :: T0, T1

   LCOV = 0._EB
   DO K = 1, EVP_NLK
      CALL EVP_CLIP_LINK(K, HR_X1(N), HR_X2(N), HR_Y1(N), HR_Y2(N), T0, T1)
      IF (T1 > T0) LCOV = LCOV + (T1-T0)*LK_LEN(K)
   ENDDO
END SUBROUTINE EVP_SOURCE_COVERAGE


! Отсечение параметрического интервала звена прямоугольником (алгоритм Лянга-Барски)
SUBROUTINE EVP_CLIP_LINK(K, RX1, RX2, RY1, RY2, T0, T1)
   INTEGER, INTENT(IN) :: K
   REAL(EB), INTENT(IN) :: RX1, RX2, RY1, RY2
   REAL(EB), INTENT(OUT) :: T0, T1
   REAL(EB) :: P, Q, DX, DY

   T0 = 0._EB; T1 = 1._EB
   DX = LK_UX(K)*LK_LEN(K)
   DY = LK_UY(K)*LK_LEN(K)

   P = -DX; Q = -(RX1 - LK_X0(K))
   IF (EVP_CLIP_EDGE(P, Q, T0, T1)) THEN; T0 = 1._EB; T1 = 0._EB; RETURN; ENDIF
   P = DX; Q = RX2 - LK_X0(K)
   IF (EVP_CLIP_EDGE(P, Q, T0, T1)) THEN; T0 = 1._EB; T1 = 0._EB; RETURN; ENDIF
   P = -DY; Q = -(RY1 - LK_Y0(K))
   IF (EVP_CLIP_EDGE(P, Q, T0, T1)) THEN; T0 = 1._EB; T1 = 0._EB; RETURN; ENDIF
   P = DY; Q = RY2 - LK_Y0(K)
   IF (EVP_CLIP_EDGE(P, Q, T0, T1)) THEN; T0 = 1._EB; T1 = 0._EB; RETURN; ENDIF

   T0 = MAX(T0, 0._EB)
   T1 = MIN(T1, 1._EB)
   IF (T0 > T1) THEN
      T0 = 1._EB; T1 = 0._EB
   ENDIF
END SUBROUTINE EVP_CLIP_LINK


! Вспомогательная проверка ребра отсечения; TRUE — интервал пуст
LOGICAL FUNCTION EVP_CLIP_EDGE(P, Q, T0, T1)
   REAL(EB), INTENT(IN) :: P, Q
   REAL(EB), INTENT(INOUT) :: T0, T1
   REAL(EB) :: R

   EVP_CLIP_EDGE = .FALSE.
   IF (ABS(P) < 1.E-12_EB) THEN
      IF (Q < 0._EB) EVP_CLIP_EDGE = .TRUE.
      RETURN
   ENDIF
   R = Q/P
   IF (P < 0._EB) THEN
      T0 = MAX(T0, R)
   ELSE
      T1 = MIN(T1, R)
   ENDIF
END FUNCTION EVP_CLIP_EDGE


! Размещение источника: равномерно вдоль покрытия прямоугольника
SUBROUTINE EVP_PLACE_SOURCE(N, T)
    INTEGER, INTENT(IN) :: N
    REAL(EB), INTENT(IN) :: T
    INTEGER :: K, IEX(1), IPL
    REAL(EB) :: LCOV, DSPACE, ALOC, BLOC, SPAN, ACC, TG, SLOC, RR, WLAT

    CALL EVP_SOURCE_COVERAGE(N, LCOV)
    IF (LCOV < 0.01_EB) THEN
       WRITE(MESSAGE,'(A,A,A)') 'ERROR: EVHR ',TRIM(HR_ID(N)),': rectangle covers no segment links'
       CALL SHUTDOWN(MESSAGE)
    ENDIF
    DSPACE = LCOV/REAL(HR_N(N),EB)
    IPL = 1
    ACC = 0._EB
    DO K = 1, EVP_NLK
       CALL EVP_CLIP_LINK(K, HR_X1(N), HR_X2(N), HR_Y1(N), HR_Y2(N), ALOC, BLOC)
       IF (BLOC <= ALOC) CYCLE
       ALOC = ALOC*LK_LEN(K); BLOC = BLOC*LK_LEN(K)
       SPAN = BLOC - ALOC
       DO WHILE (IPL <= HR_N(N))
          TG = (REAL(IPL,EB)-0.5_EB)*DSPACE - ACC
          IF (TG >= SPAN - 1.E-9_EB) EXIT
          SLOC = ALOC + MAX(TG, 0._EB)
          IF (HR_IX(N) > 0) THEN
             IEX(1) = HR_IX(N)
          ELSE
             IEX = MINLOC(EVP_DEXIT(LK_SEG(K),1:EVP_NEX))
          ENDIF
          WLAT = MAX(0._EB, LK_B(K)*0.5_EB - 0.2_EB)
          CALL RANDOM_NUMBER(RR)
          CALL EVP_ADD_AGENT(K, SLOC, (2._EB*RR-1._EB)*WLAT, HR_IC(N), IEX(1), T)
          IPL = IPL + 1
       ENDDO
       ACC = ACC + SPAN
       IF (IPL > HR_N(N)) EXIT
    ENDDO
    HR_ISTATE(N) = 1
 END SUBROUTINE EVP_PLACE_SOURCE


! Добавление агента в свободный слот
SUBROUTINE EVP_ADD_AGENT(K, SLOC, LAT, IC, IEX, T)
    INTEGER, INTENT(IN) :: K, IC, IEX
    REAL(EB), INTENT(IN) :: SLOC, LAT, T
    INTEGER :: ISLOT
    REAL(EB) :: RPH

    IF (EVP_FREE == 0) CALL SHUTDOWN('ERROR: EVAC+ agent capacity exceeded')
    ISLOT = EVP_FREE
    EVP_FREE = AG_QNEXT(ISLOT)
    AG_LINK(ISLOT) = K
    AG_S(ISLOT) = MIN(SLOC, LK_LEN(K))
    AG_LAT(ISLOT) = LAT
    AG_CLASS(ISLOT) = IC
    AG_XIT(ISLOT) = IEX
    AG_STATE(ISLOT) = 0
    AG_V(ISLOT) = 0._EB
    AG_TOUT(ISLOT) = -1._EB
    AG_TW(ISLOT) = 0._EB
    AG_DIS(ISLOT) = 0
    AG_QNEXT(ISLOT) = 0
    SG_NPLACE(LK_SEG(K)) = SG_NPLACE(LK_SEG(K)) + 1
    EVP_CLSGEN(IC) = EVP_CLSGEN(IC) + 1
    IF (EVP_MODE == 1) THEN
       ! Блуждающий шаг: случайная фаза первого обновления; до него агент
       ! движется со скоростью свободного движения
       CALL RANDOM_NUMBER(RPH)
       AG_TNEXT(ISLOT) = T + RPH*EVP_DT
       AG_V(ISLOT) = EVP_SPEED(IC, LK_KIND(K), 0._EB, LK_B(K))
    ELSE
       AG_TNEXT(ISLOT) = T
    ENDIF
    EVP_ILABEL = EVP_ILABEL + 1
    AG_ID(ISLOT) = EVP_ILABEL
    EVP_NALIVE = EVP_NALIVE + 1
 END SUBROUTINE EVP_ADD_AGENT


! ---------------------------------------------------------------------------
! Шаг движка (постоянный либо блуждающий режим); вызывается из main.f90.
! Этап 6: обёртка пакетного режима — по завершении прогона (все источники
! размещены, живых агентов нет) фиксируется время последнего выхода,
! выполняются оставшиеся прогоны серии внутри одного вызова (сброс состояния,
! зерно SEED+(№-1), локальные часы от T_BEGIN), затем сводка и файл
! распределения. Ядро шага — EVP_STEP_CORE.
! ---------------------------------------------------------------------------

SUBROUTINE EVAC_PLUS_STEP(T)
   REAL(EB), INTENT(IN) :: T
   REAL(EB) :: TAU
   INTEGER :: N, R
   LOGICAL :: LPEND

   IF (.NOT. EVP_READY) RETURN
   CALL EVP_STEP_CORE(T)
   IF (EVP_BATCHDONE) RETURN
   IF (EVP_NALIVE > 0) RETURN
   ! Прогон завершён, только если все источники размещены и был хотя бы
   ! один выход (иначе — ожидание источников с T_PRE в будущем)
   LPEND = .FALSE.
   DO N = 1, EVP_NHR
      IF (HR_ISTATE(N) == 0) LPEND = .TRUE.
   ENDDO
   IF (LPEND .OR. EVP_TLAST < 0._EB) RETURN
   EVP_TSAMP(EVP_BATCHRUN) = EVP_TLAST
   EVP_RUNSDONE = EVP_RUNSDONE + 1
   IF (EVP_RUNS <= 1) THEN
      EVP_BATCHDONE = .TRUE.
      RETURN
   ENDIF
   ! Финальный блок расчётного протокола прогона 1 — до сброса состояния
   IF (EVP_PROTDT > 0._EB .AND. LU_EVPROT > 0 .AND. .NOT. EVP_PROTFIN) THEN
      CALL EVP_REFRESH_LINK_COUNTS
      CALL EVP_PROTOCOL_WRITE(MAX(EVP_TSTEP, 0._EB))
      EVP_PROTFIN = .TRUE.
   ENDIF
   ! Внутренние прогоны 2..EVP_RUNS: вывод по шагам отключён (EVP_OUTRUN)
   DO R = EVP_BATCHRUN+1, EVP_RUNS
      EVP_BATCHRUN = R
      CALL EVP_RESET_FOR_RUN(EVP_SEED + R - 1)
      TAU = T_BEGIN
      DO N = 1, EVP_NHR
         IF (HR_TPRE(N) <= T_BEGIN + 1.E-10_EB) CALL EVP_PLACE_SOURCE(N, T_BEGIN)
      ENDDO
      DO
         LPEND = .FALSE.
         DO N = 1, EVP_NHR
            IF (HR_ISTATE(N) == 0) LPEND = .TRUE.
         ENDDO
         IF (EVP_NALIVE == 0 .AND. .NOT. LPEND) EXIT
         TAU = TAU + EVP_DT
         IF (TAU - T_BEGIN > EVP_BATCH_TMAX) &
            CALL SHUTDOWN('ERROR: EVAC+ batch run did not finish within 86400 s (routing failure?)')
         CALL EVP_STEP_CORE(TAU)
      ENDDO
      EVP_TSAMP(R) = EVP_TLAST
      EVP_RUNSDONE = EVP_RUNSDONE + 1
   ENDDO
   EVP_BATCHDONE = .TRUE.
   CALL EVP_BATCH_FINALIZE
END SUBROUTINE EVAC_PLUS_STEP


! Ядро шага: фазы 0-7 в фиксированном порядке (решение 24)
SUBROUTINE EVP_STEP_CORE(T)
   REAL(EB), INTENT(IN) :: T
   INTEGER :: I, K, NSG, N, ITER, IC, JP, J, JJ, IP, KT, CH, CLEN, I0, I1, OFF
   INTEGER(8) :: CLK0, CLK1, CLKS
   LOGICAL :: HANDOFF
   REAL(EB) :: S, V, OVER, DD, DBAL, DKIN, EPSD, RXI, VC

   IF (.NOT. EVP_READY) RETURN
   IF (EVP_OUTRUN) EVP_TSTEP = T
   IF (EVP_CLKRATE == 0) CALL SYSTEM_CLOCK(COUNT_RATE=EVP_CLKRATE)
   CALL SYSTEM_CLOCK(COUNT=CLKS)
   CLK0 = CLKS

   ! Размещение источников по времени начала движения
   DO N = 1, EVP_NHR
      IF (HR_ISTATE(N) == 0 .AND. T >= HR_TPRE(N) - 1.E-10_EB) CALL EVP_PLACE_SOURCE(N, T)
   ENDDO
   IF (EVP_NALIVE == 0) RETURN

    ! Снимок позиций на начало шага (синхронность, разд. 8 модели)
    AG_SOLD(1:EVP_NAG) = AG_S(1:EVP_NAG)
    CLEN = (EVP_NAG + EVP_NCHUNK - 1)/EVP_NCHUNK

    ! Фаза 0+1а: ожидание в очередях (t_з (15)) и целочисленные счётчики
    ! звеньев — один последовательный проход. Счётчики LK_CNT/основы LK_FA
    ! учитывают всех живых агентов (включая очередь — баланс звена); LK_MOV
    ! — только движущихся (состояние 0): плотность (8) по П7.2 определяется
    ! по вперёд идущим, стоящие в очереди не сгущают «хвост» до клампа 0.95
    ! (фиксация отладки R29d)
    LK_CNT = 0
    LK_MOV = 0
    LK_MC = 0
    EVP_XCNT = 0
    DO I = 1, EVP_NAG
       IF (AG_STATE(I) == 2) CYCLE
       K = AG_LINK(I)
       IC = AG_CLASS(I)
       LK_CNT(K) = LK_CNT(K) + 1
       LK_MC(K,IC) = LK_MC(K,IC) + 1
       IF (AG_STATE(I) == 1) THEN
          AG_TW(I) = AG_TW(I) + EVP_DT
       ELSE
          LK_MOV(K) = LK_MOV(K) + 1
       ENDIF
    ENDDO

    ! Фаза 1б+2: LK_FA — класс-группированные суммы в фиксированном порядке
    ! классов (детерминизм при любом числе потоков); плотности звеньев;
    ! смещения срезов CSR-списков
    OFF = 0
    DO K = 1, EVP_NLK
       LK_FA(K) = 0._EB
       DO IC = 1, EVP_NCLASS_MAX
          IF (LK_MC(K,IC) > 0) LK_FA(K) = LK_FA(K) + REAL(LK_MC(K,IC),EB)*CLS(IC)%F
       ENDDO
       IF (LK_LEN(K) > 0._EB) THEN
          LK_D(K) = REAL(LK_CNT(K),EB)/(LK_B(K)*LK_LEN(K))
       ELSE
          LK_D(K) = 0._EB
       ENDIF
       LK_OFF(K) = OFF
       OFF = OFF + LK_MOV(K)
       LK_CUR(K) = LK_OFF(K)
    ENDDO

    ! Фаза 1в+1г: размещение движущихся агентов по звеньям и сортировка
    ! срезов по AG_S (равные AG_S — по убыванию слота) с обратным индексом
    ! AG_POS — детерминированный итоговый порядок; поиск NLOCAL ближайших
    ! вперёд идущих завершается досрочно (R31)
    DO I = 1, EVP_NAG
       IF (AG_STATE(I) /= 0) CYCLE
       K = AG_LINK(I)
       LK_CUR(K) = LK_CUR(K) + 1
       AG_ORDER(LK_CUR(K)) = I
    ENDDO
    DO K = 1, EVP_NLK
       DO J = LK_OFF(K)+2, LK_OFF(K)+LK_MOV(K)
          JJ = AG_ORDER(J)
          IP = J - 1
          DO WHILE (IP >= LK_OFF(K)+1)
             KT = AG_ORDER(IP)
             IF (AG_S(KT) < AG_S(JJ)) EXIT
             IF (AG_S(KT) == AG_S(JJ) .AND. KT > JJ) EXIT
             AG_ORDER(IP+1) = KT
             IP = IP - 1
          ENDDO
          AG_ORDER(IP+1) = JJ
       ENDDO
       DO J = LK_OFF(K)+1, LK_OFF(K)+LK_MOV(K)
          AG_POS(AG_ORDER(J)) = J - LK_OFF(K)
       ENDDO
    ENDDO
    CALL SYSTEM_CLOCK(COUNT=CLK1)
    EVP_TPH(1) = EVP_TPH(1) + REAL(CLK1-CLK0,EB)/REAL(EVP_CLKRATE,EB)
    CLK0 = CLK1

    ! Фаза 3а: скорость и перемещение — единственная параллельная область
    ! шага (R31: стоимость барьеров области на каждом из тысяч шагов
    ! превышает работу счётных фаз; распараллеливается только проход
    ! наибольшей работы). Обход — по фиксированным отрезкам слотов: строки
    ! EVP_WSV/EVP_WSFV заполняются своим отрезком, свёртка фазы 6 в
    ! фиксированном порядке отрезков даёт результат, не зависящий от числа
    ! потоков, и устраняет полный проход по агентам в фазе 6; в блуждающем
    ! режиме область неактивна (один поток, последовательный вызов
    ! RANDOM_NUMBER). Плотности читаются из снимка позиций AG_SOLD на
    ! начало шага — синхронное обновление состояния всех агентов (разд. 8
    ! мат. модели); достигшие границы помечаются AG_PEND и ставятся в
    ! очереди фазой 3б. Скорость — от локальной плотности (8) при
    ! NLOCAL>0, иначе от счётной плотности звена; при активной
    ! ресинхронизации (разд. 9 модели) — от балансной плотности звена.
    ! Режим разуплотнения (R29d, разд. 7 модели): агент, выходящий из
    ! скопления или очереди, ограничен V <= V_qmax = a*V0 вида пути
    ! текущего участка до плотности ниже D0 и свободного пути впереди.
    ! Поиск ближайших вперёд идущих — по упорядоченному срезу звена с
    ! ранним завершением (R31)
!$OMP PARALLEL IF (EVP_MODE == 0) &
!$OMP& PRIVATE(I,K,IC,KT,CH,I0,I1,S,V,DD,VC,RXI,OVER,NSG)
!$OMP DO SCHEDULE(STATIC)
    DO CH = 1, EVP_NCHUNK
      I0 = (CH-1)*CLEN + 1
      I1 = MIN(CH*CLEN, EVP_NAG)
      EVP_WSV(CH,1:EVP_NLK) = 0._EB
      EVP_WSFV(CH,1:EVP_NLK) = 0._EB
      DO I = I0, I1
         IF (AG_STATE(I) /= 0) CYCLE
         K = AG_LINK(I)
         DD = EVP_DENSITY_OF(I, K)
         IF (EVP_MODE == 1) THEN
            IF (T >= AG_TNEXT(I) - 0.5_EB*EVP_DT) THEN
               AG_V(I) = EVP_SPEED(AG_CLASS(I), LK_KIND(K), DD, LK_B(K), DISP=.TRUE.)
               CALL RANDOM_NUMBER(RXI)
               ! кумулятивное расписание: разобщение моментов обновления без
               ! систематического смещения среднего интервала
               AG_TNEXT(I) = MAX(AG_TNEXT(I) + EVP_DT*(1._EB + (2._EB*RXI-1._EB)*EVP_WANDER_XI), T)
            ENDIF
            V = AG_V(I)
         ELSE
            V = EVP_SPEED(AG_CLASS(I), LK_KIND(K), DD, LK_B(K))
            AG_V(I) = V
         ENDIF
         IF (AG_DIS(I) == 1) THEN
            IF (DD < CLS(AG_CLASS(I))%D0(LK_KIND(K)) .AND. EVP_AHEAD_GAP(I, K) > EVP_RELAX) THEN
               AG_DIS(I) = 0
            ELSE
               VC = CLS(AG_CLASS(I))%AV(LK_KIND(K))*CLS(AG_CLASS(I))%V0(LK_KIND(K))/60._EB
               IF (V > VC) THEN
                  V = VC
                  AG_V(I) = VC
               ENDIF
            ENDIF
         ENDIF
         ! клеймо скопления: балансная плотность звена выше D_max (реальный
         ! затор, а не подход к короткой очереди — локальная плотность через
         ! очередь агрегатов переноса завышается и непригодна как триггер)
         IF (LK_LEN(K) > 0._EB) THEN
            IF (LK_FA(K)/(LK_B(K)*LK_LEN(K)) > EVP_DMAX_M2) AG_DIS(I) = 1
         ENDIF
         S = AG_SOLD(I) + V*EVP_DT
        DO
           IF (S < LK_LEN(K)) EXIT
            IF (LK_LAST(K)) THEN
               ! достиг границы участка: отложенная постановка в очередь (фаза 3б)
               OVER = S - LK_LEN(K)
               NSG = EVP_NH(LK_SEG(K), AG_XIT(I))
               IF (NSG <= 0) CALL SHUTDOWN('ERROR: EVAC+ routing failure at segment end')
               AG_PEND(I) = 1
               AG_POVER(I) = OVER
               AG_PNSG(I) = NSG
               EXIT
            ENDIF
           S = S - LK_LEN(K)
           K = LK_NEXT(K)
        ENDDO
        IF (AG_PEND(I) == 0) THEN
           AG_LINK(I) = K
           AG_S(I) = S
        ENDIF
        ! вклад агента в суммы фазы 6 (финальное звено; ожидающему
        ! постановки AG_PEND звено не менялось)
        KT = K
        IF (AG_PEND(I) == 1) KT = AG_LINK(I)
        EVP_WSV(CH,KT) = EVP_WSV(CH,KT) + AG_V(I)
        EVP_WSFV(CH,KT) = EVP_WSFV(CH,KT) + CLS(AG_CLASS(I))%F*AG_V(I)
      ENDDO
    ENDDO
!$OMP END DO
!$OMP END PARALLEL
    CALL SYSTEM_CLOCK(COUNT=CLK1)
    EVP_TPH(2) = EVP_TPH(2) + REAL(CLK1-CLK0,EB)/REAL(EVP_CLKRATE,EB)
    CLK0 = CLK1

    ! Фаза 3б: постановка достигших границы в очереди — последовательный
    ! проход в порядке слотов; порядок очередей не зависит от числа потоков
    DO I = 1, EVP_NAG
       IF (AG_PEND(I) == 1) THEN
          AG_PEND(I) = 0
          CALL EVP_QUEUE_APPEND(AG_PNSG(I), I, AG_POVER(I), .TRUE.)
       ENDIF
    ENDDO
    CALL SYSTEM_CLOCK(COUNT=CLK1)
    EVP_TPH(3) = EVP_TPH(3) + REAL(CLK1-CLK0,EB)/REAL(EVP_CLKRATE,EB)
    CLK0 = CLK1

    ! Фаза 4: пересчёт счётчиков после перемещений (для контроля места на
    ! границах) — один последовательный проход; LK_FA — класс-группированные
    ! суммы в фиксированном порядке классов (R31)
    LK_CNT = 0
    LK_MC = 0
    DO I = 1, EVP_NAG
       IF (AG_STATE(I) == 2) CYCLE
       K = AG_LINK(I)
       IC = AG_CLASS(I)
       LK_CNT(K) = LK_CNT(K) + 1
       LK_MC(K,IC) = LK_MC(K,IC) + 1
    ENDDO
    DO K = 1, EVP_NLK
       LK_FA(K) = 0._EB
       DO IC = 1, EVP_NCLASS_MAX
          IF (LK_MC(K,IC) > 0) LK_FA(K) = LK_FA(K) + REAL(LK_MC(K,IC),EB)*CLS(IC)%F
       ENDDO
    ENDDO
    CALL SYSTEM_CLOCK(COUNT=CLK1)
    EVP_TPH(4) = EVP_TPH(4) + REAL(CLK1-CLK0,EB)/REAL(EVP_CLKRATE,EB)
    CLK0 = CLK1

    ! Фаза 5: границы — пропуск из очередей в пределах лимитов (формула (6) модели)
    DO ITER = 1, EVP_NSGTOT
       HANDOFF = .FALSE.
       CALL EVP_BOUNDARY_SWEEP(T, HANDOFF, ITER)
       IF (.NOT. HANDOFF) EXIT
    ENDDO
    CALL SYSTEM_CLOCK(COUNT=CLK1)
    EVP_TPH(5) = EVP_TPH(5) + REAL(CLK1-CLK0,EB)/REAL(EVP_CLKRATE,EB)
    CLK0 = CLK1

    ! Фаза 6: контроль кумулятивной ошибки плотностей (разд. 9 модели).
    ! Dбал — по суммарной площади проекций; Dкин — по фактическим скоростям.
    ! Ресинхронизация применяется только в блуждающем режиме (имитационно-
    ! плотностная спецификация П8); в постоянном режиме диагностика только
    ! накапливается, численная схема неизменна (условие воспроизводимости
    ! В1). Число живых на звене — LK_CNT минус ушедшие через выходы в этом
    ! шаге (EVP_XCNT); суммы скоростей — свёртка строк отрезков фазы 3а
    ! в фиксированном порядке отрезков
    DO K = 1, EVP_NLK
       LK_SV(K) = 0._EB
       LK_SFV(K) = 0._EB
       DO CH = 1, EVP_NCHUNK
          LK_SV(K) = LK_SV(K) + EVP_WSV(CH,K)
          LK_SFV(K) = LK_SFV(K) + EVP_WSFV(CH,K)
       ENDDO
    ENDDO
    DO K = 1, EVP_NLK
       IF (LK_LEN(K) <= 0._EB .OR. LK_CNT(K) - EVP_XCNT(K) < 1) THEN
          LK_SYNC(K) = .FALSE.
          CYCLE
       ENDIF
       DBAL = LK_FA(K)/(LK_B(K)*LK_LEN(K))
       IF (DBAL < 1.E-12_EB .OR. LK_SV(K) <= 1.E-12_EB) THEN
          LK_SYNC(K) = .FALSE.
          CYCLE
       ENDIF
       DKIN = LK_SFV(K)*REAL(LK_CNT(K) - EVP_XCNT(K),EB)/(LK_B(K)*LK_LEN(K)*LK_SV(K))
       EPSD = ABS(DBAL-DKIN)/DBAL
       EVP_ESUM = EVP_ESUM + EPSD
       EVP_ECNT = EVP_ECNT + 1
       IF (EPSD > LK_DERR(K)) LK_DERR(K) = EPSD
        IF (EPSD > EVP_EPS_BAL .AND. EVP_MODE == 1) THEN
           LK_SYNC(K) = .TRUE.
           EVP_NRESYNC = EVP_NRESYNC + 1
        ELSE
           LK_SYNC(K) = .FALSE.
        ENDIF
      ENDDO

       ! Расчётный протокол движка на контрольные моменты времени (этап 3);
       ! строки подписываются фактическим моментом T. В пакетном режиме
       ! протокол ведётся только для прогона 1 (EVP_OUTRUN)
       IF (EVP_OUTRUN .AND. EVP_PROTDT > 0._EB) THEN
          IF (T >= EVP_TPROT - 1.E-10_EB) THEN
             CALL EVP_PROTOCOL_WRITE(T)
             EVP_TPROT = (FLOOR(T/EVP_PROTDT)+1._EB)*EVP_PROTDT
          ENDIF
       ENDIF
      CALL SYSTEM_CLOCK(COUNT=CLK1)
      EVP_TPH(6) = EVP_TPH(6) + REAL(CLK1-CLK0,EB)/REAL(EVP_CLKRATE,EB)
      CLK0 = CLK1

     ! Стеновое время шага (этап 4): метрика масштабирования OpenMP;
     ! в пакетном режиме накапливается по всем прогонам серии
     EVP_TWALL = EVP_TWALL + REAL(CLK0-CLKS,EB)/REAL(EVP_CLKRATE,EB)
     EVP_NSTEPS = EVP_NSTEPS + 1

  END SUBROUTINE EVP_STEP_CORE


! Сброс состояния между прогонами пакета (этап 6, решение 15): возврат
! счётчиков звеньев/границ/участков, очередей, агентов и источников к
! состоянию после инициализации; новые выделения памяти не выполняются.
! Зерно генератора — SEED+(номер прогона-1)
SUBROUTINE EVP_RESET_FOR_RUN(SEEDRUN)
   INTEGER, INTENT(IN) :: SEEDRUN
   INTEGER :: K, NSD
   INTEGER, ALLOCATABLE :: SEED_ARR(:)

   CALL RANDOM_SEED(SIZE=NSD)
   ALLOCATE(SEED_ARR(NSD))
   SEED_ARR = SEEDRUN + (/ (K-1, K=1,NSD) /)
   CALL RANDOM_SEED(PUT=SEED_ARR)
   DEALLOCATE(SEED_ARR)

   LK_CNT = 0; LK_D = 0._EB; LK_MOV = 0; LK_MC = 0; EVP_XCNT = 0
   LK_FA = 0._EB; LK_DERR = 0._EB; LK_SYNC = .FALSE.
   BND_QHEAD = 0; BND_QTAIL = 0; BND_QLEN = 0
   BND_LEFT = .FALSE.; BND_CROWD = .FALSE.
   BND_RESID = 0._EB; BND_CR = 0._EB
   BND_NPASS = 0; BND_QMAX = 0; BND_NEP = 0
   BND_TWSUM = 0._EB; BND_TMAXW = 0._EB; BND_ACROWD = 0._EB
   BND_TSK_SIM = 0._EB; BND_TSK_EST = 0._EB; BND_TCR0 = 0._EB; BND_QD = 0._EB
   SG_NIN = 0; SG_NOUT = 0; SG_NPLACE = 0; SG_NEXIT = 0; SG_NGONE = 0
   EVP_CLSGEN = 0; EVP_CLSEVAC = 0; EVP_CLSTLAST = -1._EB
   EX_CNT = 0; EX_TLAST = -1._EB
   EVP_NALIVE = 0; EVP_NEVAC = 0; EVP_ILABEL = 0; EVP_TLAST = -1._EB
   EVP_NRESYNC = 0; EVP_ESUM = 0._EB; EVP_ECNT = 0
   AG_STATE = 2; AG_PEND = 0; AG_DIS = 0
   EVP_FREE = 0
   DO K = EVP_NAGMAX, 1, -1
      AG_QNEXT(K) = EVP_FREE
      EVP_FREE = K
   ENDDO
   IF (EVP_NHR > 0) HR_ISTATE = 0
   EVP_OUTRUN = .FALSE.
END SUBROUTINE EVP_RESET_FOR_RUN


! Сводка пакета (этап 6): сортировка выборки, квантиль — k-я порядковая
! статистика, k = ceil(P*ND) (стандартная эмпирическая обратная функции
! распределения; допуск против ошибок представления P), статистика выборки.
! Файл CHID_evac_dist.csv: полная выборка (первичные данные экспертизы и
! будущего модуля аналитического вывода) + блок Summary. Протокольное число
! — только квантиль; медиана и P=0.95 вычисляются справочно для evac.out
SUBROUTINE EVP_BATCH_FINALIZE
   INTEGER :: N, J, ND, K95
   REAL(EB) :: TV, SS

   ND = MIN(EVP_RUNSDONE, EVP_RUNS)
   IF (ND < 1) RETURN
   EVP_TSORT(1:ND) = EVP_TSAMP(1:ND)
   DO J = 2, ND
      TV = EVP_TSORT(J)
      N = J - 1
      DO WHILE (N >= 1)
         IF (EVP_TSORT(N) <= TV) EXIT
         EVP_TSORT(N+1) = EVP_TSORT(N)
         N = N - 1
      ENDDO
      EVP_TSORT(N+1) = TV
   ENDDO
   EVP_QK = ND - INT((1._EB-EVP_PROB)*REAL(ND,EB) + 1.E-9_EB)
   EVP_QK = MIN(MAX(EVP_QK,1),ND)
   EVP_SQ = EVP_TSORT(EVP_QK)
   EVP_SMIN = EVP_TSORT(1)
   EVP_SMAX = EVP_TSORT(ND)
   EVP_SMEAN = SUM(EVP_TSORT(1:ND))/REAL(ND,EB)
   EVP_SSIG = 0._EB
   IF (ND >= 2) THEN
      SS = 0._EB
      DO N = 1, ND
         SS = SS + (EVP_TSORT(N)-EVP_SMEAN)**2
      ENDDO
      EVP_SSIG = SQRT(SS/REAL(ND-1,EB))
   ENDIF
   IF (MOD(ND,2) == 1) THEN
      EVP_SMED = EVP_TSORT((ND+1)/2)
   ELSE
      EVP_SMED = 0.5_EB*(EVP_TSORT(ND/2)+EVP_TSORT(ND/2+1))
   ENDIF
   K95 = ND - INT(0.05_EB*REAL(ND,EB) + 1.E-9_EB)
   K95 = MIN(MAX(K95,1),ND)
   EVP_SQ95 = EVP_TSORT(K95)

   LU_EVPDIST = GET_FILE_NUMBER()
   OPEN(LU_EVPDIST, FILE=TRIM(CHID)//'_evac_dist.csv', FORM='FORMATTED', STATUS='REPLACE')
   WRITE(LU_EVPDIST,'(A)') 'Run,Seed,T_evac_s'
   DO N = 1, ND
      WRITE(LU_EVPDIST,'(I0,A,I0,A,F10.2)') N,',',EVP_SEED+N-1,',',EVP_TSAMP(N)
   ENDDO
   WRITE(LU_EVPDIST,'(A)') ' '
   WRITE(LU_EVPDIST,'(A,I0)')    'Summary,N_runs,',ND
   WRITE(LU_EVPDIST,'(A,F8.4)')  'Summary,P,',EVP_PROB
   WRITE(LU_EVPDIST,'(A,I0)')    'Summary,k_index,',EVP_QK
   WRITE(LU_EVPDIST,'(A,F10.2)') 'Summary,T_quantile_s,',EVP_SQ
   WRITE(LU_EVPDIST,'(A,F10.2)') 'Summary,T_min_s,',EVP_SMIN
   WRITE(LU_EVPDIST,'(A,F10.2)') 'Summary,T_max_s,',EVP_SMAX
   WRITE(LU_EVPDIST,'(A,F10.2)') 'Summary,T_mean_s,',EVP_SMEAN
   IF (ND >= 2) WRITE(LU_EVPDIST,'(A,F10.2)') 'Summary,T_sigma_s,',EVP_SSIG
   IF (EVP_MODE == 1) THEN
      WRITE(LU_EVPDIST,'(A)') 'Summary,Mode,WANDER'
   ELSE
      WRITE(LU_EVPDIST,'(A)') 'Summary,Mode,FIXED'
   ENDIF
   WRITE(LU_EVPDIST,'(A,I0)')    'Summary,Seed_base,',EVP_SEED
   CLOSE(LU_EVPDIST)
   LU_EVPDIST = 0
   EVP_DISTWRIT = .TRUE.
END SUBROUTINE EVP_BATCH_FINALIZE


! Плотность для скорости агента I, чел./м2 (своего класса): локальная (8),
! счётная звена либо балансная при активной ресинхронизации (разд. 9 модели)
FUNCTION EVP_DENSITY_OF(I, K) RESULT(DD)
   INTEGER, INTENT(IN) :: I, K
   REAL(EB) :: DD

   IF (LK_SYNC(K)) THEN
      DD = (LK_FA(K)/(LK_B(K)*LK_LEN(K)))/CLS(AG_CLASS(I))%F
   ELSEIF (EVP_NLOCAL > 0) THEN
      DD = EVP_LOCAL_D(I, K)/CLS(AG_CLASS(I))%F
   ELSE
      DD = LK_D(K)
   ENDIF
END FUNCTION EVP_DENSITY_OF


! Расстояние вдоль пути от агента I до ближайшего впереди идущего, м (R29d:
! условие снятия ограничения разуплотнения); при отсутствии — HUGE.
! Позиции — из снимка на начало шага (AG_SOLD, синхронность разд. 8 модели).
! R31: список звена упорядочен по AG_S — первый подходящий впереди идущий
! и есть ближайший, полный обход цепочек не требуется
REAL(EB) FUNCTION EVP_AHEAD_GAP(I, K0)
    INTEGER, INTENT(IN) :: I, K0
    INTEGER :: K, J, JJ
    REAL(EB) :: BASE, DTMP

    EVP_AHEAD_GAP = HUGE(1._EB)
    BASE = -AG_SOLD(I)
    K = K0
    J = LK_OFF(K) + AG_POS(I) + 1
    DO WHILE (K /= 0 .AND. EVP_AHEAD_GAP >= HUGE(1._EB))
       DO WHILE (J <= LK_OFF(K) + LK_MOV(K))
          JJ = AG_ORDER(J)
          DTMP = BASE + AG_SOLD(JJ)
          IF (DTMP > 1.E-6_EB) THEN
             EVP_AHEAD_GAP = DTMP
             EXIT
          ENDIF
          J = J + 1
       ENDDO
       IF (EVP_AHEAD_GAP < HUGE(1._EB)) EXIT
       IF (LK_LAST(K)) EXIT
       BASE = BASE + LK_LEN(K)
       K = LK_NEXT(K)
       IF (K == 0) EXIT
       J = LK_OFF(K) + 1
    ENDDO
END FUNCTION EVP_AHEAD_GAP


! Локальная плотность (8): (n-1)*f/(b*dL) по n ближайшим вперёд идущим агентам
! в пределах текущего сегмента; для неоднородных потоков суммируются площади
! проекции f_j конкретных агентов группы; возврат в м2/м2 (0 — впереди свободно).
! Позиции — из снимка на начало шага (AG_SOLD, синхронность разд. 8 модели).
! R31: обход упорядоченного по AG_S среза звена от преемника агента I —
! первые n подряд идущих и есть ближайшие; выход при наборе n (раннее
! завершение, эквивалентно усечению отсортированного массива R30)
REAL(EB) FUNCTION EVP_LOCAL_D(I, K0)
    INTEGER, INTENT(IN) :: I, K0
    INTEGER :: K, J, JJ, NA, M
    REAL(EB) :: DTMP, BASE, DARR(32), DN, D1, FI, FSUM
    REAL(EB) :: FARR(32)
    LOGICAL :: FHOM

    NA = 0
    BASE = -AG_SOLD(I)
    K = K0
    J = LK_OFF(K) + AG_POS(I) + 1
    SCAN_LINKS: DO WHILE (K /= 0 .AND. NA < EVP_NLOCAL)
       DO WHILE (J <= LK_OFF(K) + LK_MOV(K))
          IF (NA >= 32) EXIT
          JJ = AG_ORDER(J)
          DTMP = BASE + AG_SOLD(JJ)
          IF (DTMP > 1.E-6_EB) THEN
             NA = NA + 1
             DARR(NA) = DTMP
             FARR(NA) = CLS(AG_CLASS(JJ))%F
          ENDIF
          J = J + 1
       ENDDO
       IF (LK_LAST(K)) EXIT SCAN_LINKS
       BASE = BASE + LK_LEN(K)
       K = LK_NEXT(K)
       IF (K == 0) EXIT SCAN_LINKS
       J = LK_OFF(K) + 1
    ENDDO SCAN_LINKS

   EVP_LOCAL_D = 0._EB
   IF (NA >= 2 .AND. EVP_NLOCAL >= 2) THEN
      IF (NA > EVP_NLOCAL) NA = EVP_NLOCAL
      DN = DARR(NA)
      D1 = DARR(1)
      FI = CLS(AG_CLASS(I))%F
      ! Однородная группа: точное воспроизведение (n-1)*f (воспроизводимость В1)
      FHOM = .TRUE.
      DO M = 1, NA-1
         IF (FARR(M) /= FI) THEN
            FHOM = .FALSE.
            EXIT
         ENDIF
      ENDDO
      IF (FHOM) THEN
         FSUM = REAL(NA-1,EB)*FI
      ELSE
         FSUM = SUM(FARR(1:NA-1))
      ENDIF
      IF (DN - D1 > 1.E-6_EB) THEN
         EVP_LOCAL_D = MIN(0.95_EB, FSUM/(LK_B(K0)*(DN-D1)))
      ELSE
         EVP_LOCAL_D = 0.95_EB
      ENDIF
   ENDIF
END FUNCTION EVP_LOCAL_D


! Один проход по границам; HANDOFF — были ли передачи в следующие очереди;
! ITER — номер прохода в шаге (кредиты П8.8 пополняются один раз за шаг)
SUBROUTINE EVP_BOUNDARY_SWEEP(T, HANDOFF, ITER)
   REAL(EB), INTENT(IN) :: T
   LOGICAL, INTENT(INOUT) :: HANDOFF
   INTEGER, INTENT(IN) :: ITER
   INTEGER :: B, P, NSG2, K1, IK, IK2, IEX, J, JJ, S2, KL, P2, PPRE, PB, PP, JBEST, OLDSG
   REAL(EB) :: Q, Q2, FI, DUP, FUP, CAP, CRBEST, DD2, JSUM
   REAL(EB) :: JARR(EVP_MAXPRE)
   LOGICAL :: CROWD, IONLY, PRESENT(EVP_MAXPRE)

   DO B = 1, EVP_NSGTOT
      P = BND_QHEAD(B)
      IF (P == 0) THEN
         BND_RESID(B) = 0._EB      ! без очереди накопленный остаток не сохраняется
         IF (BND_CROWD(B)) CALL EVP_CROWD_EVENT(B, T, .TRUE.)
         CYCLE
      ENDIF
      IF (SG_KIND(B) == EVP_KIND_EXIT) THEN
         IK = 2                                ! выход — проём (П2.8)
         IONLY = EX_IONLY(B-EVP_NSG)           ! выход только со счётом: без лимита (6)
      ELSE
         IK = SG_KIND(B)
         IONLY = .FALSE.
      ENDIF

      CROWD = .FALSE.
      IF (.NOT. IONLY) THEN
         ! Режим границы (П6.5-П6.6, П8.8): скопление образуется, когда суммарная
         ! транспортная способность питающих потоков D*V*b превышает пропускную
         ! способность границы q_max*c/f; тогда плотность у границы достигает
         ! 0.9 м2/м2 и пропуск ведётся с интенсивностью q_Dmax (П2.8)
         IF (SG_NPRE(B) <= 1) THEN
            S2 = SG_PRE(B,1)
            IF (S2 > 0 .AND. SG_LSTART(S2) == 0 .AND. BND_QLEN(S2) > 0) THEN
               ! Питающий поток пришёл через сквозной проём с очередью:
               ! фактическая подача ограничена разрядом очереди проёма,
               ! а не транспортной способностью звена выше по потоку
               ! (сужение ROOM1->DOOR1->ROOM2 иначе даёт ложные
               ! прерывистые скопления на входе ROOM2)
               IF (SG_KIND(S2) == EVP_KIND_EXIT) THEN
                  IK2 = 2
               ELSE
                  IK2 = SG_KIND(S2)
               ENDIF
               IF (BND_CROWD(S2)) THEN
                  Q2 = EVP_QDMAX_OF(IK2, SG_B(S2))
               ELSE
                  Q2 = EVP_QMAX(IK2)
               ENDIF
               FUP = Q2*SG_B(S2)/(60._EB*CLS(AG_CLASS(P))%F)                           ! чел/с
            ELSE
               K1 = AG_LINK(P)
               DUP = REAL(LK_CNT(K1),EB)/(LK_B(K1)*LK_LEN(K1))
               FUP = DUP*EVP_SPEED(AG_CLASS(P), LK_KIND(K1), DUP, LK_B(K1))*LK_B(K1)  ! чел/с
            ENDIF
         ELSE
            ! слияние: сумма транспортных способностей питающих потоков (П8.8)
            FUP = 0._EB
            DO J = 1, SG_NPRE(B)
               JARR(J) = 0._EB
               PRESENT(J) = .FALSE.
               S2 = SG_PRE(B,J)
               P2 = P
               DO WHILE (P2 /= 0)
                  IF (LK_SEG(AG_LINK(P2)) == S2) THEN
                     PRESENT(J) = .TRUE.
                     EXIT
                  ENDIF
                  P2 = AG_QNEXT(P2)
               ENDDO
               IF (.NOT. PRESENT(J)) CYCLE
               KL = SG_LSTART(S2) + SG_NLINK(S2) - 1
               DD2 = REAL(LK_CNT(KL),EB)/(LK_B(KL)*LK_LEN(KL))
               JARR(J) = DD2*EVP_SPEED(AG_CLASS(P2), LK_KIND(KL), DD2, LK_B(KL))*LK_B(KL)
               FUP = FUP + JARR(J)
            ENDDO
         ENDIF
         CAP = EVP_QMAX(IK)*SG_B(B)/(60._EB*CLS(AG_CLASS(P))%F)                      ! чел/с
         IF (FUP > CAP + 1.E-9_EB) THEN
            Q = EVP_QDMAX_OF(IK, SG_B(B))
            CROWD = .TRUE.
         ELSE
            Q = EVP_QMAX(IK)
         ENDIF
         ! Лимит площади за шаг с переносом дробного остатка (формула (6));
         ! начисляется один раз за шаг (ITER=1): повторные проходы фазы 5 —
         ! каскадные передачи в пределах того же интервала dT
         IF (ITER == 1) BND_RESID(B) = BND_RESID(B) + Q*SG_B(B)*EVP_DT/60._EB
         IF (CROWD .NEQV. BND_CROWD(B)) THEN
            IF (BND_CROWD(B)) CALL EVP_CROWD_EVENT(B, T, .TRUE.)
            IF (CROWD) CALL EVP_CROWD_EVENT(B, T, .FALSE.)
         ENDIF
         ! Кредиты долей участия питающих потоков (П8.8), один раз за шаг
         IF (SG_NPRE(B) > 1 .AND. ITER == 1) THEN
            JSUM = FUP
            IF (JSUM <= 1.E-12_EB) THEN
               JSUM = 1._EB
               JARR = 1._EB
            ENDIF
            DO J = 1, SG_NPRE(B)
               IF (PRESENT(J)) THEN
                  BND_CR(B,J) = BND_CR(B,J) + JARR(J)/JSUM
               ELSE
                  BND_CR(B,J) = 0._EB
               ENDIF
            ENDDO
         ENDIF
      ENDIF

      ! Выпуск из очереди в пределах лимита (6)
      DO
         P = BND_QHEAD(B)
         IF (P == 0) EXIT
         PP = 0
         PB = P
         JBEST = 0
         IF (SG_NPRE(B) > 1) THEN
            ! агент питающего потока с максимальным кредитом (П8.8);
            ! при равенстве кредитов — раньше вставший в очередь
            CRBEST = -1.E30_EB
            P2 = P
            PPRE = 0
            DO WHILE (P2 /= 0)
               S2 = LK_SEG(AG_LINK(P2))
               JJ = 0
               DO J = 1, SG_NPRE(B)
                  IF (SG_PRE(B,J) == S2) THEN
                     JJ = J
                     EXIT
                  ENDIF
               ENDDO
               IF (JJ > 0) THEN
                  IF (BND_CR(B,JJ) > CRBEST) THEN
                     CRBEST = BND_CR(B,JJ)
                     PB = P2
                     PP = PPRE
                     JBEST = JJ
                  ENDIF
               ENDIF
               PPRE = P2
               P2 = AG_QNEXT(P2)
            ENDDO
            IF (PB == 0) EXIT
         ENDIF
         P = PB
         FI = CLS(AG_CLASS(P))%F
         IF (.NOT. IONLY .AND. BND_RESID(B) < FI - 1.E-9_EB) EXIT
         ! Исключение агента из очереди
         IF (PP == 0) THEN
            BND_QHEAD(B) = AG_QNEXT(P)
         ELSE
            AG_QNEXT(PP) = AG_QNEXT(P)
         ENDIF
          IF (BND_QTAIL(B) == P) BND_QTAIL(B) = PP
          BND_QLEN(B) = BND_QLEN(B) - 1
          IF (SG_NPRE(B) > 1) BND_CR(B,JBEST) = BND_CR(B,JBEST) - 1._EB
          ! R29d: выпуск из очереди границы в режиме скопления — режим
          ! разуплотнения (ограничение V <= V_qmax до разгрузки потока)
          IF (BND_CROWD(B)) AG_DIS(P) = 1
         ! Диагностика пропуска: накопление t_з (15)
         BND_NPASS(B) = BND_NPASS(B) + 1
         BND_TWSUM(B) = BND_TWSUM(B) + AG_TW(P)
         IF (AG_TW(P) > BND_TMAXW(B)) BND_TMAXW(B) = AG_TW(P)
         AG_TW(P) = 0._EB
         IF (BND_CROWD(B)) BND_ACROWD(B) = BND_ACROWD(B) + FI
         IF (EVP_MODE == 1) AG_TNEXT(P) = T + 0.5_EB*EVP_DT
           IF (SG_KIND(B) == EVP_KIND_EXIT) THEN
              ! выход наружу
              AG_STATE(P) = 2
              AG_TOUT(P) = T
              EVP_XCNT(AG_LINK(P)) = EVP_XCNT(AG_LINK(P)) + 1
              EVP_NEVAC = EVP_NEVAC + 1
             EVP_NALIVE = EVP_NALIVE - 1
              EVP_CLSEVAC(AG_CLASS(P)) = EVP_CLSEVAC(AG_CLASS(P)) + 1
              EVP_CLSTLAST(AG_CLASS(P)) = T
             SG_NEXIT(LK_SEG(AG_LINK(P))) = SG_NEXIT(LK_SEG(AG_LINK(P))) + 1
             EX_CNT(B-EVP_NSG) = EX_CNT(B-EVP_NSG) + 1
            EX_TLAST(B-EVP_NSG) = T
            EVP_TLAST = T
            AG_QNEXT(P) = EVP_FREE
            EVP_FREE = P
            IF (.NOT. IONLY) BND_RESID(B) = BND_RESID(B) - FI
          ELSEIF (SG_LSTART(B) == 0) THEN
             ! сквозной сегмент (проём нулевой длины): передача в следующую границу
             IEX = AG_XIT(P)
             NSG2 = EVP_NH(B, IEX)
             IF (NSG2 <= 0) CALL SHUTDOWN('ERROR: EVAC+ routing failure at pass-through segment')
             CALL EVP_QUEUE_APPEND(NSG2, P, AG_S(P), .FALSE.)
             HANDOFF = .TRUE.
             BND_RESID(B) = BND_RESID(B) - FI
         ELSE
            ! вход на первый сегмент участка: контроль свободного места
            K1 = SG_LSTART(B)
            IF (REAL(LK_CNT(K1)+1,EB)*FI > EVP_ROOM_EPS*LK_B(K1)*LK_LEN(K1)) THEN
               ! места нет: агент возвращается в голову очереди
               AG_QNEXT(P) = BND_QHEAD(B)
               IF (BND_QHEAD(B) == 0) BND_QTAIL(B) = P
               BND_QHEAD(B) = P
               BND_QLEN(B) = BND_QLEN(B) + 1
               EXIT
            ENDIF
              AG_STATE(P) = 0
              OLDSG = LK_SEG(AG_LINK(P))
              AG_LINK(P) = K1
              AG_V(P) = 0._EB
              LK_CNT(K1) = LK_CNT(K1) + 1
              LK_FA(K1) = LK_FA(K1) + FI
              SG_NGONE(OLDSG) = SG_NGONE(OLDSG) + 1
              SG_NIN(B) = SG_NIN(B) + 1
              BND_RESID(B) = BND_RESID(B) - FI
         ENDIF
      ENDDO
      IF (BND_QHEAD(B) == 0) THEN
         BND_QTAIL(B) = 0
         BND_LEFT(B) = .FALSE.
         IF (BND_CROWD(B)) CALL EVP_CROWD_EVENT(B, T, .TRUE.)
      ELSE
         BND_LEFT(B) = .TRUE.
      ENDIF
   ENDDO
END SUBROUTINE EVP_BOUNDARY_SWEEP


! Событие скопления на границе: образование/растворение; измеренное время
! существования и оценка по формуле (16) — диагностика t_ск (П6.13)
SUBROUTINE EVP_CROWD_EVENT(B, T, DISSOLVE)
   INTEGER, INTENT(IN) :: B
   REAL(EB), INTENT(IN) :: T
   LOGICAL, INTENT(IN) :: DISSOLVE
   INTEGER :: IK
   REAL(EB) :: DUR

   IF (DISSOLVE) THEN
      DUR = T - BND_TCR0(B)
      BND_TSK_SIM(B) = BND_TSK_SIM(B) + DUR
       IF (BND_QD(B) > 0._EB .AND. BND_ACROWD(B) > 0._EB) THEN
          BND_TSK_EST(B) = BND_TSK_EST(B) + BND_ACROWD(B)*60._EB/(BND_QD(B)*SG_B(B))
       ENDIF
       IF (EVP_DUMPS_OPEN .AND. EVP_OUTRUN) THEN
          WRITE(LU_EVPOUT,'(A,A,A,F9.2,A,F9.2,A,F9.2,A,F9.2,A)') &
            ' Crowd dissolved at boundary ',TRIM(SG_ID(B)),' t=',T,' s; duration=',DUR, &
            ' s; max wait=',BND_TMAXW(B),' s; area passed=',BND_ACROWD(B),' m2'
      ENDIF
      BND_CROWD(B) = .FALSE.
      BND_ACROWD(B) = 0._EB
   ELSE
      BND_TCR0(B) = T
      BND_CROWD(B) = .TRUE.
      BND_NEP(B) = BND_NEP(B) + 1
      IF (SG_KIND(B) == EVP_KIND_EXIT) THEN
         IK = 2
      ELSE
         IK = SG_KIND(B)
      ENDIF
       BND_QD(B) = EVP_QDMAX_OF(IK, SG_B(B))
       IF (EVP_DUMPS_OPEN .AND. EVP_OUTRUN) THEN
          WRITE(LU_EVPOUT,'(A,A,A,F9.2,A,F7.2)') ' Crowd formed at boundary ',TRIM(SG_ID(B)), &
            ' t=',T,' s; q_Dmax=',BND_QD(B),' m/min'
      ENDIF
   ENDIF
END SUBROUTINE EVP_CROWD_EVENT


! Добавление агента в очередь границы сегмента NSG (остаток переноса OVER, формула (9)).
! COUNT_OUT=.TRUE. — уход с участка (ссылка AG_LINK ещё на звене исходного участка):
! приём только для переходов из фазы перемещения; каскадные передачи между
! очередями сквозных сегментов уходом не считаются
SUBROUTINE EVP_QUEUE_APPEND(NSG, I, OVER, COUNT_OUT)
    INTEGER, INTENT(IN) :: NSG, I
    REAL(EB), INTENT(IN) :: OVER
    LOGICAL, INTENT(IN) :: COUNT_OUT

    IF (COUNT_OUT) SG_NOUT(LK_SEG(AG_LINK(I))) = SG_NOUT(LK_SEG(AG_LINK(I))) + 1
    AG_STATE(I) = 1
   AG_V(I) = 0._EB
   AG_QNEXT(I) = 0
   IF (SG_LSTART(NSG) > 0) THEN
      AG_S(I) = MIN(OVER, LK_LEN(SG_LSTART(NSG)))
   ELSE
      AG_S(I) = OVER
   ENDIF
   IF (BND_QTAIL(NSG) == 0) THEN
      BND_QHEAD(NSG) = I
   ELSE
      AG_QNEXT(BND_QTAIL(NSG)) = I
   ENDIF
   BND_QTAIL(NSG) = I
   BND_QLEN(NSG) = BND_QLEN(NSG) + 1
   IF (BND_QLEN(NSG) > BND_QMAX(NSG)) BND_QMAX(NSG) = BND_QLEN(NSG)
END SUBROUTINE EVP_QUEUE_APPEND


! Скорость по плотности: формулы (3)-(5) модели; возврат в м/с.
! DISP=.TRUE. — случайная составляющая скорости (13): sigma(V_D) = sigma(V0)*F1,
! нормально распределённая, с кинематическим усечением +-3 сигма и пределом
! ходьбы EVP_VMAX (разд. 10 пп. 7-8 модели; sigma(V0) — SIGMA_V0 либо Cv*V0)
FUNCTION EVP_SPEED(IC, IK, D, BW, DISP) RESULT(V)
   INTEGER, INTENT(IN) :: IC, IK
   REAL(EB), INTENT(IN) :: D, BW
   LOGICAL, INTENT(IN), OPTIONAL :: DISP
   REAL(EB) :: V, VMIN, DM2, FF, F1, SIG, VS, U1, U2, ZN

   FF = CLS(IC)%F
   DM2 = D*FF
   IF (D <= CLS(IC)%D0(IK) .OR. D < 1.E-10_EB) THEN
      F1 = 1._EB
      VMIN = CLS(IC)%V0(IK)
   ELSE
      F1 = 1._EB - CLS(IC)%AV(IK)*LOG(D/CLS(IC)%D0(IK))
      VMIN = CLS(IC)%V0(IK)*F1
   ENDIF
   SIG = 0._EB
   IF (PRESENT(DISP)) THEN
      IF (DISP) SIG = CLS(IC)%SIG(IK)
   ENDIF
    IF (SIG > 0._EB) THEN
       CALL RANDOM_NUMBER(U1)
       CALL RANDOM_NUMBER(U2)
       U1 = MAX(U1, 1.E-12_EB)
       ZN = SQRT(-2._EB*LOG(U1))*COS(2._EB*EVP_PI*U2)
       VS = CLS(IC)%V0(IK)*F1 + ZN*SIG*F1
       ! Кинематическое усечение (разд. 10 п. 8 модели): правило трёх сигм
       ! вокруг среднего V0*F1 и физический предел скорости ходьбы EVP_VMAX
       ! сверху; предотвращаются отрицательные скорости и скорости бега
       VS = MIN(VS, CLS(IC)%V0(IK)*F1 + 3._EB*SIG*F1)
       VS = MAX(VS, CLS(IC)%V0(IK)*F1 - 3._EB*SIG*F1)
       VMIN = MIN(MAX(VS, 0._EB), EVP_VMAX)
    ENDIF
   ! Коэффициент влияния проёма (формула (4))
   IF (IK == 2 .AND. DM2 >= 0.5_EB) VMIN = VMIN*(1.25_EB - 0.05_EB*DM2)
   ! Ограничение предельной интенсивности q_max (П2.8)
   IF (D > 1.E-10_EB) VMIN = MIN(VMIN, EVP_QMAX(IK)/(D*FF))
   ! Режим скопления: движение с интенсивностью q_Dmax (П2.8)
   IF (DM2 > EVP_DMAX_M2 .AND. D > 1.E-10_EB) VMIN = EVP_QDMAX_OF(IK, BW)/(D*FF)
   V = MAX(VMIN, 0._EB)/60._EB
END FUNCTION EVP_SPEED


! q_Dmax по виду пути; для проёма — функция ширины (П2.8)
FUNCTION EVP_QDMAX_OF(IK, BW) RESULT(Q)
   INTEGER, INTENT(IN) :: IK
   REAL(EB), INTENT(IN) :: BW
   REAL(EB) :: Q

   IF (IK == 2 .AND. BW < 1.6_EB) THEN
      Q = 2.5_EB + 3.75_EB*BW
   ELSE
      Q = EVP_QDMAXF(IK)
   ENDIF
END FUNCTION EVP_QDMAX_OF


! ---------------------------------------------------------------------------
! Вывод: EVA5 (Smokeview), CSV, итоговая статистика
! ---------------------------------------------------------------------------

! Кадр EVA5 в файл LU_PART(NM); формат идентичен DUMP_EVAC (evac.f90)
SUBROUTINE EVAC_PLUS_DUMP(T, NM)
   REAL(EB), INTENT(IN) :: T
   INTEGER, INTENT(IN) :: NM
   INTEGER :: I, N, NPLIM, IZERO, K, ISG
   REAL(EB) :: ZAG, RR
   REAL(FB), ALLOCATABLE, DIMENSION(:) :: XP, YP, ZP
   REAL(FB), ALLOCATABLE, DIMENSION(:,:) :: AP
   INTEGER, ALLOCATABLE, DIMENSION(:) :: TA
   TYPE (MESH_TYPE), POINTER :: M => NULL()

   IF (.NOT. EVP_READY) RETURN
   IF (.NOT. (EVACUATION_ONLY(NM) .AND. EVACUATION_GRID(NM))) RETURN
   IF (USE_MPI .AND. MYID /= PROCESS(NM)) RETURN
   M => MESHES(NM)

   NPLIM = 0
   DO I = 1, EVP_NAG
      IF (AG_STATE(I) /= 2) NPLIM = NPLIM + 1
   ENDDO
   NPLIM = MIN(NPLIM, EVP_NPPS)

   ALLOCATE(TA(NPLIM), STAT=IZERO); CALL ChkMemErr('EVAC_PLUS_DUMP','TA',IZERO)
   ALLOCATE(XP(NPLIM), STAT=IZERO); CALL ChkMemErr('EVAC_PLUS_DUMP','XP',IZERO)
   ALLOCATE(YP(NPLIM), STAT=IZERO); CALL ChkMemErr('EVAC_PLUS_DUMP','YP',IZERO)
   ALLOCATE(ZP(NPLIM), STAT=IZERO); CALL ChkMemErr('EVAC_PLUS_DUMP','ZP',IZERO)
   ALLOCATE(AP(NPLIM,4), STAT=IZERO); CALL ChkMemErr('EVAC_PLUS_DUMP','AP',IZERO)

   N = 0
   DO I = 1, EVP_NAG
      IF (AG_STATE(I) == 2) CYCLE
      N = N + 1
      IF (N > NPLIM) EXIT
      K = AG_LINK(I)
      XP(N) = REAL(LK_X0(K) + LK_UX(K)*AG_S(I) - LK_UY(K)*AG_LAT(I), FB)
      YP(N) = REAL(LK_Y0(K) + LK_UY(K)*AG_S(I) + LK_UX(K)*AG_LAT(I), FB)
      ISG = LK_SEG(K)
      ZAG = MIN(MAX(SG_Z(ISG), M%ZS), M%ZF)
      ZP(N) = REAL(ZAG, FB)
      ! Угол ориентации аватара: направление звена, [0,360)
      AP(N,1) = REAL(MOD(ATAN2(LK_UY(K),LK_UX(K))*180._EB/EVP_PI + 360._EB, 360._EB), FB)
      RR = MAX(0.18_EB, SQRT(CLS(AG_CLASS(I))%F/EVP_PI))
      AP(N,2) = REAL(2._EB*RR, FB)
      AP(N,3) = REAL(2._EB*0.55_EB*RR, FB)
      AP(N,4) = 1.75_FB
      TA(N) = AG_ID(I)
   ENDDO

   WRITE(LU_PART(NM)) REAL(T, FB)
   WRITE(LU_PART(NM)) NPLIM
   WRITE(LU_PART(NM)) (XP(I),I=1,NPLIM),(YP(I),I=1,NPLIM),(ZP(I),I=1,NPLIM), &
        (AP(I,1),I=1,NPLIM),(AP(I,2),I=1,NPLIM),(AP(I,3),I=1,NPLIM),(AP(I,4),I=1,NPLIM)
   WRITE(LU_PART(NM)) (TA(I),I=1,NPLIM)

   DEALLOCATE(AP); DEALLOCATE(ZP); DEALLOCATE(YP); DEALLOCATE(XP); DEALLOCATE(TA)
END SUBROUTINE EVAC_PLUS_DUMP


! Открытие файлов вывода CHID_evac.out и CHID_evac.csv
SUBROUTINE EVAC_PLUS_INIT_DUMPS(TIN)
    REAL(EB), INTENT(IN) :: TIN
    INTEGER :: N, EVP_NTH
    CHARACTER(10000) :: HDR

   IF (.NOT. EVP_ON) RETURN
   LU_EVPOUT = GET_FILE_NUMBER()
   OPEN(LU_EVPOUT, FILE=TRIM(CHID)//'_evac.out', FORM='FORMATTED', STATUS='REPLACE')
   LU_EVPCSV = GET_FILE_NUMBER()
   OPEN(LU_EVPCSV, FILE=TRIM(CHID)//'_evac.csv', FORM='FORMATTED', STATUS='REPLACE')

     WRITE(LU_EVPOUT,'(/A)')     ' EVAC+ Evacuation Module (Order MCHS No. 1140)'
     WRITE(LU_EVPOUT,'(A,A)')    ' Revision               : ',TRIM(EVP_REVDATE)
     EVP_NTH = 1
!$   EVP_NTH = OMP_GET_MAX_THREADS()
     WRITE(LU_EVPOUT,'(A,I6)')   ' OpenMP threads         : ',EVP_NTH
     WRITE(LU_EVPOUT,'(A,F6.3)') ' Time step DT, s        : ',EVP_DT
     WRITE(LU_EVPOUT,'(A,I6)')   ' NLOCAL (local density) : ',EVP_NLOCAL
     IF (EVP_MODE == 0) THEN
        WRITE(LU_EVPOUT,'(A)')   ' Step mode              : FIXED'
     ELSE
        WRITE(LU_EVPOUT,'(A)')   ' Step mode              : WANDER'
        WRITE(LU_EVPOUT,'(A,I8)')   ' Batch runs (RUNS)      : ',EVP_RUNS
        WRITE(LU_EVPOUT,'(A,F8.4)') ' Quantile probability   : ',EVP_PROB
        IF (EVP_RUNS == 1) WRITE(LU_EVPOUT,'(A)') &
           ' WARNING: RUNS=1 in WANDER: single run is not a P=0.999 estimate (app. 8)'
     ENDIF
    WRITE(LU_EVPOUT,'(A,I8)')   ' Random seed            : ',EVP_SEED
    WRITE(LU_EVPOUT,'(A,I6)')   ' Segments               : ',EVP_NSG
    WRITE(LU_EVPOUT,'(A,I6)')   ' Exits                  : ',EVP_NEX
    WRITE(LU_EVPOUT,'(A,I6)')   ' Sources                : ',EVP_NHR
    WRITE(LU_EVPOUT,'(A,I8)')   ' Agents planned         : ',EVP_NAGMAX-16
    WRITE(LU_EVPOUT,'(A,F10.2)') ' Initialization time, s : ',TIN

    HDR = 'Time_s,N_inside,N_evac_total'
    DO N = 1, EVP_NEX
       HDR = TRIM(HDR)//',Exit_'//TRIM(EX_ID(N))
    ENDDO
    ! Диагностика границ: длина очереди и признак скопления (q_Dmax)
    DO N = 1, EVP_NSGTOT
        HDR = TRIM(HDR)//',Q_'//TRIM(SG_ID(N))//',C_'//TRIM(SG_ID(N))
     ENDDO
     WRITE(LU_EVPCSV,'(A)') TRIM(HDR)
     IF (EVP_PROTDT > 0._EB) THEN
        LU_EVPROT = GET_FILE_NUMBER()
        OPEN(LU_EVPROT, FILE=TRIM(CHID)//'_evac_prot.csv', FORM='FORMATTED', STATUS='REPLACE')
        ! Расчётный протокол: фактические подстановки формул Методики по
        ! участкам (N, D м2/м2, V м/мин, q=D*V, ширина) и границам (режим q,
        ! ширина, очередь, скопление, пропущено) на контрольные моменты
        WRITE(LU_EVPROT,'(A)') 'Time_s,Item,ID,N,D_m2m2,V_m_min,q_m_min,Width_m,Queue,Crowd,Passed_cum'
        EVP_TPROT = TIN
     ENDIF
     EVP_DUMPS_OPEN = .TRUE.
  END SUBROUTINE EVAC_PLUS_INIT_DUMPS


! Строка CSV: время, численность внутри, эвакуировано, по выходам,
! длины очередей и признаки скопления по границам
SUBROUTINE EVAC_PLUS_DUMP_CSV(T)
    REAL(EB), INTENT(IN) :: T
    INTEGER :: N
    CHARACTER(60) :: TCF

    IF (.NOT. EVP_DUMPS_OPEN) RETURN
    WRITE(TCF,'(A,I0,A)') '(ES13.6,', EVP_NEX+2+2*EVP_NSGTOT, '(",",I8))'
    WRITE(LU_EVPCSV,TCF) T, EVP_NALIVE, EVP_NEVAC, (EX_CNT(N), N=1,EVP_NEX), &
         ((BND_QLEN(N), MERGE(1,0,BND_CROWD(N))), N=1,EVP_NSGTOT)
END SUBROUTINE EVAC_PLUS_DUMP_CSV


! Запись блока расчётного протокола на момент T: по одному блоку SEG на
! каждый участок со звеньями и BND на каждую действующую границу. D —
! балансная плотность участка (N*f/(l*b), м2/м2); V — средняя фактическая
! скорость агентов участка, м/мин; q = D*V (2). Состояния массивов — на
! момент вызова (после фазы 6 шага)
SUBROUTINE EVP_PROTOCOL_WRITE(T)
    REAL(EB), INTENT(IN) :: T
    INTEGER :: S, B, K, I, KL, NS
    REAL(EB) :: FA, VSUM, D2, VMM, QQ
    REAL(EB), ALLOCATABLE :: VSM(:)
    CHARACTER(80) :: FMT

    IF (.NOT. EVP_DUMPS_OPEN .OR. LU_EVPROT <= 0) RETURN
    ! Средние скорости участков (R31): один проход по агентам вместо обхода
    ! всех агентов на каждый участок (порядок слотов сохранён — результат
    ! идентичен R30)
    ALLOCATE(VSM(EVP_NSG))
    VSM = 0._EB
    DO I = 1, EVP_NAG
       IF (AG_STATE(I) /= 0) CYCLE
       S = LK_SEG(AG_LINK(I))
       VSM(S) = VSM(S) + AG_V(I)
    ENDDO
    FMT = '(ES13.6,A,A,A,I0,A,ES13.6,A,ES13.6,A,ES13.6,A,ES13.6,A,I0,A,I0,A,I0)'
    DO S = 1, EVP_NSG
       IF (SG_NLINK(S) == 0) CYCLE
       NS = 0; FA = 0._EB; VSUM = 0._EB
       DO KL = 1, SG_NLINK(S)
          K = SG_LSTART(S) + KL - 1
          NS = NS + LK_CNT(K)
          FA = FA + LK_FA(K)
       ENDDO
       VSUM = VSM(S)
       D2 = 0._EB; VMM = 0._EB
       IF (SG_LEN(S) > 0._EB) D2 = FA/(SG_B(S)*SG_LEN(S))
       IF (NS > 0) VMM = VSUM/REAL(NS,EB)*60._EB
       QQ = D2*VMM
       WRITE(LU_EVPROT,FMT) T,',SEG,',TRIM(SG_ID(S)),',',NS,',',D2,',',VMM,',',QQ,',',SG_B(S),',',0,',',0,',',0
    ENDDO
    DEALLOCATE(VSM)

    DO B = 1, EVP_NSGTOT
       IF (SG_NPRE(B) == 0) CYCLE
       IF (SG_KIND(B) == EVP_KIND_EXIT) THEN
          K = 2
       ELSE
          K = SG_KIND(B)
       ENDIF
       IF (BND_CROWD(B)) THEN
          QQ = EVP_QDMAX_OF(K, SG_B(B))
       ELSE
          QQ = EVP_QMAX(K)
       ENDIF
       WRITE(LU_EVPROT,FMT) T,',BND,',TRIM(SG_ID(B)),',',BND_QLEN(B),',',0._EB,',',0._EB,',',QQ,',',SG_B(B), &
          ',',BND_QLEN(B),',',MERGE(1,0,BND_CROWD(B)),',',BND_NPASS(B)
    ENDDO
END SUBROUTINE EVP_PROTOCOL_WRITE


! Итоговая статистика прогона
SUBROUTINE EVAC_PLUS_FINAL_STATS
    INTEGER :: N, B, I, NB, NLIVE(EVP_NSGTOT)
    REAL(EB) :: DMAXL, TFIN

     IF (.NOT. EVP_DUMPS_OPEN) RETURN
     ! Незакрытые эпизоды скоплений
     DO B = 1, EVP_NSGTOT
        IF (BND_CROWD(B)) CALL EVP_CROWD_EVENT(B, EVP_TSTEP, .TRUE.)
     ENDDO
     ! Точный пересчёт счётчиков звеньев и финальный блок протокола
     ! (в пакетном режиме финальный блок прогона 1 записан при его завершении)
     IF (.NOT. EVP_PROTFIN) CALL EVP_REFRESH_LINK_COUNTS
     IF (EVP_PROTDT > 0._EB .AND. LU_EVPROT > 0 .AND. .NOT. EVP_PROTFIN) THEN
        TFIN = MAX(EVP_TSTEP, 0._EB)
        CALL EVP_PROTOCOL_WRITE(TFIN)
     ENDIF
    WRITE(LU_EVPOUT,'(/A)') ' Evacuation summary'
   WRITE(LU_EVPOUT,'(A,I8)')  '   Elementary links   : ',EVP_NLK
   WRITE(LU_EVPOUT,'(A,I8)')  '   Agents generated : ',EVP_ILABEL
   WRITE(LU_EVPOUT,'(A,I8)')  '   Agents evacuated : ',EVP_NEVAC
   WRITE(LU_EVPOUT,'(A,I8)')  '   Agents remaining : ',EVP_NALIVE
   WRITE(LU_EVPOUT,'(A,I10)') '   Steps executed   : ',EVP_NSTEPS
   WRITE(LU_EVPOUT,'(A,F10.3)') '   Engine wall time, s: ',EVP_TWALL
    WRITE(LU_EVPOUT,'(A)')   '   Phase time, s: [0-2 counts/densities/lists] [3a move(OpenMP)] [3b queues] [4 recount] [5 sweep] [6+7 control/protocol]'
    WRITE(LU_EVPOUT,'(6F12.3)') EVP_TPH(1:6)

    ! Сводка пакета (этап 6): протокольное число — квантиль P; медиана и
    ! P=0.95 — справочно. Усечённый случай (T_END раньше завершения серии) —
    ! предупреждение без квантиля
    IF (EVP_RUNS > 1) THEN
       IF (.NOT. EVP_DISTWRIT) CALL EVP_BATCH_FINALIZE
       WRITE(LU_EVPOUT,'(/A)') ' Stochastic batch summary (model sec. 8; order 1140, app. 8, closing paragraph)'
       WRITE(LU_EVPOUT,'(A,I8,A,I8)') '   Runs done / requested : ',EVP_RUNSDONE,' /',EVP_RUNS
       IF (EVP_DISTWRIT) THEN
          WRITE(LU_EVPOUT,'(A,F8.4)') '   Quantile probability  : ',EVP_PROB
          WRITE(LU_EVPOUT,'(A,I8)')   '   Order statistic k     : ',EVP_QK
          WRITE(LU_EVPOUT,'(A,F10.2)') '   T_quantile, s         : ',EVP_SQ
          WRITE(LU_EVPOUT,'(A,F10.2)') '   Reference median, s   : ',EVP_SMED
          WRITE(LU_EVPOUT,'(A,F10.2)') '   Reference P=0.95, s   : ',EVP_SQ95
          WRITE(LU_EVPOUT,'(A,F10.2,A,F10.2)') '   Sample min / max, s   : ',EVP_SMIN,' /',EVP_SMAX
          WRITE(LU_EVPOUT,'(A,F10.2)') '   Sample mean, s        : ',EVP_SMEAN
          WRITE(LU_EVPOUT,'(A,F10.2)') '   Sample sigma, s       : ',EVP_SSIG
          WRITE(LU_EVPOUT,'(A,A,A)')   '   Distribution file     : ',TRIM(CHID),'_evac_dist.csv'
          WRITE(LU_EVPOUT,'(A)')       '   Protocol number: quantile; per-step outputs (EVA5/csv/protocol) cover run 1'
       ELSE
          WRITE(LU_EVPOUT,'(A)') '   Batch incomplete: quantile not estimated'
       ENDIF
       WRITE(LU_EVPOUT,'(A,I8)') '   Single-run counters below refer to realization # ',EVP_BATCHRUN
    ENDIF
    IF (EVP_TLAST >= 0._EB) THEN
      WRITE(LU_EVPOUT,'(A,F10.2)') '   Last exit time, s: ',EVP_TLAST
   ELSE
      WRITE(LU_EVPOUT,'(A)') '   Last exit time, s: none'
   ENDIF
   DO N = 1, EVP_NEX
      WRITE(LU_EVPOUT,'(A,A,A,I8)') '   Exit ',TRIM(EX_ID(N)),' passed: ',EX_CNT(N)
      IF (EX_TLAST(N) >= 0._EB) WRITE(LU_EVPOUT,'(A,A,A,F10.2)') '   Exit ',TRIM(EX_ID(N)),' last at, s: ',EX_TLAST(N)
   ENDDO

   ! Диагностика границ: t_з (15), t_ск (16), максимумы очередей
   WRITE(LU_EVPOUT,'(/A)') ' Boundary diagnostics (t_d = mean/max queue delay (15); t_cr = crowd duration vs estimate (16))'
   DO B = 1, EVP_NSGTOT
      IF (BND_NPASS(B) == 0 .AND. BND_QMAX(B) == 0) CYCLE
      WRITE(LU_EVPOUT,'(A,A)')    '   Boundary              : ',TRIM(SG_ID(B))
      WRITE(LU_EVPOUT,'(A,I8)')   '     Passed, agents      : ',BND_NPASS(B)
      IF (BND_NPASS(B) > 0) THEN
         WRITE(LU_EVPOUT,'(A,F10.2)') '     Mean t_d, s         : ',BND_TWSUM(B)/REAL(BND_NPASS(B),EB)
      ENDIF
      WRITE(LU_EVPOUT,'(A,F10.2)') '     Max  t_d, s         : ',BND_TMAXW(B)
      WRITE(LU_EVPOUT,'(A,I8)')   '     Max queue, agents   : ',BND_QMAX(B)
      WRITE(LU_EVPOUT,'(A,I8)')   '     Crowd episodes      : ',BND_NEP(B)
      WRITE(LU_EVPOUT,'(A,F10.2)') '     Sum t_cr (meas.), s : ',BND_TSK_SIM(B)
      WRITE(LU_EVPOUT,'(A,F10.2)') '     Sum t_cr (16), s    : ',BND_TSK_EST(B)
   ENDDO

    ! Контроль кумулятивной ошибки плотностей (разд. 9 модели)
    WRITE(LU_EVPOUT,'(/A)') ' Density balance control (model sec. 9)'
    IF (EVP_ECNT > 0) THEN
       DMAXL = 0._EB
       DO B = 1, EVP_NLK
          IF (LK_DERR(B) > DMAXL) DMAXL = LK_DERR(B)
       ENDDO
       WRITE(LU_EVPOUT,'(A,I10)')  '     Comparisons          : ',EVP_ECNT
       WRITE(LU_EVPOUT,'(A,F10.5)')'     Mean epsilon         : ',EVP_ESUM/REAL(EVP_ECNT,EB)
       WRITE(LU_EVPOUT,'(A,F10.5)')'     Max  epsilon (link)  : ',DMAXL
       WRITE(LU_EVPOUT,'(A,I10)')  '     Resynchronizations   : ',EVP_NRESYNC
    ELSE
       WRITE(LU_EVPOUT,'(A)') '     No comparisons (empty links)'
    ENDIF

    ! Статистика по классам мобильности (этап 3); момент последнего выхода
    ! класса — контроль замыкающей группы смешанного потока (В5)
     WRITE(LU_EVPOUT,'(/A)')     ' Mobility class statistics'
     DO N = 1, EVP_NCLSUSED
       IF (EVP_CLSGEN(N) == 0) CYCLE
       IF (EVP_CLSTLAST(N) >= 0._EB) THEN
          WRITE(LU_EVPOUT,'(A,A,A,I8,A,I8,A,I8,A,F10.2)') '   Class ',TRIM(CLS(N)%NAME), &
             ': generated ',EVP_CLSGEN(N),'; evacuated ',EVP_CLSEVAC(N), &
             '; remaining ',EVP_CLSGEN(N)-EVP_CLSEVAC(N),'; last exit at, s: ',EVP_CLSTLAST(N)
       ELSE
          WRITE(LU_EVPOUT,'(A,A,A,I8,A,I8,A,I8)') '   Class ',TRIM(CLS(N)%NAME), &
             ': generated ',EVP_CLSGEN(N),'; evacuated ',EVP_CLSEVAC(N), &
             '; remaining ',EVP_CLSGEN(N)-EVP_CLSEVAC(N)
       ENDIF
    ENDDO

    ! Баланс численности по участкам: размещено + вошло − передано далее = на участке + эвакуировано
    NLIVE = 0
    DO I = 1, EVP_NAG
       IF (AG_STATE(I) == 2) CYCLE
       NLIVE(LK_SEG(AG_LINK(I))) = NLIVE(LK_SEG(AG_LINK(I))) + 1
    ENDDO
    WRITE(LU_EVPOUT,'(/A)') ' Segment balance (placed+entered-handed-over must equal on-segment+exited)'
    DO B = 1, EVP_NSG
       IF (SG_NLINK(B) == 0) CYCLE
       NB = SG_NPLACE(B) + SG_NIN(B) - SG_NGONE(B) - NLIVE(B) - SG_NEXIT(B)
       WRITE(LU_EVPOUT,'(A,A,A,I8,A,I8,A,I8,A,I8,A,I8,A,I7)') '   ',TRIM(SG_ID(B)), &
          ': placed ',SG_NPLACE(B),'; entered ',SG_NIN(B),'; queued-out ',SG_NOUT(B), &
          '; handed-over ',SG_NGONE(B),'; on-segment ',NLIVE(B),'; balance ',NB
    ENDDO

    IF (LU_EVPROT > 0) THEN
       CLOSE(LU_EVPROT)
       LU_EVPROT = 0
    ENDIF
    CLOSE(LU_EVPCSV)
    CLOSE(LU_EVPOUT)
    EVP_DUMPS_OPEN = .FALSE.
END SUBROUTINE EVAC_PLUS_FINAL_STATS


! Пересчёт счётчиков звеньев по фактическому состоянию агентов (этап 3):
! устраняет устаревшие счётчики после выпусков фазы 5 последнего шага
SUBROUTINE EVP_REFRESH_LINK_COUNTS
    INTEGER :: I, K

    LK_CNT = 0
    LK_FA = 0._EB
    DO I = 1, EVP_NAG
       IF (AG_STATE(I) == 2) CYCLE
       K = AG_LINK(I)
       LK_CNT(K) = LK_CNT(K) + 1
       LK_FA(K) = LK_FA(K) + CLS(AG_CLASS(I))%F
    ENDDO
END SUBROUTINE EVP_REFRESH_LINK_COUNTS


! ---------------------------------------------------------------------------
! Служебные функции
! ---------------------------------------------------------------------------

! Индекс вида пути по строке KIND
FUNCTION EVP_KIND_INDEX(KINDSTR) RESULT(IK)
   CHARACTER(*), INTENT(IN) :: KINDSTR
   INTEGER :: IK
   SELECT CASE (TRIM(KINDSTR))
   CASE ('HORIZONTAL'); IK = 1
   CASE ('DOOR');       IK = 2
   CASE ('STAIR_DOWN'); IK = 3
   CASE ('STAIR_UP');   IK = 4
   CASE ('RAMP_DOWN');  IK = 5
   CASE ('RAMP_UP');    IK = 6
   CASE DEFAULT
      WRITE(MESSAGE,'(A,A)') 'ERROR: unknown EVSG/EVCP KIND: ',TRIM(KINDSTR)
      CALL SHUTDOWN(MESSAGE)
      IK = 0
   END SELECT
END FUNCTION EVP_KIND_INDEX


! Поиск класса мобильности по строке CLASS; 0 — не найдено (без диагностики)
FUNCTION EVP_CLASS_LOOKUP(CLSSTR) RESULT(IC)
   CHARACTER(*), INTENT(IN) :: CLSSTR
   INTEGER :: IC, N
   SELECT CASE (TRIM(CLSSTR))
   CASE ('M0-1'); IC = 1
   CASE ('M0-2'); IC = 2
   CASE ('M0-3'); IC = 3
   CASE ('M0-4'); IC = 4
   CASE ('M0-5'); IC = 5
   CASE ('M0-6'); IC = 6
   CASE ('M0-7'); IC = 7
   CASE ('M1');   IC = 8
   CASE ('M2');   IC = 9
   CASE ('M3');   IC = 10
   CASE ('M4');   IC = 11
   CASE ('M1D');  IC = 12
   CASE ('M1C');  IC = 13
   CASE ('M1G');  IC = 14
   CASE ('M1B');  IC = 15
   CASE ('M2S');  IC = 16
   CASE DEFAULT
      ! пользовательские классы (&EVCP DEFINE=.TRUE., R31)
      IC = 0
      DO N = EVP_NCLASS+1, EVP_NCLSUSED
         IF (TRIM(CLSSTR) == TRIM(CLS(N)%NAME)) THEN
            IC = N
            EXIT
         ENDIF
      ENDDO
   END SELECT
END FUNCTION EVP_CLASS_LOOKUP


! Индекс класса мобильности по строке CLASS; неизвестный класс — ошибка
FUNCTION EVP_CLASS_INDEX(CLSSTR) RESULT(IC)
   CHARACTER(*), INTENT(IN) :: CLSSTR
   INTEGER :: IC
   IC = EVP_CLASS_LOOKUP(CLSSTR)
   IF (IC == 0) THEN
      WRITE(MESSAGE,'(A,A)') 'ERROR: unknown EVHR/EVCP CLASS: ',TRIM(CLSSTR)
      CALL SHUTDOWN(MESSAGE)
   ENDIF
END FUNCTION EVP_CLASS_INDEX


SUBROUTINE GET_REV_EVACP(REV_NUMBER, REV_DATE)
   INTEGER, INTENT(INOUT) :: REV_NUMBER
   CHARACTER(255), INTENT(INOUT) :: REV_DATE
   IF (EVP_REVNUM > REV_NUMBER) THEN
      REV_NUMBER = EVP_REVNUM
      WRITE(REV_DATE,'(A)') EVP_REVDATE
   ENDIF
END SUBROUTINE GET_REV_EVACP

END MODULE EVACPLUS


