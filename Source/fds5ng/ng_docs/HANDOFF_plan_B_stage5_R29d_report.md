# Отчёт этапа 5 (R29d): предохранители + MLR/CCC — план B

Дата: 25.09.2026 (сессия ночная 23:39–00:45 + утренняя 09:59–…)
Ревизия кода: R29d. Сборка: зелёная, 23:55:52 (Build/impi_intel_win_ng/fds5_impi_intel_win.exe, 7 709 696 Б); копия fds5ng.exe обновлена после финиша базового прогона (00:0x, отложенное copy /y).

## 1. Перенесённые хунки (донор fds5 → fds5ng)

Пакет R29d перенесён целиком, тег R29d во всех файлах:

| Файл | Хунки |
|---|---|
| cons.f90 | Глобалы: HRRPUA_MAX, GOVERN_HRR, HRR_CAP_GLOBAL, HRR_GLOBAL_PREV, MLR_GLOBAL_PREV, GOV_HOC, F_INJECT (BUG#11); DT_GUARD/DT_GUARD_MASS/DT_GUARD_MAX/FUEL_UNBURNED (R22); DT_TARGET=-1/CFL_TARGET=0.8 (R28x); init HRRPUA_SHEET=-1 (сентинел для авто-гейта). Проверка полноты деклараций этапа 2 выявила, что фактически были объявлены только MLR_BASED_HRR/CCC_FIXED_GLOBAL — остальное добавлено сейчас. |
| type.f90 | SURFACE_TYPE: MLR_BASED_HRR=.FALSE. |
| read.f90 | &MISC: +DT_TARGET,CFL_TARGET,DT_GUARD,DT_GUARD_MASS,DT_GUARD_MAX; дефолты (DT_TARGET=-1, CFL_TARGET=0.8, DT_GUARD=.FALSE., DT_GUARD_MASS=2, DT_GUARD_MAX=0.01); SET_REAC_DEFAULTS: гарды `.AND. HRRPUA_SHEET<0` (иначе авто-гейт 200 кВт/м² нельзя отключить через &REAC/&MISC — выявлено сличением с базой 5.5.3); PROC_SURF_2 BURNING_IF: расчёт MLRPUA из HRRPUA (фикс. CCC=HRRPUA/(Hc*CCC) / процедурная=HRRPUA/Hc, баннер в LU_OUTPUT), пропуск ADJUST_BURN_RATE для MLR-режима, аккумулятор HRRPUA_MAX с учётом максимума RAMP; конец PROC_SURF_2: авто-гейт (GOVERN_HRR=.TRUE., GOV_HOC, HRRPUA_SHEET=200000 при <=0, авто DT_TARGET=0.05 при HRRPUA_MAX>3 МВт/м² с NOTE). |
| main.f90 | Декларации HRR_RANK_SUM/MLR_RANK_SUM/HRR_GOV_BUF/HRR_GOV_OUT, T_MIN_PREV; губернаторский блок после VELOCITY_BC_LOOP_2: MPI-редукция HRR/MLR, HRR_CAP=1.15*MLR*Hc, rate-limit 1x/с, интегратор FUEL_UNBURNED (кап 2*DT_GUARD_MASS), DT_GUARD-кламп DT_NEXT, F_INJECT=1/(1+FUEL_UNBURNED/DT_GUARD_MASS) только при DT_TARGET>0. |
| wall.f90 | SUM_MASSFLUX_LOOP: `IF (GOVERN_HRR) MASSFLUX=MASSFLUX*F_INJECT` перед MASSFLUX_ACTUAL. ITMP-клампы губернатора/стенки НЕ перенесены (реестр). |
| fire.f90 | Модуль: F_GOVERN, DZ_CAP, ZETA_MESH_TYPE+ZETA_STORE (R20). COMBUSTION: F_GOVERN=MIN(1,HRR_CAP_GLOBAL/max(HRR_GLOBAL_PREV,1e-10)), DZ_CAP=0.25 при GOVERN_HRR. COMBUSTION_MF: ленивая аллокация ZETA-хранилища; `IF_SUPPRESSION: IF (SUPPRESSION .AND. CCC_FIXED_GLOBAL < 0)` (MLR-обход, сток-условие сохранено); R20_EDC_IF (ZETA-ветка ZMIX/BURN_F, ZETA_STORE(NM)%Z обновляется) вокруг NEW_DYF_IF; MIN(DZ_CAP,...) в обеих ветках DYF и в DYCO; R28x Q_DYN в Q_BOUND_2 (D_TARGET=MAX(0.1,CFL_TARGET/DT_TARGET-|u|/d), q_dyn=D_TARGET*rho*cp*T) и CONVERT_CO (Q_DYN_REM с тем же бюджетом); Q_NEW=MIN(QB1,QB2)*F_GOVERN; OMP PRIVATE дополнены (ZMIX,BURN_F,CP,Q_DYN,UVW_ADV,D_TARGET,Q_DYN_REM,YY_GET). |
| dump.f90, init.f90, velo.f90 | R29d-хунков нет (MLR_TOTAL в hrr.csv и MASSFLUX_ACTUAL-аккумуляция уже идентичны донору; диффы — только пробельные/вне перечня). Проверено полными диффами. |

Не перенесено (реестр §3 отчёта R29c, без изменений): FLAME_SHEET-машина (вся), TAU_G-капы MIX_TIME + безусловный DELTA вне LES_IF (fire.f90), ITMP-клампы (wall/init/dump), cut-face/SOAP/RELAXED_ALIGNMENT-семейство, телеметрия донора (STEPS_FILE/CFL_FILE/CPU_FILE, PLUME_PROBE), EMBEDDED_MESH_EXCHANGE, DZ_CAP2/Q_BOUND_3-запаска, CFL_DP_SCALE.

## 2. Состояние среды на момент передачи

- Рабочая сборка: `Build/impi_intel_win_ng/fds5ng.exe` = R29d (обновлена после снятия блокировки базовым прогоном).
- Все fds5-процессы завершены (проверено tasklist).
- Скретчи этапа: `Source/test/fds5ng_r29d/{0106,roomfire,synth710,r28qsynth2,mirror_b789}`.
- Компиляция: только remark #8291 (ширина дескрипторов, унаследовано). Ошибок нет.

## 3. Приёмка (а)(б)(в) этапа 4

- **(а) 0106e472 (Fenix+, SPEC_ID_NU)**: чтение без ошибок; баннер `MLR_BASED_HRR: Surface SURF_171 MLRPUA = 3.053E+01 kg/s/m2, CCC = procedural(T,O2)`; R28j-блок идентичен (SOOT 7.035E-02, трассер 3.389, O2_F_RATIO 2.389). Останов t=46.94 с детекторной цепочкой DEVC_4053→CTRL_3→DetectorsTrigger→DetectorKill (R29c: 48.54 с) — штатный. HRR 359 кВт на 46 с (R29c: 386 на 48). Трассер переносится: CO2 1.95E-3, CO 2.93E-4, HCl 4.12E-5 кг/м³; VISIBILITY min 1.96 м (R29c: 1.85). Стен-время 385 с.
- **(б) room_fire 15 с**: STOP success, 446 шагов (R29c: 437; +2 % — эффект губернатора/MLR, ожидаем: физика изменилась планомерно). Баннер MLRPUA=5.251E-02 (процедурная CCC).
- **(в-1) synth_710 (6.7 МВт/м²)**: STOP success, 8720 шагов (R29c: 8723). **DT min 0.0573, p10 0.0625, медиана 0.0683, max 0.46 — критерий DT>=0.03 выполнен, спирали нет** (R29c: min 0.0438). Авто-гейт R28x сработал: `NOTE(R28x): dynamic Q limit enabled: DT_TARGET=0.05 s, CFL_TARGET=0.80 (auto, HRRPUA_MAX=6.667E+06 W/m2)`.
- **(в-2) r28q_synth2 (60 МВт, запертый)**: STOP success, 627 шагов (R29c: 631), DT min ≈0.010/медиана 0.070 по консольным интервалам (грубая оценка; R29c: 0.0140) — спирали нет.

## 4. ГЛАВНОЕ ОТКЛОНЕНИЕ: synth_710 HRR не вышел на плато

Ожидание хендофа («HRR ближе к 6.67 МВт») НЕ подтверждено: HRR 2.2–3.9 МВт (t=500: 2.22; t=600: 3.43; t=710: 3.49 МВт) против донора 6.40/6.66/0.27 МВт (прогон донора `test/R28q_test/synth_710_hrr.csv`; поздний коллапс донора на 700 с — его собственный артефакт, не реплицируем).

**Диагноз (по данным, не гипотеза):** HRR/BURN_RATE = Hc на всём плато (3.43e3/0.078 = 44 МДж/кг) — всё впрыснутое топливо сгорает полностью; лимитирует ВПРЫСК: BURN_RATE 0.078 кг/с при предписанных 0.1513 → F_INJECT ≈ 0.52 → FUEL_UNBURNED ≈ DT_GUARD_MASS (равновесие P-регулятора). Причина накопления запаса: ZETA-дроссель R20 затянут, т.к. MIX_TIME в стоке FDS5 не ограничен сверху на спокойных ячейках (молекулярная MU: MIX_TIME ~ 800 с для ячейки 0.5 м) → ZMIX=ZETA*exp(-DT/огромное)≈ZETA → BURN_F≈0 на старте → запас растёт → F_INJECT режет впрыск до половины. У донора TAU_G-кап MIX_TIME (sqrt(2Δ/g)≈0.32 с) гарантирует быстрый распад ZETA.

**Следствие для реестра:** позиция «TAU_G-капы MIX_TIME + безусловный DELTA вне LES_IF» (кандидат R29e «при устойчивостных проблемах») — **обязательная для работы губернатора**, а не опциональная: без неё R20-ZETA+R28x-регулятор сам ограничивает HRR до ~50 % предписанного на сценариях с резким розжигом. Рекомендация: R29e = TAU_G-капы + безусловный DELA в том виде, как у донора (fire.f90, LES_MIX_TIME_CAP/NONLES_MIX_TIME_CAP у донора отсутствуют — капы инлайн), с повтором synth_710.

## 5. Полный протокол §3 против эталона same_but_fds6 (CHID b7896c0a, FDS-6.11.1)

Зеркальный прогон: одиночный вход detectors.fds с T_END=690; убит детекторной цепочкой на t=41.88 с (эталон: 39.10 с — цепочка CTRL_3=TIME_DELAY(DEVC_4052,20)→ANY→KILL+RESTART та же); продолжен рестарт-церемонией finish.fds (RESTART=.TRUE., T_END=690): csv обрезаны до t<=41.888, заглушки _1_3.s3dd/_2_3.s3dd, PL3D в входе отсутствует. **Рестарт-церемония сработала с первого раза (нить 5 этапа 3 закрыта): состояния DEVC/CTRL восстановлены, kill-цепочка не перезапустилась (пройдена точка t≈62 с), HRR непрерывен через шов (279 кВт → 306 кВт через один нулевой интервал).** Итог: STOP success, t=690.08, 4373 шага стадии, ~21 мин стен.

| Пункт | Эталон | Зеркало R29d | Вердикт |
|---|---|---|---|
| (а) max_dens меш-1 (наша col6 ↔ эталон col4), кг/м³ | 60с: 1.345e-4; 80с: 2.002e-4; 100с: 2.667e-4; 120с: 3.500e-4; 160с: 4.945e-4 | 1.272e-4; 1.803e-4; 2.334e-4; 2.796e-4; 3.989e-4 (−5.5/−9.9/−12.5/−20.1/−19.3 %) | **PASS** (допуск ±50 %) |
| (б) HRR | 98с: 1.59e3; 158с: 4.49e3; 398с: 2.73e4; 600с: 6.71e4; 689с: 7.49e4 кВт | 1.66e3 (+4.1 %); 4.38e3 (−2.5 %); 2.81e4 (+2.9 %); 6.18e4 (−7.9 %); 7.96e4 (+6.3 %) | **PASS** (допуск 10–15 % на росте; хвост тоже в 10 %) |
| (в) DT | — | finish-стадия: min 0.0736, p10 0.0829, медиана 0.1018, max 0.682; до 690 с без спирали | **PASS** |
| (д) DEVC z=1.5 (сетка кейса; «1.7 м» протокола соответствует ряду 1.5), 168 устройств: T>=70°C, VIS<=20 м | 8 пересечений парных | из 8 парных в ±20 % времени — 1 (DEVC_2 VIS20: 255.9→265.1, +3.6 %); 6 парных с задержкой зеркала ~+390…+600 с (40→429, 41→431, 54→501, 58→556, 59→576, 64→666 с); only-mirror 43 (ранний приход дыма в ближней зоне, напр. DEVC_8 VIS20: 1.83 с против 285 с); only-etalon 8; никогда 109 | **PARTIAL/FAIL по строгому критерию ±20 %** |

Заключение по (д): глобальные интегральные характеристики (мощность, максимальная плотность дыба, устойчивость шага) согласованы с FDS6 в допусках; локальные времена прихода дыма на высоте 1.5 м расходятся систематически (зеркало дымит ближнюю зону раньше, дальнюю — позже). Кандидат-причины для следующего этапа: различия LES-диффузии/параметров сноса FDS5 vs FDS6, дефолты видимости/MASS_EXTINCTION_COEF у очага, поведение открытых проёмов. Отдельного внимания заслуживает идентичность точек: сетка DEVC в обоих прогонах одна (порядок колонок сверен).

Тест B (срезы скорости, компаньон b7896c0a_v) — не выполнялся (за рамками этого этапа, следующий).

## 6. Базовый прогон (акт этапа 1, R29b-сборка)

Завершён под наблюдением этого этапа: STOP success, 900 с, 62 542 шага, HRR 3.92 МВт на финише, DT min 0.008/медиана 0.0133/max 0.11 (по консольным интервалам). Прото-акт сложен в `test/fds5ng_baseline/`; метрики для акта этапа 1 зафиксированы выше. После финиша fds5ng.exe освобождён и перезаписан сборкой R29d.

## 7. Промпт следующей сессии (этап 6, R29e)

1. Прочитать: этот отчёт; `HANDOFF_plan_B_fds5_rebase.md` (родитель, правила/§3/§4); `HANDOFF_plan_B_stage4_R29c_report.md` (реестр неотнесённого); `memory/COMPATIBILITY_REPORT.md` §13–15; `memory/FDS5_MLR_BASED_HRR_AND_CCC_IMPLEMENTATION.md`.
2. **R29e = TAU_G-капы MIX_TIME + безусловный DELTA вне LES_IF** (fire.f90, донор): перенос хунков DELTA-блока (вычисление DELTA до LES_IF без USE_MAX_FILTER_WIDTH-ветки в non-LES… сверить точно по донору), MIX_TIME для non-LES (C_EDC-формула), TAU_G-ограничители MIX_TIME (`MIX_TIME=MIN(MIX_TIME,TAU_G)`; у донора — инлайн-капы без отдельных глобалов; LES/NONLES-капы донора отсутствуют в коде — переносить ровно то, что есть). Приёмка: повтор synth_710 — ожидание HRR 6.4–6.8 МВт на 500–600 с (донор), DT>=0.03, F_INJECT≈1 (по BURN_RATE≈0.1513 кг/с); повтор (а)(б) этапа 4 (0106/room_fire — без регрессий по трассерам/останову). После R29e — тест B (срезы скорости, b7896c0a_v) и разбор (д)-расхождений DEVC 1.5 м (п. 5).
3. НЕ переносить (реестр §3 R29c + этот отчёт §1): FLAME_SHEET; ITMP-клампы; cut-face/SOAP/RELAXED_ALIGNMENT; телеметрию донора; EMBEDDED_MESH_EXCHANGE; DZ_CAP2/Q_BOUND_3; CFL_DP_SCALE.
4. Среда: всё свободно; fds5ng.exe = R29d; скрипты `C:\Users\User\AppData\Local\Temp\kilo\build2025ng.bat`, `run_ng_scratch.bat`; парсер срезов `readsf.ps1`, парсер DEVC `devcross2.ps1` (колонки строкой) — там же.
5. Отчёт: `HANDOFF_plan_B_stage6_R29e_report.md`, та же структура.
