# Сборка FDS E+ (FDSE) для Windows x64

Инструкция по компиляции релизной и отладочной версий солвера FDSE (fds5ng) с использованием Intel OneAPI (ifx + Intel MPI).

## Требования

- **Intel OneAPI Base Toolkit** (включая ifx, MKL, Intel MPI)
- **Git Bash / MSYS2** (для запуска скриптов через терминал Hermes или стандартный bash)
- Переменные окружения `I_MPI_ROOT` и `MKLROOT` должны быть установлены (обычно через `D:\Intel\oneAPI\setvars.bat intel64`)

> ⚠️ **ВАЖНО:** Не используйте флаг `/Qipo` при компиляции через `ifx` без явного `/Qipo-link` на этапе линковки. Это приводит к раздуванию бинарника до ~27 МБ (вместо ~8 МБ) из-за включения неоптимизированного IPO-bitcode. Релизная сборка по умолчанию использует чистый `/O2`.

---

## Релизная сборка (~8 МБ)

Оптимизированная версия для продакшн-расчётов. Без отладочной информации, без `/Qipo`.

### Способ 1: Через make (рекомендуется)

Полная сборка с автоматической перекомпиляцией изменённых файлов:

```bash
cd E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Build/impi_intel_win_ng
MSYS_NO_PATHCONV=1 cmd.exe /c "make_fds5.bat"
```

Бинарник: `fds5_impi_intel_win.exe` (~8 МБ).

### Способ 2: Прямая линковка (LINK_MONOLITHIC)

Если obj-файлы уже собраны и нужно только перелинковать:

```bash
cd E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Build/impi_intel_win_ng
MSYS_NO_PATHCONV=1 cmd.exe /c "LINK_MONOLITHIC.bat"
```

> ⚠️ Если obj-файлы были ранее скомпилированы с `/Qipo`, простая перелинковка через LINK_MONOLITHIC НЕ уменьшит размер. Нужна полная перекомпиляция через make или удаление всех *.obj перед сборкой.

---

## Отладочная сборка (~27 МБ)

Версия с полной отладочной информацией, traceback, проверками границ массивов. Используется для диагностики access violation и других runtime-ошибок.

```bash
cd E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Build/impi_intel_win_ng
MSYS_NO_PATHCONV=1 cmd.exe /c "BUILD_DEBUG_FULL.bat"
```

Бинарник: `fds5_impi_intel_win_db.exe` (~27 МБ).

Флаги: `/Od /debug:all /Z7 /traceback /check:all`.

---

## Копирование бинарника в тестовую папку

После сборки скопируйте exe в корень тестов:

```bash
cp E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Build/impi_intel_win_ng/fds5_impi_intel_win.exe \
   E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/test/R29/fdse.exe
```

Для debug-версии:

```bash
cp E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Build/impi_intel_win_ng/fds5_impi_intel_win_db.exe \
   E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/test/R29/fdse_db.exe
```

---

## Запуск тестов

### Serial

```bash
cd E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/test/R29/<test_case>/fdse
../../fdse.exe <case>.fds > run_fdse_serial.log 2>&1
```

### MPI (8 процессов)

```bash
cd E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/test/R29/<test_case>/fdse
mpiexec -n 8 ../../fdse.exe <case>.fds > run_fdse_mpi.log 2>&1
```

---

## Известные проблемы и решения

| Проблема | Причина | Решение |
|---|---|---|
| Бинарник 27 МБ вместо 8 МБ | `/Qipo` без LTCG в ifx | Удалить все *.obj, пересобрать через make_fds5.bat |
| Access violation в END_FDS (MPI) | STOP после MPI_FINALIZE | Использовать код с RETURN вместо STOP (main.f90:1263) |
| make_fds5.bat зависает | Конфликт find в MSYS | Использовать LINK_MONOLITHIC.bat после ручной компиляции |
| MPI crash при KILL | CHAIN_EXEC рассинхронизация | Обновлённый END_FDS без SHUTDOWN (коммит 1301170f37) |

---

## История изменений размера бинарника

- **До R30**: ~8 МБ (ifort + LTCG)
- **R30 (ifx + /Qipo)**: ~27 МБ (IPO-bitcode без оптимизации)
- **R30 fix**: ~8 МБ (чистый /O2 без /Qipo)
