# HANDOFF: план B, этап 4 — R29c завершён (химия Simple Chemistry)

Версия: 1.0
Дата: 2026-09-25, 00:05 MSK
Статус: этап 4 выполнен; приёмка (а)(б)(в) пройдена.
Родительские документы: `HANDOFF_plan_B_fds5_rebase.md`, `HANDOFF_plan_B_stage3_R29b.md`,
`HANDOFF_plan_B_stage3_R29b_report.md`.

## 1. Сборка

- Compile-gate и полная сборка: 23:23:09 (`fds5_impi_intel_win.exe`, каталог
  `Build/impi_intel_win_ng/`), ошибок компиляции нет.
- `fds5ng.exe` (21:26, R29a) занят базовым прогоном room_fire (PID 6340,
  перезапуск 22:29:49); `copy /y` выполнит контроль финиша (wakeup ~00:45,
  см. §5).

## 2. Перенесено (тег R29c)

### 2.1. fire.f90 — единственный физический хунк этапа

В `COMBUSTION_MF` обе ветки обновления продуктов (CO_PRODUCTION и обычная):
условие `IF (SOOT_DEPOSITION)` заменено на `IF (SOOT_INDEX > 0 .AND.
I_PROG_SOOT > 0)` с расщеплением `DYF*(1-SOOT_YIELD)` в трассер продуктов и
`DYF*SOOT_YIELD` в слот свободной сажи. Обоснование: ридер R29b выделяет слот
сажи (переносимый скаляр, `Y2Y(SOOT_INDEX,I_Z_MAX)=Y_F_INLET`) при
`SOOT_DEPOSITION .OR. SOOT_YIELD>0`; без хунка слот оставался нулевым (сажа,
видимость, дым — пустые), а продукты получали всю массу DYF. Форма хунка
эквивалентна донорской (донор в ветке без слота добавляет `(1-Ys)`, затем `Ys`
в тот же трассер — сумма равна DYF, как в стоке).

### 2.2. Почему больше ничего перенесено не было

Диффы донор→NG сняты для fire.f90 (945 строк), mass.f90, wall.f90; по ключевым
словам химии (`SOOT|I_PROG|OTHER_INDEX|Y2Y|HCL|MIX_TIME|NU(|REACTION|SPEC`)
просканированы func/init/radi/turb/divg/pres/hvac/part/prec/velo/type/scrc/vege.
Итог: вся химическая машинерия SPEC_ID_NU уже перенесена этапами 2–3 в ридер
(NU-вектор, O2_F_RATIO, Y_F_INLET, SOOT_YIELD из баланса R28j, трассер
PRODUCTS как Z2 с `FUEL_FRAC=1/(1-SOOT_YIELD)`, канал OTHER = HYDROGEN
CHLORIDE через Y2Y, свойства state-видов, derived-виды в dump). Химия в
физическом слое сводится к заполнению слота сажи — см. §2.1.
`PRODUCTS_TRACER_RATIO`/`PRODUCTS_COMP_*` у донора используются только в
read.f90 и dump.f90 (проверено грепом) — обе стороны уже в NG.

## 3. НЕ перенесено (реестр отложенного, дополнение к §3 отчёта этапа 3)

| Семейство (файл донора) | Причина |
|---|---|
| FLAME_SHEET: `COMBUSTION_FLAME_SHEET` целиком, баннер R26, `FS_*` | §2.6 родителя — никогда |
| Губернатор BUG#11: `F_GOVERN`, `DZ_CAP`, `HRR_CAP_GLOBAL` (fire.f90) | R29d, только целиком |
| R20 unmixedness: `ZETA_STORE`, `R20_EDC_IF` (fire.f90) | R29d |
| R28x: `Q_DYN`/`D_TARGET` в Q_BOUND_2 и в CONVERT_CO (`Q_DYN_REM`), `F_GOVERN`-множители (fire.f90) | R29d |
| MLR/CCC: `IF (SUPPRESSION .AND. CCC_FIXED_GLOBAL < 0)` — обход подавления в MLR-режиме (fire.f90) | R29d (вместе с MLR_BASED_HRR по §6.3 отчёта этапа 3); сток-условие `IF (SUPPRESSION)` сохранено |
| TAU_G-ограничители MIX_TIME (`LES_MIX_TIME_CAP`, `NONLES_MIX_TIME_CAP`) и безусловный расчёт DELTA вне LES_IF (fire.f90) | численный выбор донора (EDC), вне перечня §2 родителя; закр. в реестр |
| MIX_TIME-кап 1.0 с для не-LES (комментарий донора) | то же |
| wall.f90: `GOVERN_HRR`-умножение на `F_INJECT` | R29d |
| wall.f90, part.f90, vege.f90: ITMP-клампы `MAX(0, MIN(...))` (JANAF) | реестр этапа 3 (захарденинг физфункций) |
| mass.f90 | идентичен донору (diff пуст), перенос не требуется |
| init.f90 `SCALAR_SAVE*` (0:N_SPECIES), turb/divg/velo ITMP-хунки | не химия; вне перечня |

## 4. Приёмка R29c (каталог `Source/test/fds5ng_r29c/`)

Все запуски — однопроцессорные прямые через `run_ng_scratch.bat`
(`fds5_impi_intel_win.exe` сборки 23:23, R29c).

### (а) 0106e472_detectors.fds (Fenix+ 3.7.1.1)

- Чтение без ошибок; журнал: «INFO: Extracted 6 components from PRODUCTS
  mixture»; блок R28j: SOOT 7.035E-02, трассер продуктов 3.389 кг/кг,
  O2_F_RATIO 2.389 — значения идентичны донору и этапу 3.
- Физика: HRR монотонно растёт 0 → 386 кВт к t=48 с; BURN_RATE 1.2E-02 кг/с.
- Трассер продуктов переносится (последний кадр срезов меша 1, t≈47 с):
  CO2-плотность max 2.06E-03 кг/м³, CO max 3.10E-04 кг/м³ у очага.
- Сажа/видимость (прямой эффект R29c): VISIBILITY min 1.85 м (max 30) в срезе
  SOOT VISIBILITY; на этапе 3 слот сажи оставался нулевым.
- HCl-величины осмысленны: HYDROGEN CHLORIDE DENSITY max 4.36E-05 кг/м³
  (≈36 ppm в раннем дыме) в срезе; удалённые DEVC (270 шт., z=4.7 м) на
  t=48 с ещё чистые — дым до них не дошёл.
- Останов: t=48.54 с, ШТАТНЫЙ — цепочка CTRL входа DEVC_4053 (71.2 °C) →
  DetectorsTrigger → DetectorKill (KILL). Фиксация: физика дошла дальше
  стадии старта и дальше этапа 3 (23 с); сбоя нет.

### (б) room_fire.fds (T_END=15 с, без правок входа)

«STOP: FDS completed successfully», 437 шагов — шаг-в-шаг совпадает с этапом 3
(REAC входа без SOOT_YIELD: слот сажи не выделяется, сток-путь). HRR в конце
11.0 кВт — ранний участок разгона (базовый прогон 900 с выходит на плато
≈3.8 МВт, см. акт этапа 1).

### (в) Синтеты

- synth_710.fds (6.7 МВт/м², T_END=710): «STOP: FDS completed successfully»,
  8723 шага; DT min 0.0438 с, медиана 0.0702 с, max 0.4564 с — спирали нет,
  критерий DT>=0.03 выдержан на всём интервале. Предел до R29d (зафиксировать):
  EDC+Q_UPPER (локальный предел 2500 кВт/м³ стока) на сетке 0.5 м выдаёт
  HRR 2.5–4.5 МВт с осцилляциями против предписанных 6.67 МВт на плато
  (RAMP t² через TAU_Q=-500). Полный протокол §3 родителя (эталон
  same_but_fds6, допуски по HRR/плотности дыма) — после R29d.
- r28q_synth2.fds (CHID r28q_synth, запертый объём 60 МВт, T_END=60):
  «STOP: FDS completed successfully», 631 шаг; DT min 0.0140 с, медиана
  0.0724 с — спирали нет. HRR ~2.3–3.6 МВт: кислородное голодание запертого
  объёма при штатном SUPPRESSION стока — ожидаемое поведение EDC без
  губернатора.

## 5. Состояние среды

- Базовый прогон room_fire (900 с) ЗАВЕРШЁН в 01:07 MSK: «STOP: FDS completed
  successfully», 62 542 шага, стен-время 9 429 с, DT min 0.00797/медиана
  0.0132 с, HRR t>=800 с: 2.77–4.62 МВт, среднее 3.76 МВт. Метрики — в акте
  этапа 1 (`ng_docs/HANDOFF_plan_B_stage1_baseline_act.md`). Нить 1 закрыта.
- 01:20 MSK, дополнение: обнаружена параллельная работа следующей сессии по
  R29d (правки type/read/main/wall/fire 23:46–23:53, её сборка-гейт 23:58:26).
  `copy /y` в `fds5ng.exe` задеплоил её сборку (не R29c 23:23 — та не
  сохранена). Контрольный прогон её exe на room_fire: STOP success, 446 шагов.
- Остаточных процессов приёмки нет; каталоги прогонов этапа 4 сохранены
  (`Source/test/fds5ng_r29c/`: 0106/, synth710/, r28qsynth2/ — нетронуты;
  roomfire/ перезаписан контрольным прогоном 01:20 сборки R29d — цифры
  приёмки R29c (437 шагов) сохранены только в тексте §4(б)).
- `evac_plus.f90` и `Source/fds5/` не менялись; коммитов нет.

## 6. Незакрытые нити

1. ЗАКРЫТО 01:07–01:20 MSK: базовый прогон завершён, метрики в акте этапа 1;
   `fds5ng.exe` обновлён (сборкой R29d-в-работе параллельной сессии, §5).
2. Визуальная приёмка .smv/.s3d в SmokeView 6.11+ — за владельцем
   (нить 2 этапа 3).
3. R29d — предохранители одним пакетом: GOVERN_HRR+F_INJECT+DZ_CAP (BUG#11),
   R20-ZETA, R28x (DT_TARGET/CFL_TARGET/q_dyn, авто-гейт HRRPUA_MAX в
   PROC_SURF_2), DT_GUARD (R22), MLR_BASED_HRR/CCC_FIXED_GLOBAL (+обход
   SUPPRESSION, +MLR-поля type.f90/init.f90/dump.f90). Перенос только целиком
   с повтором синтетов.
4. Полный протокол §3 родителя против эталона same_but_fds6 (b7896c0a) —
   после R29d; тест B (срезы скорости, компаньон b7896c0a_v) — там же.
5. TAU_G-ограничители MIX_TIME и безусловный DELTA (реестр §3) — кандидат в
   R29e при устойчивостных проблемах протокола §3.
6. Рестарт-церемония не проверялась (входов с RESTART не было; нить 5 этапа 3).

## 7. Промпт для следующей сессии (копировать дословно)

---
Ты работаешь с репозиторием E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds.
Задача: план B, этап 5 — R29d (предохранители и MLR/CCC) в чистом дереве
Source/fds5ng (сток 5.5.3 + R29a + R29b + R29c: совместимость чтения/вывода
и химия Simple Chemistry перенесены).

Сначала прочитай:
- Source/fds5ng/ng_docs/HANDOFF_plan_B_fds5_rebase.md (родитель: правила,
  протокол приёмки §3, перечень переноса §2);
- Source/fds5ng/ng_docs/HANDOFF_plan_B_stage3_R29b_report.md (этапы 1–3);
- Source/fds5ng/ng_docs/HANDOFF_plan_B_stage4_R29c_report.md (этап 4: реестр
  отложенного §3 — он и есть план R29d; состояние среды §5);
- Source/memory/FDS5_MLR_BASED_HRR_AND_CCC_IMPLEMENTATION.md и
  MLR_BASED_HRR_GUIDE.md (справочник MLR/CCC);
- Source/memory/COMPATIBILITY_REPORT.md (разделы 13–15).

Порядок работ:
(0) Проверить состояние: если fds5ng.exe старее fds5_impi_intel_win.exe и не
    занят — copy /y (иначе базовый прогон ещё держит; метрики акта этапа 1
    должна была снять сессия-предшественница по wakeup ~00:45 — проверить
    Source/test/fds5ng_baseline/room_fire.out на «completed successfully»).
(1) R29d переносится ТОЛЬКО ЦЕЛИКОМ, одним пакетом, с повтором всех
    синтетов после каждого файла:
    - GOVERN_HRR + F_INJECT (wall.f90:~464, main.f90 расчёт F_GOVERN по
      HRR_GLOBAL_PREV/HRR_CAP_GLOBAL) + DZ_CAP=0.25;
    - R20-ZETA (ZETA_STORE в fire.f90);
    - R28x: DT_TARGET/CFL_TARGET/q_dyn в COMBUSTION_MF (Q_BOUND_2) и
      CONVERT_CO (Q_DYN_REM), авто-гейт HRRPUA_MAX по эффективным HRRPUA>3
      МВт/м² с учётом рампы (read.f90 PROC_SURF_2 донора);
    - DT_GUARD (R22);
    - MLR_BASED_HRR/CCC_FIXED_GLOBAL: MLRPUA=HRRPUA/(Hc*CCC) в PROC_SURF_2,
      обход MIX_TIME/Q_UPPER/SUPPRESSION в fire.f90, поля type.f90/init.f90,
      MLR_TOTAL в hrr.csv (dump.f90).
    Диффы: git diff --no-index -- fds5/<файл> fds5ng/<файл> ('-'=донор, '+'=база),
    классификация каждого хунка ДО переноса; конс.f90-декларации этапа 2 уже
    объявлены (GOVERN_HRR, MLR_BASED_HRR, CCC_FIXED_GLOBAL, DT_TARGET,
    CFL_TARGET, HRR_CAP_GLOBAL и т.д. — проверить полноту по хункам).
(2) НЕ переносить: FLAME_SHEET и всё под его флагом (никогда); TAU_G-капы
    MIX_TIME и безусловный DELTA (fire.f90, реестр §3 отчёта R29c — кандидат
    R29e); ITMP-клампы; телеметрию; SOAP/блендинг; авто-зоны; cut-face;
    evac+.
(3) Compile-gate после каждого файла: cmd /c C:\Users\User\AppData\Local\Temp\kilo\build2025ng.bat
    (ASCII+CRLF, явные пути 2025.3). Ревизии помечать R29d.
(4) Приёмка R29d:
    а) повтор (а)(б)(в) этапа 4 в новых скретчах Source/test/fds5ng_r29d/
       (0106e472_detectors, room_fire 15 с, synth_710, r28q_synth2);
    б) synth_710: с губернатором R28x донора ожидание — HRR ближе к 6.67 МВт
       плато и DT>=0.03; зафиксировать HRR-тренд по реперным точкам
       (50/100/200/.../700 с) и DT min/median;
    в) полный протокол §3 родителя против эталона same_but_fds6 (CHID
       b7896c0a, FDS-6.11.1): (а) тренд max_dens из CHID_1_1.s3d.sz, допуск
       ±50 %; (б) HRR из _hrr.csv, допуск 10–15 % на росте; (в) DT без
       спирали до 710 с; (д) DEVC 1.7 м по совпадающим ID. Эталонные файлы —
       в test/R28q_test/ и каталогах эталона (см. родителя §3).
(5) Запуски — однопроцессорные прямые через bat с setvars (шаблон
    C:\Users\User\AppData\Local\Temp\kilo\run_ng_scratch.bat <dir> <file>);
    двухранковые mpiexec терминируются внешне.
(6) evac_plus.f90 и Source/fds5/ — только чтение. Коммитов не делать.
    Отчёт — отдельным .md в Source/fds5ng/ng_docs/ с промптом следующей
    сессии.

Ограничения среды — §4 родителя и §5 отчёта R29c.
---
