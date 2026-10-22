# Отчёт: 31a68a22_detectors — FDS E+ (fds5ng) vs FDS 6.11.1 vs FDS 5

**Дата:** 2026-10-20 · **Папка:** `Source/test/R29/31a68a22_detectors/{fdse,fds6,fds5}`
**Солверы:** FDS E+ R29z6 · FDS 6.11.1 · FDS 5 (original)

## 1. Условия теста

| Параметр | Значение |
|---|---|
| Кейс | ZHK_Lazurnyj_2_3: Scenarij_1 (Fenix+ 3.7.1.3, формат FDS6) |
| Сетки | 8 мешей, MPI ×8 |
| T_END | 200 с |
| DEVC | 150 (T, visibility, O₂/CO₂/CO/HCl density, rad flux ×6 ориентаций, pressure) |
| Конвейер | EXTERNAL(CSV) → TIME_DELAY(20 с) → ANY → KILL + RESTART |
| Пожар | SURF_10 HRRPUA=147.72 + RAMP_Q, SPREAD_RATE=0.0125 (CTRL_14) |
| Химия | SPEC_ID_NU / LUMPED_COMPONENT_ONLY (FDS6 native) |

## 2. Статус завершения

| Солвер | Режим | Статус | Время моделирования | Шагов | Wall Clock | Примечание |
|---|---|---|---|---|---|---|
| FDSE | Serial | ✅ Успех | 200.06 с | 2705 | ~46 мин | Полный расчет без ошибок |
| FDSE | MPI ×8 | ✅ Успех (KILL) | 61.19 с | 670 | ~4 мин | R30 fix: ASSOCIATED(M%CELL_INDEX) + CHAIN_EXEC; стабильный выход по KILL без access violation |
| FDS6 | MPI ×8 | ❌ MPI Timeout | 69.35 с | 994 | ~28 мин | ERROR(123): particle exchange timeout |
| FDS5 | Serial | ✅ Успех | 200.03 с | ~2900 | ~55 мин | Полный расчет (KILL/RESTART отключены) |

> **FDSE (serial), FDSE (MPI) и FDS5 успешно завершили расчёт.** FDSE в MPI-режиме (v11) корректно отработал до срабатывания KILL (t=61.19 с, 670 шагов) без access violation после фиксов `ASSOCIATED(M%CELL_INDEX)` в `read.f90` и логики `CHAIN_EXEC`. FDS6 остается нестабильным на данном кейсе.

## 3. FDSE: MPI Stabilization (R30 fix)

### Найденная причина падения
Debug-сборка (`/debug /traceback /check:all`) выявила истинную причину access violation:
- **Ошибка:** `forrtl: severe (408): Attempt to use pointer CELL_INDEX when it is not associated with a target`
- **Локация:** `read.f90:9516` (SUBROUTINE READ_SLCF)
- **Причина:** Блок R29z (SLCF PUSH) использовал глобальный указатель `CELL_INDEX`, который в MPI-режиме не ассоциирован для мешей, не принадлежащих текущему процессу, на этапе чтения входных данных. В релизной сборке это проявлялось как «тихий» access violation без символов.

### Применённое исправление
В `read.f90` (строка ~9515) добавлена проверка `ASSOCIATED(M%CELL_INDEX)` и замена глобального указателя на безопасный доступ через `M%CELL_INDEX` с контролем границ:
}]

## 4. FDS6: MPI Particle Exchange Timeout

- Ошибка: `ERROR(123): MPI exchange of particle sizes (CODE=7) timed out`
- Все 8 процессов сообщили о таймауте одновременно
- Вероятная причина: deadlock в обмене частицами между mesh boundaries при 8 процессах
- Может быть связано с `MINIMUM_ZONE_VOLUME=1` и авто-зонированием, создающим дополнительные коммуникационные паттерны

## 5. FDS5: Успешный baseline (serial)

- Использован готовый бинарник `fds5_win_64.exe` (FDS 5.5.3, SVN 7031, 2010)
- Для совместимости удалены: `EXTERNAL_FILENAME`, `DT_EXTERNAL`, `DIAGNOSTICS_INTERVAL`, `VISIBILITY_FACTOR`, `MAX_LEAK_PATHS`, `HCL_YIELD`, `MPI_PROCESS`, `&ZONE XYZ`, `FUNCTION_TYPE='EXTERNAL'/'TIME_DELAY'/'KILL'/'RESTART'`
- Хлор учтен через `OTHER=0.0054 MW_OTHER=36.46` + явный `&SPEC ID='HYDROGEN CHLORIDE'`
- **Результат**: полный расчет до 200 с, HRR=195 кВт (vs FDSE 158 кВт, Δ=23%)

## 6. Сравнительная таблица (полные данные, t=200 с)

| Величина | FDSE (serial) | FDS5 (serial) | Δ | Примечание |
|---|---|---|---|---|
| Time, s | 200.06 | 200.03 | ≈0 | Оба дошли до T_END |
| Steps | 2705 | ~2900 | -7% | FDSE крупнее шаг |
| HRR, kW | 157.7 | 195.3 | **-37.6 (19%)** | FDSE занижает |
| Q_RADI, kW | 73.8 | 95.4 | -21.6 | FDSE меньше излучение |
| Q_CONV, kW | -212.1 | -205.8 | -6.3 | Сопоставимо |
| Q_COND, kW | 132.0 | 169.0 | -37.0 | FDSE меньше кондукция |
| MLR_AIR, kg/s | 0.063 | 0.009 | **7×** | FDS5: OTHER вместо явного HCl |
| DEVC rows | 163 | 1003 | 6× | FDSE реже вывод |

### Критические расхождения
1. **HRR**: FDSE занижает тепловыделение на ~19% относительно FDS5. Вероятная причина — различие в обработке SPEC_ID_NU vs OTHER+MW_OTHER.
2. **MLR_AIR**: Семикратная разница обусловлена упрощением химии в FDS5 (HCl как OTHER, а не отдельный вид).
3. **Q_RADI/Q_COND**: Систематическое занижение в FDSE коррелирует с меньшим HRR.
4. **Частота DEVC**: FDSE выводит данные реже (163 vs 1003 строк), возможно из-за различий в DT_DEVC по умолчанию.

> FDS6 исключен из финального сравнения из-за MPI-таймаута на t=69 с.

## 7. Выводы

1. **FDSE (serial) стабилен**: полный расчет до 200 с без ошибок, физические результаты разумны.
2. **FDSE (MPI) стабилизирован**: тест v11 подтвердил отсутствие access violation при выходе по KILL (t=61 с). Проблема финализации устранена; требуется верификация полного прогона до T_END=200 с.
3. **FDS5 baseline получен**: успешный serial-прогон позволяет количественно сравнивать физику. Расхождение HRR ~19% требует анализа химической модели.
4. **FDS6 нестабилен**: MPI particle exchange timeout на t=69 с делает его непригодным для данного кейса без дальнейшего debugging.
5. **Приоритеты для R30**: (a) исправить MPI-финализацию в FDSE, (b) исследовать расхождение HRR/Q_RADI между FDSE и FDS5, (c) диагностировать FDS6 MPI deadlock.

## 8. Артефакты

- `fdse/run_fdse_serial_full.log` — лог успешного serial-прогона FDSE (200 с)
- `fdse/run_fdse_mpi_v4.log` — лог MPI-прогона FDSE (crash on exit, исторический)
- `fdse/run_fdse_mpi_v11.log` — лог успешного MPI-прогона FDSE (KILL t=61 с, без ошибок)
- `fds5/run_fds5_serial.log` — лог успешного serial-прогона FDS5 (200 с)
- `fds6/run_fds6.log` — лог падения FDS6 (MPI timeout)
- `fdse/31a68a22_hrr.csv` — полный HRR FDSE (200 с, 2705 строк)
- `fds5/31a68a22_hrr.csv` — полный HRR FDS5 (200 с)
- `fdse/31a68a22_devc.csv` — полный DEVC FDSE (163 строки)
- `fds5/31a68a22_*_devc.csv` — полный DEVC FDS5 (1003 строки × 3 меша)
- `31a68a22_detectors_fds5.fds` — сконвертированный вход для FDS5
