# Промпт следующей сессии: проектирование GUI «Fenix+ alike» для fds5ng

Дата: 06.10.2026
Статус: проектная сессия — БЕЗ кода, только архитектура и решения владельца.
Родительские документы:
- `HANDOFF_next_stage_fenix_integration.md` (конвейер Fenix+ и его сигнальная
  поверхность);
- `HANDOFF_post_act_R29g_R29h.md` §15–16 (R29p: CTRL_STOP/.end; R29q: EVAC+);
- `E:/FIREGOAWAY/GitHub/FDS_source/FIREMODELS/fds/Source/fds5/evac+_docs/00_OVERVIEW.md`
  (EVAC+ завершён, этап 5 — сопряжение с пожарной динамикой — не начат).

## Что уже есть (не переизобретать)

1. **Решатель**: `Build/impi_intel_win_ng/fds5_impi_intel_win.exe` (R29p+R29q,
   монолит, OpenMP-DLL рядом). Сборка — `make_fds5.bat` в той же папке;
   пользовательская команда:
   `cmd /c ""D:\Intel\oneAPI\setvars.bat" && cd e:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Build\impi_intel_win_ng && make_fds5.bat"`
2. **Конвейер Fenix+** (двухстадийный, воспроизведён в R29p):
   - detectors-стадия: `CHID_detectors.fds` с CTRL `DetectorKill`/`DetectorRestart`;
     решатель на сработке извещателя пишет рестарты всех мешей, выходит с
     CTRL_STOP («STOP: FDS was stopped by KILL control function and completed
     successfully») и УДАЛЯЕТ `.end` — сигнал «успешно, продолжай»;
   - finish-стадия: `CHID_finish.fds` (RESTART=.TRUE., T_END увеличен,
     DT_RESTART увеличен) подхватывает рестарты и досчитывает;
   - `result_info.xml` (Status=Completed/Interrupted, ModelTime) и
     `CHID_devc_ctrl_log.csv` (сработки) — сигнальные файлы для GUI.
3. **EVAC+** (R29q): индивидуально-поточная эвакуация по Методике 1140
   (прил. 7/8), включается `&MISC EVAC_PLUS=.TRUE., EVACUATION_DRILL=.TRUE.` +
   сетка `EVACUATION=.TRUE., EVAC_HUMANS=.TRUE.` + группы `&EVPL/&EVSG/&EVEX/&EVHR/&EVCP`;
   кейсы-эталоны В1–В6/В1w/В5w в `Source/fds5/evac+_docs/cases/`; вывод:
   `CHID_evac.out/.csv`, `CHID_evac_prot.csv` (протокол подстановок для отчёта
   по Методике), `CHID_evac_dist.csv` (стохастика P=0.999), кадры EVA5/SmokeView.
4. **Пожарная часть**: R29-стек fds5ng (план B закрыт) — FLAME_SHEET для грубых
   сеток, EDC для точных; CSV FDS6-совместимые (devc/hrr/steps/ctrl);
   SmokeView-визуализация .smv/.sf/.s3d/.q.

## Цель сессии

Спроектировать наш собственный GUI-оркестратор (замена Fenix+ 3.7.1.1),
который:
1. Готовит и запускает расчёты (пожар + опционально эвакуация EVAC+);
2. Мониторит прогресс (steps.csv, hrr.csv, ctrl-лог, result_info.xml, .end/.out);
3. Ведёт двухстадийный конвейер detectors→finish автоматически (свой SUPERVISOR
   вместо внешней оболочки);
4. Показывает живые графики (HRR, температуры датчиков, DT/CFL) и статус мешей;
5. Экспортирует результаты (CSV, SmokeView-срезы, протокол EVAC+ для DOCX-отчёта).

## Вопросы к владельцу (решить до кода)

1. Технология GUI: Electron/Tauri (веб-стек, быстрый прототип) vs Qt/PySide vs
   C#/WinUI? Критичны: живые графики (по steps/hrr CSV), деревья сценариев,
   фоновые процессы, дистрибуция. У юзера уже есть опыт с WebGL-рендером
   (FDS Building Editor GUI удалён из репо — коммит 8d96e5d3ad).
2. Рендер 3D-картин дыма: SmokeView (готовый, но отдельный процесс) vs
   собственный WebGL-вьювер s3d/sf-файлов? (В удалённом GUI был WebGL/ANGLE.)
3. Оркестрация процессов: libmpi-запуски mpiexec -n N, лимит ядер, очередь
   задач, отмена, авто-рестарт конвейера — всё внутри GUI?
4. Формат проекта: один .fnx-подобный файл проекта или папка сценария +
   генератор .fds (Fenix+ генерит CHID_detectors/finish автоматически)?
5. Нужен ли импорт существующих .fnx (Fenix+ 3.7.1.1) и старых проектов?
6. Этап 5 EVAC+ (блокировка маршрутов по времени блокирования) — включать в
   v1 GUI или отложить?

## Начальные шаги сессии (после ответов)

1. Зафиксировать решения в `ng_docs/HANDOFF_gui_architecture.md`;
2. Каркас проекта (скелет репозитория, CI-сборка, changelog);
3. Спецификация формата проекта и сигнальной поверхности GUI↔решатель
   (расширение result_info.xml, heartbeat, прогресс-файлы);
4. Прототип монитора живого расчёта (tail steps.csv → график DT/T + HRR) —
   самая быстрая проверка UX-гипотез.

## Справка по среде

- Windows 10, Intel oneAPI 2025.3 (ifx), Intel MPI, MKL; сборка из
  git-bash/MSYS через cmd-обёртки (PATH без MSYS-утилит, GnuWin32 make в
  D:\make\GnuWin32\bin).
- Тестовые прогоны в D:/tmp (юзер: tmp=D:\tmp). Рабочие каталоги на E:.
- Donor-сборка fds5 в `Build/impi_intel_win` — эталон для сравнения.
- Fenix+ домен: эвакуация = встроенный модуль (НЕ FDS; FDS только пожар);
  Obf-имена Fenix+ меняются между версиями.
