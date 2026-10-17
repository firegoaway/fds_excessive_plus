# HANDOFF: план B, этап 3 — R29b завершён (слой совместимости чтения/вывода)

Версия: 1.0
Дата: 2026-09-24, 23:15 MSK
Статус: этап 3 выполнен; приёмка (а)(б)(г) пройдена, (в) — по структурным маркерам .smv.
Родительские документы: `HANDOFF_plan_B_fds5_rebase.md`, `HANDOFF_plan_B_stage3_R29b.md`.

## 1. Сборка

- Сборки этапа 3: 22:58:16 и 23:04:01 (линк `fds5_impi_intel_win.exe`, каталог
  `Build/impi_intel_win_ng/`). Ошибок компиляции нет; хвостовой сбой bat — только
  `copy /y` на имя `fds5ng.exe`, занятое базовым прогоном (ожидаемо, §4.3 родителя).
- `fds5ng.exe` всё ещё сборки 21:26 (R29a): базовый прогон держит имя. После его
  завершения — `copy /y fds5_impi_intel_win.exe fds5ng.exe`.

## 2. Перенесено (тег R29b в комментариях)

### 2.1. cons.f90 (дополнение к этапу 2)

- `MAXIMUM_VISIBILITY` в списке глобалов (пропуск этапа 2, фикс #17 совместимости);
- `LU_DEVC_CTRL`, `FN_DEVC_CTRL` (журнал `_devc_ctrl_log.csv`).

### 2.2. read.f90

- Заглушки `READ_WIND`/`READ_COMB` (чтение и игнорирование `&WIND`, `&COMB`).
- EXTERNAL-управление: включение `READ_EXTERNAL` после `READ_TIME`.
- `&TIME`: член `DT_EXTERNAL`.
- `&MISC`: члены `VISIBILITY_FACTOR`, `MAXIMUM_VISIBILITY`, `MAX_LEAK_PATHS`,
  `MINIMUM_ZONE_VOLUME`, `EXTERNAL_FILENAME`, `CCC_DEFAULT`,
  `EMBEDDED_MESH_RATIO`, `RELAXED_ALIGNMENT`; сентинелы -1 для видимости;
  дефолты после `MISC_LOOP`; `TEMP_MIN_SMV` из TMPA.
- `&MESH`: член `EMBEDDED` + флаг `EMBEDDED_MESH_FLAG/RATIO`; назначение мешей
  непрерывными блоками (R28o донора: MPI_PROCESS читается, но игнорируется —
  корректно с коллективами R29a в main.f90).
- `&DUMP`: дефолты FDS6 (`NFRAMES=-1` → кадр/с, `FLUSH_FILE_BUFFERS=.FALSE.`,
  `COLUMN_DUMP_LIMIT=.FALSE.`, `DT_BNDF/DT_DEVC=1.25`, `DT_FLUSH=50`) и
  телеметрические члены донора (`DIAGNOSTICS_INTERVAL`, `CFL_FILE`, `DT_CFL`,
  `STEPS_FILE`, `DT_STEPS`, `CPU_FILE`, `DT_CPU`) — только толерантное чтение
  локальными переменными, без глобалов и без записи файлов.
- `&SPEC`: люмп-виды (`LUMPED_COMPONENT_ONLY`/`BACKGROUND`/`COPY_LUMPED`/
  `PRIMITIVE`, `SPEC_ID(100)`, `VOLUME_FRACTION`, `MASS_FRACTION_FDS6`, `CL`);
  пропуск FDS6-видов при подсчёте и чтении; захват состава люмп-AIR
  (`AIR_LUMP_*`); `EXTRACT_PRODUCTS_COMPONENTS` + `GET_STANDARD_SPEC_MW` +
  `REAC_PRESCAN_FENIX` (перенесены целиком); слот вида сажи всегда при
  `MIXTURE_FRACTION`.
- `&REAC`: `SPEC_ID_NU`/`NU` — конверсия в mixture fraction (эвристика C/H/O,
  `FUEL_SPEC_*`, `CCC_FIXED_GLOBAL`); выходы продуктов из PRODUCTS (R28j:
  `PRODUCTS_COMP_YIELD`, `SOOT_YIELD` методики, `HCL_YIELD_FENIX`); канал
  OTHER = HCl (`MW_HCl`, `STATE_SPECIES('HYDROGEN CHLORIDE')`); O2-стехиометрия
  из люмп-AIR + замыкание массы через H2O + `PRODUCTS_TRACER_RATIO` с печатью;
  синхронизация подсчёта/заполнения `N_STATE_SPECIES` (SOOT: `SOOT_DEPOSITION
  .OR. SOOT_YIELD`); члены FDS6 (`COMBUSTION_COMPLETENESS`, `CHI_R_REAC`,
  `RADIATIVE_FRACTION`, `HOC_COMPLETE`, `FUEL_C_TO_CO_FRACTION`,
  `FUEL_H_TO_H2_FRACTION`, `FUEL_N_TO_HCN_FRACTION`, `LOWER_OXYGEN_LIMIT`,
  `HCL_YIELD`, `HCN_YIELD`, `CL`); инициализация `CL=0` (фикс #19).
- PROC_SPEC: сажа — свободный вид (`YYMAX(I_PROG_SOOT)=0.1`, ветки Y2Y/MW_AVG/YYY
  по условию `SOOT_DEPOSITION .OR. SOOT_YIELD>0`); `Y2Y` канала OTHER через
  `MF_MW(OTHER_INDEX)`.
- `&ZONE`: точка `XYZ` → малый бокс (совместимость FDS6).
- DEVC/CTRL: пропуск `QUANTITY='TIME'` из CSV; включение `WRITE_DEVC_CTRL`
  (уставка DEVC, любой CTRL); ветка `EXTERNAL` + `EXTERNAL_GATE`; ограниченный
  цикл `INPUT_COUNT: DO NN=1,SIZE(CF%INPUT_ID)` (фикс Fenix+ INPUT_ID(1:40));
  вход `CLOCK` (`CLOCK_INPUT`); гард отсутствия `OBSTRUCTION` в PROC_OBST (MPI).
- SLCF/DEVC/поиск видов: `SEARCH_ENTITY`-грамматика HCl/HF/HBr/HCN; пропуск
  продуктов-трассеров (R28k); поиск derived-видов (`DERIVED_SPEC_OFFSET`);
  отображение стандартных имён FDS6 на индексы mixture fraction с
  предупреждением вместо останова; SLCF с неизвестным видом помечается -999 и
  пропускается; метки SmokeView для derived-видов.

### 2.3. dump.f90 + smvv.f90

- `smoke3d_defs` в smvv.f90: `RLE_COMPRESS` + `SMOKE3D_TO_FILE` (Fortran
  UNFORMATTED, маркеры записей; `.s3d`/`.sz`/`.s3dd`).
- 3 записи `SMOKF3D` на меш (SOOT с MASS_EXTINCTION_COEFFICIENT, HRRPUV с
  коэффициентом 0.0 = FIRE, EFFECTIVE FLAME TEMPERATURE; третья — заголовки без
  кадров, как в доноре); `LU_SMOKE3D(9,*)` через `GET_FILE_NUMBER`.
- Короткие имена файлов (I0): xyz/iso/s3d/sf/bf/bf.sf/prt5/restart/q.
- .smv в формате FDS6: `TERRAINIMAGE`, `FDSVERSION` («FDS5NG R29b»), без секции
  VERSION/ENDF, `CSVF` (hrr/devc/ctrl), `TIMES`, `HRRPUV_MINMAX`,
  `TEMP_MINMAX`, `IBLANK`, `GVEC`, 4 доп. `SURFACE`, `HRRPUVCUT` одним
  значением, `HoC`/`FUEL`, секция `RAMP` удалена, `DEVICE` с QUANTITY, `GRID
  MESH_0000001` + `9I6` с EVAC_CODE, метки `! OBST-<N>` + флаги видимости,
  RGB-заглушки -999 у VENT, `CVENT`.
- CSV: единый `_devc.csv` без лимита колонок; заголовки `Time`/`status`;
  формат вывода `ES15.7E3`+I2; R28i (I0 в форматах заголовков при >=10000
  величин); `_devc_ctrl_log.csv` (OPEN/FLUSH/`WRITE_DEVC_CTRL_LOG`); ctrl.csv.
- Срезы SLCC/SLCF c `# STRUCTURED &`, `CC_VAL`, `IOR_SLICE` (STRING 200).
- PL3D не форсируется на останове (порядок секций для Fenix+, фикс #11);
  `DUMP_CORE` и по `CTRL_STOP`.
- `GAS_PHASE_OUTPUT`/`HVAC_OUTPUT`/`SOLID_PHASE_OUTPUT`: derived-виды PRODUCTS
  (Y через трассер, RCON, MW), гарды `-999`/границ `N_SPECIES`/
  `N_STATE_SPECIES`; H2O из трассера; базовый MEC для derived-сажи.
- Диагностика: полный перечень DEVC (I0, QUANTITY, derived-виды) и PLOT3D;
  времена активации DEVC и CTRL (формат FDS6).

### 2.4. func.f90 / mesh.f90 / main.f90

- func.f90: `SHUTDOWN` с суффиксом `(CHID: …)`; `READ_EXTERNAL_FILE` (парсер
  CSV внешнего управления); `RE_ALLOCATE_STRINGS` 80→200.
- mesh.f90: `EMBEDDED_MESH_FLAG/EMBEDDED_RATIO` в `MESH_TYPE`; `STRING` 200
  (тип и зеркало).
- main.f90: MPI-буферы `WRITE_STRINGS` 80→200 (3 места + декларация); хук
  чтения внешнего управления (`READ_EXTERNAL` + `MPI_BCAST` `EXTERNAL_CTRL`);
  журнал событий DEVC/CTRL в конце шага; `MPI_ALLREDUCE` статуса останова;
  `USE CONTROL_VARIABLES`.

## 3. НЕ перенесено (реестр отложенного)

| Семейство | Причина |
|---|---|
| FLAME_SHEET целиком (включая `BUILD_FS_BURNERS`) | §2.6 родителя — никогда |
| Губернатор BUG#11 / R20 / R22 / R28x (`GOVERN_HRR`, `F_INJECT`, `HRRPUA_MAX` авто-гейт, `DT_TARGET`/`CFL_TARGET`, `DT_GUARD*`, `HRRPUA_SHEET`-логика в PROC_SURF_2, `MLR_BASED_HRR`-пересчёт MLRPUA) | стадия R29d, только целиком |
| Телеметрия (запись `_cfl/_steps/_cpu.csv`, `CONSOLE_DEBUG`, `PLUME_PROBE`, зонды канала/плумы, `CFL_ADV/CFL_DP` вDiagnosticах, FLUSH(LU_ERR) на каждом шаге) | диагностический слой донора; в READ_DUMP члены только читаются |
| SOAP/блендинг/пол давления (`PRES_METHOD`-валидация, рестарт-поля `US/VS/WS/HS/H_PREV/RHOS`, SOAP-счётчики) | вне перечня §2 родителя |
| Авто-зоны R27/R28 (`COUNT_PRESSURE_POCKETS`, `DETECT_AND_SEAL_FIRE_POCKETS`, `ZONE_IS_AUTO`, `H_ZONE_MEAN`, `RELEVEL_AUTO_ZONE_H`) | вне перечня §2 |
| cut-face/THIN (`CUT_FACE`, `XB_INPUT`, `FACE_AREA_FRACTION`, `VOLUME_FACTOR`, ghost-DX снап OBST, mesh-proximity фильтр, `INTERP_OVERLAP_WEIGHT`) | численные усовершенствования донора; требует type.f90 |
| R28l embedded-mesh машина (пропуск/компакция наложений, `FIND_PARENT_MESH`, контроль выравнивания) | глубокая связь с нумерацией мешей; флаг `EMBEDDED` читается, интерполяции нет |
| R28d рестарт-защита (`GEOM_FINGERPRINT`, `CHECK_RESTART_GEOM`, маркер `END_R28M`) | механизм рестарт-цепочек донора |
| ITMP-клампы JANAF (`MAX(0,…)`) | не чтение/вывод; захарденинг физфункций |
| Удаление `.end` при успешном завершении | косметика main.f90 |
| Численные значения донора (CSMAG 0.4, RADIATIVE_FRACTION 0.45, RADI-инкременты, SUPPRESSION-варнинг вместо ошибки, EVAC+, LU_OUTPUT-редиректы предупреждений) | численный выбор донора, не совместимость |

## 4. Приёмка этапа 3 (протокол §5 handoff)

- (а) `0106e472_detectors.fds` (Fenix+ 3.7.1.1): ошибок чтения нет; в журнале
  «INFO: Extracted 6 components from PRODUCTS mixture», блок R28j (SOOT
  7.035E-02; трассер 3.389 кг/кг — совпадает с донором); расчёт идёт
  (остановлен вручную на t=23 с, шаг 90; DT ~0.35–0.46 с).
- (б) `room_fire.fds` (Verification, T_END укоротен до 15 с): «STOP: FDS
  completed successfully», 437 шагов, без правок входа.
- (в) `0106e472.smv` содержит маркеры FDS6: `FDSVERSION`, `CSVF`×3, `TIMES`,
  `HRRPUV_MINMAX`, `TEMP_MINMAX`, `IBLANK`, `GVEC`, `GRID MESH_0000001`,
  `SMOKF3D`×6 (3 на меш), `SLCC`×20, `CVENT`, `HoC`, `FUEL`; секции `RAMP`
  нет. Визуальная проверка SmokeView 6.11+ — за владельцем.
- (г) журнал конверсий: консоль/`run_console.log` (как у донора — фаза чтения
  стирается REPLACE в `INITIALIZE_GLOBAL_DUMPS`); CSV: единый `devc.csv`
  (`Time,"DEVC_1",…`), `hrr.csv` (`FDS_HRR_Time,…,BURN_RATE`), `ctrl.csv`
  (`Time,"CTRL_1",…`), `devc_ctrl_log.csv` с событиями CLOCK-управления.

Каталог приёмки: `Source/test/fds5ng_r29b/` (0106/, roomfire/).

## 5. Состояние среды

- Базовый прогон room_fire (900 с): первый запуск 21:32 оборван внешне на
  522.5 с (останки в `fds5ng_baseline/dead_20260924_2227/`); перезапущен
  22:29:49 (persistent), на 23:09 — 329 с из 900. Использует `fds5ng.exe`
  сборки 21:26 (R29a) — поведение для room_fire идентично.
- Двухранговые mpiexec не применялись; приёмка — однопроцессорные прямые
  запуски через `run_ng_scratch.bat` (setvars, ASCII+CRLF).
- `evac_plus.f90` не трогался; `Source/fds5/` не менялся.

## 6. Незакрытые нити

1. Завершение базового прогона (метрики HRR-плато/DT/шаги/стен-время в акт
   этапа 1) + `copy /y` свежего exe в `fds5ng.exe`.
2. Визуальная приёмка .smv/.s3d в SmokeView 6.11+ (за владельцем).
3. R29c — химия: применение `PRODUCTS_COMP_*` в физике переноса (трассер
   продуктов), HCl-трассировка в fire.f90, Simple Chemistry/SOOT_YIELD,
   MLR/CCC (вместе с R29d).
4. Полный протокол §3 родителя (синтеты synth_710/r28q_synth2 против
   same_but_fds6) — после R29c/R29d.
5. Рестарт-церемония не проверялась (входов с RESTART на этапе 3 не было).

## 7. Промпт для следующей сессии (копировать дословно)

---
Ты работаешь с репозиторием E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds.
Задача: план B, этап 4 — R29c (химия) в чистом дереве Source/fds5ng
(сток 5.5.3 + R29a + R29b: слой чтения/вывода совместимости FDS6/Fenix+ перенесён).

Сначала прочитай:
- Source/fds5ng/ng_docs/HANDOFF_plan_B_fds5_rebase.md (родитель: правила,
  протокол приёмки §3, §2 перечня переноса);
- Source/fds5ng/ng_docs/HANDOFF_plan_B_stage3_R29b.md (этапы 1–2);
- Source/fds5ng/ng_docs/HANDOFF_plan_B_stage3_R29b_report.md (этап 3: что
  перенесено, реестр отложенного §3, состояние среды §5);
- Source/memory/COMPATIBILITY_REPORT.md и MLR_BASED_HRR-документы (справочник
  поведения).

Порядок работ:
(0) Проверить состояние: сборка 23:04 зелёная (fds5_impi_intel_win.exe); если
    базовый прогон room_fire завершён — метрики в акт этапа 1 уже сняты
    сессией-предшественницей (wakeup ~00:35), проверить акт; если fds5ng.exe
    старее fds5_impi_intel_win.exe и не занят — copy /y.
(1) Снять диффы донор→NG: git diff --no-index -- fds5/fire.f90 fds5ng/fire.f90
    (далее mass.f90, wall.f90 — только хунки, необходимые химии). Направление:
    '-' = донор, '+' = база. Каждый хунк классифицировать ДО переноса.
(2) Перенести из fire.f90 ТОЛЬКО химию Simple Chemistry при SPEC_ID_NU-входе:
    - стехиометрия/скорость реакции из REACTION-полей, заполненных R28j
      (NU-вектор, PRODUCTS_TRACER_RATIO, O2_F_RATIO);
    - трассер продуктов PRODUCTS как state-величина переноса (применение
      PRODUCTS_COMP_* в физике, а не только в выводе — вывод R29b уже читает
      derived-виды из трассера);
    - HCl-трассировка как продукта (канал OTHER = HYDROGEN CHLORIDE);
    - SOOT_YIELD из баланса при разложении PRODUCTS (значения R28j уже
      вычислены ридером);
    - COMBUSTION_COMPLETENESS/CCC_FIXED_GLOBAL и MLR_BASED_HRR/CCC-механизм —
      только если они неотделимы от перенесённых хунков; иначе отложить в R29d.
(3) НЕ переносить: FLAME_SHEET и всё под его флагом (в т.ч. RT-демпфер
    velo.f90), губернатор BUG#11/GOVERN_HRR/F_INJECT/HRR_CAP_GLOBAL, R28x
    (DT_TARGET/CFL_TARGET/q_dyn/авто-гейт HRRPUA_MAX), DT_GUARD (R22),
    телеметрию, evac+. Это R29d или «никогда» (§3 отчёта этапа 3).
(4) Compile-gate после каждого файла: cmd /c C:\Users\User\AppData\Local\Temp\kilo\build2025ng.bat
    (ASCII+CRLF, явные пути 2025.3). Ошибки чинить до перехода к следующему
    файлу. Ревизии помечать R29c в комментариях.
(5) Приёмка R29c:
    а) 0106e472_detectors.fds (Source/test/R28q_test/, каталог скретча
       Source/test/fds5ng_r29b/0106/): чтение без ошибок (база этапа 3),
       физика идёт дальше стадии старта: HRR из _hrr.csv начинает расти,
       трассер продуктов переносится (SLCC/DEVC derived-виды ненулевые вне
       источника), HCl-величины осмысленны; фиксировать где останов, если был;
    б) деградация: room_fire.fds считается без правок входа до STOP success;
    в) синтеты этапа химии: synth_710.fds (6.7 МВт/м²) и r28q_synth2.fds
       (60 МВт запертый) — DT без спирали, STOP success; полный протокол §3
       родителя (эталон same_but_fds6) — только если R29d уже не требуется
       для устойчивости, иначе зафиксировать предел.
(6) Запуски — однопроцессорные прямые, через bat с call setvars (шаблон
    C:\Users\User\AppData\Local\Temp\kilo\run_ng_scratch.bat <dir> <file>);
    двухранковые mpiexec на этой машине терминируются внешне.
(7) evac_plus.f90 и Source/fds5/ — только чтение. Коммитов не делать.
    Докладывать по завершении каждой стадии; отчёт — отдельным .md в
    Source/fds5ng/ng_docs/ с промптом для следующей сессии.

Ограничения среды — §4 родителя и §5 отчёта этапа 3.
---
