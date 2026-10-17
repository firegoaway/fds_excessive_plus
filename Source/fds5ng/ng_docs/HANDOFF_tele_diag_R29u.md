# HANDOFF: Телеметрия TELE_DIAG (R29u) — расследование расхождения извещателя c6812929

**Дата:** 2026-10-08 · **Автор:** сессия R29u (продолжение серии FDS6‖FDSE)
**Файлы:** этот handoff + `D:\tmp\danilovo_diag\`, `D:\tmp\tele_val\`, `D:\tmp\soot_repro\`

---

## 1. Большой контекст

Серия параллельных тестов FDS 6.11.1 (оригинал) vs FDS E+ (fds5ng R29t) в
`E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Source\test\R29`. Завершены и закрыты
отчётами (MD+HTML+JSON в папках кейсов):

- **2c155400_detectors**: ядро совпадает (HRR/T/O₂/CO₂/CO/HCl/видимость, конвейер
  KILL/RESTART Δ=0.17 с). Дефициты FDSE: cut-зоны давления (1 зона vs 6 у FDS6,
  локально 100 % расхождение), RELATIVE HUMIDITY=0, WATER VAPOR vf=0,
  рад-фон RAD FLUX GAS +0.53 кВт/м² на всех 168 точках.
- **c6812929_detectors (Danilovo_K1)**: HRR идентичен (94 кВт → 470 кВт синхронно),
  T/O₂/P/видимость совпадают. **КРИТИЧНО: срабатывание дымового извещателя
  PATH OBSCURATION (луч 180 м, z=12.25) — FDS6 t=13.8 с, FDSE t=140.6 с (4.8×).**
  Дым FDS6 достигает луча ~7 с, FDSE ~125 с. При 94 кВт время подъёма факела 10 м
  ≈6 с ⇒ поведение FDS6 физично, FDSE — слишком медленный перенос.

Приоритет правок (утверждён пользователем): №1 сажа/дым при малых HRR → №2 cut-зоны →
№3 H₂O/RH → №4 рад-фон. Текущая работа — диагностика №1.

## 2. Итог 9 изолированных репро (D:\tmp\soot_repro\repro1..9)

Все конфигурации (открытый бокс 5 МВт; герметичный бокс 55 кВт; 8 мешей MPI; полный
масштаб 97×24.5×13 м; одноклеточный пожар 94 кВт; TMPA=38; PATH_LENGTH=21.6; пожар на
гранях OBST; луч через все 8 мешей; ложный потолок 3.73–3.98 с проёмом) —
**расхождение НЕ воспроизводится**: FDSE доставляет дым так же или быстрее FDS6.
Вывод: генерического дефекта транспорта сажи нет; причина в специфике полного кейса
(сотни OBST, CTRL-двери VEL=9.5, INIT, clutter). Слепой перебор остановлен,
по решению пользователя вместо бисекции внедряется телеметрия TELE_DIAG.

## 3. Телеметрия FDSE — ГОТОВО, валидировано

Патч (ревизия R29u, только debug-бинарь, релиз НЕ тронут):
- `cons.f90`: `TELE_DIAG=.FALSE.` в блоке дефолтов MISC (строка ~66, после
  EMBEDDED_MESH-строки); `LU_TELE=60` в строке LU_ERR=0,... 
- `read.f90`: `TELE_DIAG,` в `NAMELIST /MISC/` (строка ~1366, продолжение после
  `CFL_MAX,...,GROUND_LEVEL, &`).
- `dump.f90`: `SUBROUTINE WRITE_TELEMETRY(T)` вставлена ПЕРЕД `SUBROUTINE DUMP_DEVICES`
  (~строка 5273); имя добавлено в PUBLIC-список модуля DUMP (строка ~52,
  `WRITE_DEVC_CTRL_LOG,WRITE_TELEMETRY`).
- `main.f90`: `IF (TELE_DIAG) CALL WRITE_TELEMETRY(T)` после блока диагностики (~956).

Формат CSV (одна строка на шаг, файл `CHID_tele.csv`; MPI: `CHID_tele_pNNN.csv` на ранг):
```
t,dt,hrr_mw,m1_soot_kg,m1_soot_max,m1_ceil_soot,m1_tmax,m1_fluxy,m1_q_mw,...
```
- По мешам 1..min(NMESHES,9): масса сажи (кг), max сажа (кг/м³), сажа в верхних 10 %
  высоты меша, max T (°C, TMAX−TMPM), поток массы через +Y-грань.
- ВАЖНО (известные особенности парсинга):
  - заголовок на меш объявляет 6 колонок (включая mN_q_mw), данных пишется 5 —
    последняя именованная колонка группы всегда пуста; парсер берёт группы по 5;
  - `hrr_mw` — интеграл Q только по мешам СВОЕГО ранга (PROCESS(NM)/=MYID CYCLE);
    глобальный HRR = сумма по всем rank-файлам, либо брать из `CHID_hrr.csv`;
  - сажа: `YY_GET(1:N_SPECIES)=YY(I,J,K,1:N_SPECIES)` + `GET_MASS_FRACTION(YY_GET,
    SOOT_INDEX,Y_SOOT)`; при отсутствии трекаемого слота (simple-химия) — нули.

Бинарь: `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_db\
fds5_impi_intel_win_db.exe` (08:43, ~28 МБ, /Od /check:all).
Сборка: `cmd /c D:\tmp\build_ng_db.cmd` (makefile5ng, VPATH=Source/fds5ng,
цель fds5_impi_intel_win_db, каталог impi_intel_win_db).

Валидация (D:\tmp\tele_val, room_fire): TELE on/off — **бит-в-бит идентичные
devc+hrr**; 440 шагов телеметрии без сбоев; HRR 14.4 МВт и T_max 411 °C соответствуют
физике. Отличия devc от релизного R29t-бинаря — только последний знак отдельных чисел
(эффект /Od vs /O2), патч ни при чём (доказано паритетом on/off).

Грабли, уже закрытые (не повторять):
- WRITE_TELEMETRY обязана быть в PUBLIC модуля DUMP (там PRIVATE по умолчанию) — иначе LNK2019.
- Unit 31 занят DUMP_BNDF ⇒ severe(256) на первом BNDF-дампе. LU_TELE=60.
- YY имеет 4-ю размерность N_SPECIES, а НЕ N_Y_ARRAY: читать только 1:N_SPECIES,
  индекс — SOOT_INDEX (species-индекс), не I_PROG_SOOT (может превышать N_SPECIES).

## 4. Телеметрия FDS6 — реализована, СТАТУС ПЕРЕСБОРКИ НЕ ПРОВЕРЕН

Патч исходников `E:\FIREGOAWAY\GitHub\FDS6.11.1\Source` (по явному разрешению
пользователя; оригинальный бинарь build_nmk\fds.exe не тронут):
- `cons.f90`: `LOGICAL :: TELE_DIAG=.FALSE.` (после SUPPRESS_DIAGNOSTICS, ~248) +
  `LU_TELE=60` (~574).
- `read.f90`: `TELE_DIAG,&` в NAMELIST /MISC/ (после GRAVITATIONAL_DEPOSITION, ~1743).
- `dump.f90`: WRITE_TELEMETRY перед WRITE_DIAGNOSTICS (~3976); в PUBLIC
  (`...,WRITE_DIAGNOSTICS,WRITE_TELEMETRY, &`).
- `main.f90`: `IF (TELE_DIAG) CALL WRITE_TELEMETRY(T,DT)` после блока WRITE_DIAGNOSTICS (~1057).

Отличия API от FDSE: FDS6 — `CELL(CELL_INDEX(I,J,K))%SOLID` (не SOLID()),
ZZ(1:N_TRACKED_SPECIES), MY_RANK/N_MPI_PROCESSES, POINT_TO_MESH(NM) тот же.
**HRR: поле Q в FDS6 имеет другую калибровку — интеграл по ячейкам дал 31 МВт вместо
94 кВт. Исправлено на `Q_MW = 0.001_EB*Q_DOT_SUM(1)` (модуль DATA, заполняется
DUMP_GLOBAL_OUTPUTS, который в цикле идёт РАНЬШЕ вызова телеметрии).**
Пересборка с этим фиксом запущена (лог `/d/tmp/build_fds_tele6.log`) и завершилась
ровно на лимите итераций сессии — **EXIT не проверен, это пункт №1 следующей сессии.**

Бинарь: `E:\FIREGOAWAY\GitHub\FDS6.11.1\build_tele\fds.exe` (122 МБ; в каталог
скопированы impi.dll, libifcoremd.dll, libiomp5md.dll из build_nmk — без них из MSYS
возвращается 127). Сборка: `cmd /c D:\tmp\build_fds_tele3.cmd` — cmake из
`D:\.cargo\vcpkg\downloads\tools\cmake-3.30.1-windows\cmake-3.30.1-windows-i386\bin`,
генератор "NMake Makefiles", Release, ifx 2025.3,
`-DUSE_SUNDIALS=OFF -DFETCHCONTENT_SOURCE_DIR_HYPRE=<build_nmk/_deps/hypre-src>`
(оригинал собран с USE_SUNDIALS=OFF; с ON sundials-объект падает на unistd.h).

Первая валидация (r4tele = repro4 + TELE_DIAG, в D:\tmp\tele_val): телеметрия пишется
штатно, т/dt корректны; HRR был 31 МВт (баг выше) — после фикса ожидается ~0.094 МВт.
ВАЖНО: `room_fire.fds` содержит `&MISC SURF_DEFAULT=` — FDS6 отвергает с ERROR(101)
даже оригинальным бинарем; для FDS6-валидаций использовать repro-файлы или diag-копию.

## 5. Диагностическая копия кейса (готова)

`D:\tmp\danilovo_diag\c6812929_diag.fds` = оригинал c6812929_detectors.fds +
`&MISC ... TELE_DIAG=.TRUE.` + нативные зонды:
- вертикальная башня над очагом (259.25, −20.5): W-VELOCITY / TEMPERATURE /
  DENSITY SPEC_ID='SOOT' на z = 1 / 4.5 / 8 / 12.25 м (probe_W/T/SOOT_*);
- SLCF DENSITY SOOT PBX=259.25 (через все 8 мешей) и PBZ=12.25 (потолочный,
  в плоскости луча), SLCF W-VELOCITY PBX=259.25.
Геометрия: пожар x=258–260.5, y=−26..−15, z=0–2 (меш 7: y=−35..−11); потолок 13 м;
луч DEVC_253 z=12.25; меша — Y-полосы 97×24.5×13, IJK≈194×48×26, DX=0.5.

## 6. Правила запуска (обязательны)

- Машина: 6 ядер / 12 логических, 16 ГБ RAM. nproc в MSYS врёт (говорит 4).
- Экспорт: `I_MPI_FABRICS=shm`, `OMP_NUM_THREADS=1`, PATH с oneAPI mpi/compiler/mkl.
- FDS6 требует рангов ≥ max(MPI_PROCESS)+1 (для c6812929 — 8). Дефолт 8×4OMP=32
  потока вешает радиационный MPI-обмен (ERROR 123) — лечится shm+OMP=1.
- Большие прогоны ТОЛЬКО последовательно: два 8-ранговых задания параллельно = OOM
  (~800 МБ свободной).
- FDS6 ≈7× медленнее FDSE (на 8 рангах): детекторный кейс FDSE ~2 ч, FDS6 — вечер.
- После прогонов убивать остатки: `taskkill /F /IM fds.exe` и
  `taskkill /F /IM fds5_impi_intel_win.exe` (в MSYS — слэши /F /IM, не //F).

## 7. План следующей сессии (по порядку)

1. Проверить `/d/tmp/build_fds_tele6.log` (строка EXIT=). Если не 0 — пересобрать
   `cmd /c D:\tmp\build_fds_tele3.cmd` (инкрементально, только dump/main).
2. Валидация HRR-фикса: в `D:\tmp\tele_val` прогнать `r4tele.fds` на
   build_tele\fds.exe ⇒ в `r4tele_tele.csv` колонка hrr_mw должна быть ≈0.094
   (плато, сравнить с r4tele_hrr.csv). Заодно проверить, что soot-колонки >0
   (в repro4 soot есть; если 0 — проверить SOOT_INDEX>N_TRACKED_SPECIES и при
   необходимости брать derived soot как в CASE(155) PATH OBSCURATION: PY%Y_INDEX).
3. Основной прогон: из `D:\tmp\danilovo_diag\` сделать два подкаталога run/fdse и
   run/fds6 с копией c6812929_diag.fds; запустить ПОСЛЕДОВАТЕЛЬНО:
   - FDSE: `mpiexec -n 8 ...impi_intel_win_db\fds5_impi_intel_win_db.exe` (~2 ч);
      产物 per-rank `c6812929_diag_tele_p000..p007.csv`;
   - FDS6: `mpiexec -n 8 ...FDS6.11.1\build_tele\fds.exe` (вечер; KILL ожидается
     на t≈34 с — этого достаточно, расхождение развивается на t=5–30).
4. Сравнение телеметрий (написать Python-скрипт, сохранить в danilovo_diag):
   слить rank-файлы по мешам (mesh↔rank из .out), выровнять по t (интерполяция на
   общую сетку 0.5 с), для каждой величины посчитать |Δ(t)|/max(|v|,eps) и скорость
   роста расхождения; рейтинг «что ушло первым»:
   - m7_soot_* расходится первым ⇒ инжекция сажи (fire.f90 MLR-ветка);
   - probe_W/T над очагом (из devc) ⇒ подъём факела/buoyancy;
   - m7→m6→m5 потолочная сажа/fluxy ⇒ потолочная струя через mesh-границы;
   - всё расходится равномерно с шага 1 ⇒ сравнивать профили, думать о FDS6-артефакте.
5. По результатам — доклад пользователю и точечная правка fds5ng (релизный бинарь
   по-прежнему не трогать без решения; правка — в Source/fds5ng с ревизией R29u+,
   сборка релиза только после одобрения).
6. Опционально после п.4: та же методика на 2c155400 (cut-зоны, дефицит №2).

## 8. Сводка артефактов

| Что | Где |
|---|---|
| Отчёты тестов 1–2 | test\R29\{2c155400_detectors,c6812929_detectors}\report_*.md/.html + compare_data.json |
| Репро 1–9 | D:\tmp\soot_repro\repro[1-9].fds + fds6_N/fdse_N |
| Валидация телеметрии FDSE | D:\tmp\tele_val\{off,on}.fds, on_tele.csv, room_fire-эталон D:\tmp\r29t_reg |
| Валидация телеметрии FDS6 | D:\tmp\tele_val\r4tele.fds, r4.log, r4tele_tele.csv |
| Диагностический кейс | D:\tmp\danilovo_diag\c6812929_diag.fds |
| Debug-бинарь FDSE (телеметрия) | Build\impi_intel_win_db\fds5_impi_intel_win_db.exe (08:43) |
| Телеметрический бинарь FDS6 | E:\FIREGOAWAY\GitHub\FDS6.11.1\build_tele\fds.exe + dll |
| Скрипты сборки | D:\tmp\build_ng_db.cmd, D:\tmp\build_fds_tele3.cmd |
| Логи сборок | /d/tmp/build_ng_db*.log, /d/tmp/build_fds_tele*.log (№6 — НЕ ПРОВЕРЕН) |

Релизные бинари не изменялись: `Build\impi_intel_win_ng\fds5_impi_intel_win.exe`
(R29t, чистый) и `FDS6.11.1\build_nmk\fds.exe` (оригинал).
