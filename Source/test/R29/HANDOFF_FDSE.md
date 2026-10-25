# HANDOFF: FDS E (fds5ng) — Инструкция для следующей сессии

**Дата актуализации:** 2026-10-25
**Ответственный:** AI Agent (Hermes)
**Статус:** Активен

---

## 1. АБСОЛЮТНЫЕ ПУТИ (НЕ МЕНЯТЬ)

| Назначение | Путь |
|---|---|
| **Исходники FDS E (fds5ng)** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Source\fds5ng\` |
| **Бинарник FDS E (актуальный)** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Source\test\R29\fdse.exe` |
| **Сборка Release (БЕЗ /Qipo)** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng\LINK_MONOLITHIC_NO_IPO.bat` |
| **Сборка Debug** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng\BUILD_DEBUG_FULL.bat` |
| **Сборочная папка** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng\` |
| **Тестовая директория R29** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Source\test\R29\` |
| **Оригинальный FDS 6.11.1** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Source\test\R29\fds6.exe` |
| **Оригинальный FDS 5.5.3** | `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Source\test\R29\FDS5\fds5_win_64.exe` |
| **Репо GitHub** | `https://github.com/firegoaway/fds_excessive_plus.git` |
| **Ветка** | `master` |

> ⚠️ **НИКОГДА не использовать `make_fds5.bat` для финальной сборки** — он включает `/Qipo`, который раздувает бинарник до 27 МБ и вызывает проблемы.
> ✅ **ВСЕГДА использовать `LINK_MONOLITHIC_NO_IPO.bat`** для release-сборки (только `/O2`).

---

## 2. ПРОТОКОЛ ЗЕРКАЛЬНЫХ ТЕСТОВ

### 2.1. Структура папки теста
Каждый тест в `Source/test/R29/<test_name>/` должен содержать:
- `*.fds` — исходный входной файл (из Fenix+ или адаптированный)
- `run_fdse/` — прогон FDSE (копия .fds + выходные CSV/OUT)
- `run_fds6/` — прогон FDS 6.11.1 (копия .fds + выходные CSV/OUT)
- `run_fds5/` — прогон FDS 5.5.3 (адаптированная копия .fds + выходные CSV/OUT)
- `report_<test_name>.md` — итоговый отчет
- `diagnosis.md` — (опционально) диагноз при ошибке запуска

### 2.2. Запуск зеркального теста
```bash
# 1. Создать подпапки
mkdir -p run_fdse run_fds6 run_fds5

# 2. Скопировать .fds в каждую
cp <source>.fds run_fdse/
cp <source>.fds run_fds6/
cp <source>.fds run_fds5/

# 3. Запустить FDSE (актуальный бинарник)
cd run_fdse
E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/test/R29/fdse.exe <file>.fds > fdse_run.log 2>&1

# 4. Запустить FDS6
cd ../run_fds6
E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/test/R29/fds6.exe <file>.fds > fds6_run.log 2>&1

# 5. Запустить FDS5 (требует адаптации входа!)
cd ../run_fds5
E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/test/R29/FDS5/fds5_win_64.exe <file>_fds5.fds > fds5_run.log 2>&1
```

### 2.3. Адаптация входа для FDS 5
FDS 5.5.3 НЕ поддерживает:
- `LUMPED_COMPONENT_ONLY=.True.` в `&SPEC`
- `SPEC_ID_NU()` / `NU()` массивы в `&REAC`
- `FUNCTION_TYPE='EXTERNAL'`, `'TIME_DELAY'`, `'KILL'`, `'RESTART'` в `&CTRL`
- `VISIBILITY_FACTOR`, `MAX_LEAK_PATHS`, `MINIMUM_ZONE_VOLUME`, `BNDF_DEFAULT`, `EXTERNAL_FILENAME`, `DT_EXTERNAL` в `&MISC`
- `MPI_PROCESS=` в `&MESH`
- `DT_RESTART`, `DIAGNOSTICS_INTERVAL`, `FLUSH_FILE_BUFFERS`, `STATUS_FILES`, `STATE_FILE`, `DT_DEVC`, `DT_BNDF`, `NFRAMES`, `DT_FLUSH`, `DEVC_COLUMN_LIMIT` в `&DUMP`
- `HYDROGEN CHLORIDE` как отдельный вид (без mixture fraction)

**Алгоритм адаптации:**
1. Удалить все неподдерживаемые параметры (см. выше).
2. Заменить `&REAC` с `SPEC_ID_NU` на простую реакцию: `&REAC FUEL='SPEC_1' HEAT_OF_COMBUSTION=14000 SOOT_YIELD=0.02 CO_YIELD=0.047/`
3. Удалить все `&SPEC` с `LUMPED_COMPONENT_ONLY` или `SPEC_ID()` массивами.
4. Удалить все `&CTRL` с неподдерживаемыми `FUNCTION_TYPE`.
5. Удалить `&DEVC` с `SPEC_ID='HYDROGEN CHLORIDE'`.
6. Сохранить как `<name>_fds5.fds`.

---

## 3. МЕХАНИЗМ СРАБАТЫВАНИЯ ИЗВЕЩАТЕЛЕЙ (Fenix+)

### 3.1. Как Fenix+ моделирует извещатели
Fenix+ использует **НЕ DEVC с SETPOINT**, а связку:
```
&SLCF QUANTITY='EXTINCTION COEFFICIENT' CELL_CENTERED=.True. PBZ=<height>/
&CTRL ID='CTRL_EXT' FUNCTION_TYPE='EXTERNAL'/
&CTRL ID='CTRL_DELAY' FUNCTION_TYPE='TIME_DELAY' INPUT_ID(1:1)='CTRL_EXT' DELAY=20/
&CTRL ID='DetectorsTrigger' FUNCTION_TYPE='ANY' INPUT_ID(1:1)='CTRL_DELAY'/
&CTRL ID='DetectorKill' FUNCTION_TYPE='KILL' INPUT_ID(1:1)='DetectorsTrigger'/
```

**Критерий срабатывания (Методика 1140 Приложение 11):**
- Эффективный диаметр задымленной области: `d_эфф = sqrt(4*F/pi)`
- Порог: `d_эфф >= 12.8 м` (для дымовых извещателей)
- Инерционность: `DELAY=20 с` (фиксировано методикой)

### 3.2. Совместимость сольверов
| Механизм | FDSE | FDS6 | FDS5 |
|---|---|---|---|
| SLCF+CTRL (EXTERNAL/TIME_DELAY/KILL) | ✅ | ✅ | ❌ |
| DEVC с SETPOINT | ✅ | ✅ | ✅ |
| DEVC-сетка (альтернатива) | ✅ | ✅ | ✅ |

### 3.3. Альтернатива для FDS5: DEVC-сетка
Поскольку FDS5 не поддерживает EXTERNAL/TIME_DELAY, используется подход:
1. Разместить по одному `&DEVC QUANTITY='EXTINCTION COEFFICIENT'` в каждой ячейке на высоте извещателей.
2. Постпроцессинг `_devc.csv`: подсчитать ячейки, превысившие порог.
3. Вычислить `F = N_triggered * dx * dy`, `d_эфф = sqrt(4*F/pi)`.
4. Триггер при `d_эфф >= 12.8 м`.

**Порог EXTINCTION COEFFICIENT:**
- Методика 1140: 0.2 дБ/м → **K = 0.0461 1/м**
- Fenix SETPOINT=0.023 (оптическая плотность) → K = 0.0530 1/м
- **Рекомендуемое значение: 0.0461 1/м**

---

## 4. ИЗВЕСТНЫЕ ФИКСЫ И БАГИ

### R29e (2026-10-25): Авто-назначение SPEC_ID='SOOT' для оптических величин
**Проблема:** В `read.f90:10482-10485` условие `.AND. MIXTURE_FRACTION` блокировало назначение SOOT при `LUMPED_COMPONENT_ONLY` видах. Видимость оставалась 30 м.
**Фикс:** Убрано условие `.AND. MIXTURE_FRACTION`. Теперь совпадает с FDS6.
**Файл:** `Source/fds5ng/read.f90`

### R29z6: Wall thermal clamp fix
**Проблема:** Numerical instability при t≈31 с в тестах 30d2fba3, 753e73b0.
**Фикс:** Clamp wall temperature to prevent unphysical cooling.
**Статус:** Верифицирован.

### R30: MPI stabilization
**Проблема:** Access violation после MPI_FINALIZE при KILL.
**Фикс:** RETURN вместо STOP в END_FDS (main.f90:1263).
**Статус:** Верифицирован на 31a68a22_detectors.

### 76c3f508: EXTERNAL_FILENAME handling
**Проблема:** При отсутствии файла EXTERNAL_FILENAME солвер генерирует ложный KILL вместо ошибки.
**Статус:** Диагностировано, требует фикса в func.f90.

---

## 5. ПРАВИЛА РАБОТЫ С GITHUB

### 5.1. Что коммитить
✅ **ОБЯЗАТЕЛЬНО:**
- Исходные `.fds` файлы (в корне папки теста или в `run_*/`)
- Отчеты `report_*.md` и `diagnosis.md`
- Изменения в исходниках `Source/fds5ng/*.f90`
- Скрипты сборки `Build/impi_intel_win_ng/*.bat`
- Документацию `HANDOFF_FDSE.md`

❌ **НИКОГДА не коммитить:**
- Переписку (`ChatExport_*`, `result.json`, скриншоты чата)
- Выходные CSV/OUT/SMV файлы (слишком большие)
- Бинарники `.exe` (кроме эталонных в `test/R29/`)
- Объектные файлы `*.obj`, `*.mod`
- Логи сборки `build_log*.txt`
- Временные файлы `nul`, `git*.txt`

### 5.2. Формат коммитов
```
fix(fdse): <краткое описание фикса>
test(R29): add <test_name> source .fds files and report
chore(R29): cleanup build artifacts in impi_intel_win_ng
docs(R29): update HANDOFF_FDSE.md with new paths
```

### 5.3. Пуш
- Всегда пушить сразу после коммита.
- Ветка: `master`.
- Репозиторий: `firegoaway/fds_excessive_plus`.

---

## 6. ФОРМАТ ОТЧЕТОВ

Каждый отчет `report_<test_name>.md` должен содержать:

```markdown
# Отчет: <test_name> — FDS E vs FDS 6 vs FDS 5
**Дата:** YYYY-MM-DD
**Тест:** <название кейса>
**Версия FDS E:** R29x (описание фикса)

## Проблема
<Описание жалобы из переписки>

## Диагностика
### Корневая причина
<Техническое описание бага/расхождения>

### Численная верификация
<Таблицы сравнения ключевых параметров>

## Исправление (если применимо)
**Файл:** <путь>
**Изменение:** <описание>

## Результаты зеркальных тестов
| Показатель | FDS 5 | FDSE | FDS 6 |
|---|---|---|---|
| Max HRR | ... | ... | ... |
| Время срабатывания | ... | ... | ... |
| Мин. видимость | ... | ... | ... |

## Анализ механизма SLCF+CTRL (если применимо)
<Описание работы детекторов>

## Вывод
<Итоговое заключение>
```

---

## 7. РЕАКЦИЯ НА ЖАЛОБЫ ПОЛЬЗОВАТЕЛЯ

### 7.1. Алгоритм обработки жалобы
1. **Изучить переписку** в `ChatExport_*/` (локально, НЕ коммитить!).
2. **Найти исходный .fds** в локальной папке (обычно в `ChatExport_*/files/`).
3. **Запустить зеркальные тесты** FDSE/FDS6/FDS5.
4. **Сравнить ключевые параметры:** HRR, температура, видимость, время срабатывания детекторов.
5. **Если расхождение >10%:** исследовать корневую причину в исходниках.
6. **Если баг найден:** исправить, пересобрать через `LINK_MONOLITHIC_NO_IPO.bat`, верифицировать.
7. **Написать отчет** по шаблону выше.
8. **Закоммитить .fds + отчет**, запушить.

### 7.2. Критические замечания пользователя
- ⚠️ **НЕ добавлять переписку в GH** — только `.fds` файлы из `run_*` папок.
- ⚠️ **НЕ использовать /Qipo** — только `/O2` через `LINK_MONOLITHIC_NO_IPO.bat`.
- ⚠️ **Проверять ctrl.csv/devc_ctrl_log.csv** для детекторов, а не только devc.csv.
- ⚠️ **FDS5 требует адаптации входа** — никогда не запускать оригинальный Fenix-файл напрямую.
- ⚠️ **Память ограничена (2200 символов)** — экономить место, удалять устаревшее.

---

## 8. СПИСОК ВСЕХ ТЕСТОВ R29 И ИХ СТАТУС

| Тест | Статус FDSE | Статус FDS6 | Статус FDS5 | Примечание |
|---|---|---|---|---|
| 0526c6ca | ✅ | ✅ | — | Эталонный, без жалоб |
| 2c155400_detectors | ✅ | ✅ | — | Детекторы, совпадение |
| 30d2fba3 | ✅ | ✅ | — | Fixed numerical instability (R29z6) |
| 31a68a22_detectors | ✅ (serial+MPI) | ❌ MPI timeout | ✅ | R30 MPI fix verified |
| 61005c85_finish | ⏳ SKIP | ⏳ SKIP | — | Не хватает RAM для MPI×4 |
| 753e73b0 | ✅ | ✅ | — | Fixed numerical instability (R29z6) |
| 76c3f508_detectors | ✅ | ⏳ | ✅ | EXTERNAL_FILENAME bug diagnosed |
| b09533b5_finish | ✅ | ✅ | — | RESTART test |
| c6812929 | ⏳ SKIP | ⏳ SKIP | — | Не хватает RAM для MPI×8 |
| c6812929_detectors | ✅ (R29t) | ✅ (R29t) | — | Detector timing discrepancy (4.8×) |
| fc11291b_detectors | ✅ (R29e) | ✅ | ✅ | R29e fix: VISIBILITY/SOOT auto-assign |

---

## 9. ИНСТРУКЦИЯ ДЛЯ НОВОЙ СЕССИИ

При старте новой сессии по FDS E:
1. **Прочитать этот файл** (`Source/test/R29/HANDOFF_FDSE.md`).
2. **Прочитать последний отчет** в `Source/test/R29/*/report_*.md` (сортировать по дате).
3. **Проверить память** — там должны быть актуальные пути и правила.
4. **Не повторять изучение структуры проекта** — всё есть здесь.
5. **Следовать протоколам** из разделов 2–7 выше.
6. **При завершении задачи:** обновить этот HANDOFF, если появились новые фиксы/тесты.

---

*Этот документ является единственным источником истины по работе с FDS E. Все противоречия между памятью и этим файлом разрешаются в пользу этого файла.*
