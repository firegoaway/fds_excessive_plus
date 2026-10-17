# Отчёт пост-актовых ревизий R29g–R29r: диагностика, цепочка авто-рестарта, steps.csv, CSV-форматы FDS6, EVAC+, cut-face

Версия: 1.4 (дополнена R29r — порт cut-face)
Дата: 06.10.2026
Статус: выполнено по прямым решениям владельца после закрытия плана B
(итоговый акт — `HANDOFF_plan_B_final_act.md`; настоящий документ — дополнение к нему).
Ревизии кода: R29g…R29o + R29p (сборка `Build/impi_intel_win_ng`, 05.10.2026 23:14).

## 1. Решение владельца

1. `DT_EXTERNAL` и `DIAGNOSTICS_INTERVAL` должны работать как задумано (диагностика
   в консоль/.out с заданным интервалом, опрос внешнего CSV управления).
2. Сценарий Fenix+ `0106e472` (R29e_test_fenix): расчёт останавливается на первом
   срабатывании извещателя и должен перезапускаться автоматически (эталон Fenix+:
   tпор = 27,16 с, tср = 47,33 с для «Линейный дымовой извещатель 2»).

## 2. DT_EXTERNAL — уже функционален, изменений не требуется

Цепочка перенесена этапом R29b и идентична донору: глобал `DT_EXTERNAL` (cons.f90),
член `&TIME` (read.f90), включение `READ_EXTERNAL` при заданном
`EXTERNAL_FILENAME` и `DT_EXTERNAL>0` (read.f90:107), понятийный опрос файла в
главном цикле `T_MIN >= T_EXTERNAL` с перезапуском таймера `T_EXTERNAL = T_MIN +
DT_EXTERNAL` (main.f90), парсер `READ_EXTERNAL_FILE` (func.f90), потребление
`EXTERNAL_GATE`-CTRL (ctrl.f90).

Причина «молчания» в прогоне 0106e472: файл `0106e472_control.csv` отсутствует в
каталоге прогона — читатель делает `INQUIRE` и молча выходит; вход не содержит
CTRL типа `EXTERNAL`, на которые действует канал. Для видимого действия нужны
обе вещи: CSV-файл оператора и `&CTRL FUNCTION_TYPE='EXTERNAL'` во входе.

## 3. R29g — DIAGNOSTICS_INTERVAL

До R29g параметр читался локальной переменной `READ_DUMP` и выбрасывался
(заглушка совместимости R29b). Перенесена форма донора:

| Файл | Изменение |
|---|---|
| cons.f90 | Глобал `DIAGNOSTICS_INTERVAL=100` в строке целочисленных констант |
| read.f90 | Локальная декларация удалена — член namelist `&DUMP` привязан к глобалу (модульный `USE GLOBAL_CONSTANTS`) |
| main.f90 | `MOD(ICYC,100)` заменён на `MOD(ICYC,DIAGNOSTICS_INTERVAL)` в условии диагностики |

Дефолт 100 сохраняет поведение чистых FDS5-входов побитово (литерал 100 стока
равен дефолту). Значение 1 даёт печать каждого шага.

## 4. R29h — цепочка авто-рестарта после KILL

Механизм KILL+RESTART в FDS5/FDS6 и доноре всегда требовал внешнего перезапуска
(оболочка Fenix+ либо ручная рестарт-церемония этапа 5). Реализован авто-рестарт:

- маркер `CHID.chain` (текст: время KILL, номер звена);
- на старте `CHAIN_CHECK` (после `READ_DATA`): маркер + рестарт-файл →
  `RESTART=.TRUE.`, `RESTART_CHID=CHID`, `APPEND=.TRUE.`;
- в `END_FDS` при `USER_STOP`: `CHAIN_ARMED` (гарды: прогресс времени
  `T_KILL > T_PREV + 1e-6`, глубина цепочки ≤ 32; при нарушении маркер
  удаляется), реплика сообщений `SHUTDOWN`, закрытие юнитов `CHAIN_CLOSE_UNITS`,
  синхронный перезапуск потомка `CHAIN_EXEC` (`EXECUTE_COMMAND_LINE`, WAIT);
- успешное завершение (`NO_STOP`, T_END) снимает маркер (`CHAIN_CLEAN`);
- при сбое запуска маркер сохраняется — цепочку можно продолжить ручным
  запуском того же входа.

Повторного срабатывания KILL после рестарта нет: заластавленные CTRL
пропускаются (`UPDATE_CONTROLS`, ctrl.f90:21-26), рестарт-файлы восстанавливают
состояния DEVC/CTRL. Перезапись рестарт-файлов на KILL выполняется как
`DUMP_CORE` по `USER_STOP` (dump.f90:168) и по RESTART-CTRL (он же `CORE_DUMP`).

Ограничение: потомок запускается одиночным процессом (прямой запуск exe); при
многоранговом родителе продолжает считать все меши в одном процессе.

## 5. Валидация (скретч `Source/test/fds5ng_r29h/fenix0106/`)

Вход — копия `tests/R29e_test_fenix/0106e472_detectors.fds` (DT_RESTART=120,
DIAGNOSTICS_INTERVAL=1, T_END=120, EXTERNAL_FILENAME без файла):

1. Первая нога: печать каждого шага (R29g подтверждён); KILL детекторной
   цепочкой на t≈47,3 с — порог DEVC_4052 на 27,16 с + задержка 20 с, время
   совпадает с эталоном Fenix+ (tср = 47,33 с); рестарт-файлы записаны
   (`CHID_1.restart` 419 862 748 Б, `CHID_2.restart` 94 226 228 Б).
2. Шов цепочки (run_console.log): «STOP: FDS stopped by user» → «R29h:
   авто-рестарт цепочки — запуск продолжения» → «R29h: продолжение цепочки,
   звено 1 (t>=46.72 с): RESTART=.TRUE.»; нумерация шагов непрерывна
   (195→208 через точку рестарта), повторного KILL нет.
3. Вторая нога: до T_END=120,03 с (шаг 688), «STOP: FDS completed successfully
   (CHID: 0106e472)», маркер `0106e472.chain` снят автоматически, процессов не
   осталось. Шов hrr.csv непрерывен (строки 45,12 → 46,03 → 47,17 → 48,08 с),
   дублей и пропусков нет; на шве один переходный кадр HRR — то же поведение,
   что у ручной рестарт-церемонии этапа 5.

Отсутствие рестарт-файлов в `tests/R29e_test_fenix/` при наличии записей
DetectorRestart в `devc_ctrl_log` объясняется окружением владельца (файлы не
сохранены в копию каталога), не решателем: в контролируемом прогоне файлы
записываются.

## 6. R29i — CHID_steps.csv по умолчанию

Решение владельца: файл шагов пишется всегда (`STEPS_FILE=.TRUE.` в cons.f90),
член `&DUMP` управляет включением. Перенесена телеметрия донора: формат FDS6
(`Time Step,Wall Time,Step Size,Simulation Time,CPU Time`, ISO 8601 с зоной,
динамические разряды T), запись по `DIAGNOSTICS_INTERVAL` (main.f90), юнит/имя в
`ASSIGN_FILE_NAMES`, `CSVF steps` в .smv. Отличие от донора — APPEND-ветка на
рестарте (донор терял файл REPLACE); колонка CPU у потомка цепочки считается от
нуля своего процесса. Глобал `CPU_TIME_START` (cons.f90), стартовый замер в
main.f90.

## 7. R29j — CSV-выходы в формате FDS6

Причина: кавычки в заголовках `_ctrl.csv`/`_devc.csv` и старая структура
`_hrr.csv` — унаследованная девиация донора. Наработки июня (memory:
`FDS5_HRR_CSV_COMPATIBILITY.md` §4.1/§5, `COMPATIBILITY_REPORT.md` §15) были
утеряны из дерева донора при R28-переработках и не попали в R29b. Восстановлено
по memory и сверено с исходником FDS-6.11.1 (`E:\FIREGOAWAY\GitHub\FDS6.11.1\Source\dump.f90`)
и эталонными прогонами (`test/R28q_test/same_but_fds6/`):

| Файл | Изменение |
|---|---|
| _ctrl.csv | Заголовки без кавычек: `Time,CTRL_1,...` (формат строк уже был FDS6: ES15.7E3 + I2) |
| _devc.csv | Заголовки без кавычек (обе ветки COLUMN_DUMP_LIMIT) |
| _hrr.csv | Колонки FDS6: `Time,HRR,HRR_OX,Q_RADI,Q_CONV,Q_COND,Q_DIFF,Q_PRES,Q_PART,Q_ENTH,Q_TOTAL,MLR_AIR,MLR_SPEC_1,MLR_PRODUCTS,ZONE_*`; зоны в Pa (`PBAR(1,I)-P_INF`); MLR_AIR/MLR_PRODUCTS по стехиометрии (`O2_F_RATIO`, `Y_O2_INFTY`); HRR_OX/Q_DIFF/Q_PRES/Q_PART/Q_ENTH = 0 (в FDS5 нет разложения) |

Проверка заголовков NG после R29j побайтно совпадает с эталоном FDS6 по всем
трем файлам. `_steps.csv`, `_devc_ctrl_log.csv` — формат FDS6 (у FDS6 есть
 FN_STEPS и LU_DEVC_CTRL с теми же заголовками).

## 10. R29k — .out: секция DEVICE Activation без заголовка колонок

Владелец: Fenix+ разбирает сработки в том числе из `.out`. Сверка трёх форматов
(FDS6 эталон `tests/fds6_fenix_test/b7896c0a.out`, донор, NG) показала: FDS6
пишет строку `Device number Device ID …` только в секции CONTROL (и в
continuation-чанках DEVICE при >255 устройствах на файл), донор — так же; NG
после R29b писал её и в первой DEVICE-секции — лишняя строка. Убрана
(dump.f90, тег R29k). Формат строк активаций (I10,4X,A25,1X,L1,8X,F8.x по
диапазону |T_CHANGE|, «No Activation» при ≥10⁶) — сток, идентичен FDS6.

Примечание к цепочке: авто-рестарт R29h дописывает `.out` (APPEND), поэтому в
цепочечном прогоне финальные секции активаций и STOP-маркеры встречаются по
два раза (родитель на KILL + потомок на T_END) — как у FDS6 при
RESTART-цепочках Fenix+.

Валидация: room_fire 15 с (скретч `Source/test/fds5ng_r29k/roomfire/`),
STOP success, 439 шагов (побитово контроль этапов 3–6), DEVICE-секция без
заголовка.

Среда (восстановлено после очистки Temp 05.10.2026): `build2025ng.bat` и
`run_ng_scratch.bat` пересозданы с явными путями LIB (oneAPI компилятор+MPI,
MSVC 14.44 x64 коротким путём, Windows Kits 10.0.19041.0 um/ucrt x64 с `D:\`);
сборка 05.10.2026 19:14:53, 7 674 880 Б, `fds5ng.exe` синхронизирован.

## 11. R29l — выключатель авто-цепочки и standalone-дистрибутив

1. `FDS5NG_NO_CHAIN` (любое непустое значение) отключает авто-рестарт R29h и
   снимает остаточный маркер (CHAIN_ARMED, main.f90). Назначение: оболочка
   Fenix+ ведёт собственную процедуру detectors→finish и ждёт выхода
   процесса на KILL — авто-цепочка ей мешает (см. §5 отчёта проверок:
   «detectors дважды» искажает источник стадии 2).
2. Standalone-дистрибутив `Build/impi_intel_win_ng/dist/`: `fds5ng.exe` +
   `impi.dll`, `libmui.dll` (Intel MPI), `ifcore_msg.dll`, `irc_msg.dll`,
   `libicaf.dll` (сообщения/библиотеки компилятора; Фортран-рантайм
   слинкован статически — импорты идентичны донорскому fds5_r28x.exe,
   модель «самодостаточности» как у бандла FDS6: exe + DLL в одном
   каталоге, без setvars).
3. Валидация: оба прогона из `dist` с PATH=C:\Windows\System32 —
   room_fire 15 с: STOP success; 0106e472 + FDS5NG_NO_CHAIN=1: останов на
   детекторном KILL, процесс вышел, маркера нет. Скретчи
   `Source/test/fds5ng_r29l/`.

Примечание о сборке: `build2025ng.bat` (Temp) воспроизводит линкерную
строку официального `makefile5` (статические MKL mkl_intel_lp64/core/
sequential + impi.lib); setvars заменён явными LIB-путями (MSVC, Windows
Kits с `D:\`), поскольку setvars не срабатывал в контексте сессии.

## 12. R29m — цепочка выключена под MPI-запускателем

Инцидент интеграции (Source/test/R29l_test, Сценарий 1): Fenix+ запускает
решатель через mpiexec (2 ранга); на KILL авто-цепочка R29h запускала
потомка, тот наследовал PMI-канал hydry (PMI_FD) и падал в MPI_Init
(«PMI_Get_appnum returned -1», access violation) — оболочка видела сбой.

Исправление: CHAIN_ARMED при наличии PMI_FD/PMI_RANK (т.е. под mpiexec)
авто-цепочку не вооружает, пишет сообщение в консоль и снимает остаточный
маркер. Оркестровка рестарта под оболочкой принадлежит оболочке;
FDS5NG_NO_CHAIN остаётся ручным выключателем для одиночных запусков.

Валидация (mpiexec -n 1 из dist, чистый PATH): KILL на 47,3 с → сообщение
R29m → «STOP: FDS stopped by user» → процесс вышел, маркера нет,
рестарт-файлы написаны (419 862 748 + 94 226 228 Б) — контракт Fenix+
(выход на сработке + рестарты для finish-стадии) выполняется. Прямой
одиночный запуск (без PMI) цепочку сохраняет (валидации R29h–R29j).

Сборка R29m 05.10.2026 21:12:46; `fds5ng.exe` и `dist/` синхронизированы.

## 13. R29n — живой поток консоли и .out (порт R28m донора)

Инцидент интеграции R29m_test: решатель под Fenix+ чисто останавливается на
сработке, все данные сработки и рестарт на месте, но оболочка не переходит к
этапу эвакуации. Причина: NG не сбрасывал консоль (LU_ERR) — при
перенаправлении stderr буферизируется блоками, оболочка, хвостящая консоль в
реальном времени, не видит хода расчёта. Донор решал это принудительным
сбросом (R28m: «при перенаправлении в файл stderr буферизируется блоками»).

Исправление (dump.f90, WRITE_DIAGNOSTICS): FLUSH(LU_ERR) после позаговой
строки (порт R28m донора) и FLUSH(LU_OUTPUT) после диагностического блока
(как у FDS6). Блоки `.out` пишутся на каждый вызов с R29g (каденция
DIAGNOSTICS_INTERVAL) — формат идентичен донорскому, с ним Fenix+ работал.

Валидация (mpiexec -n 1 из dist): на 18-й секунде симуляции перенаправленный
лог уже содержит хвост шагов (шаг 67), `.out` растёт в реальном времени;
KILL на ~47 с — чистый выход. Скретч `Source/test/fds5ng_r29n/fenix0106/`.

Сборка R29n 05.10.2026 21:38:13; `fds5ng.exe` и `dist/` синхронизированы.

## 14. R29o — идентификация решателя «EXCESSIVE+» (распознавание Fenix+)

Инцидент R29m/R29n_test: конвейер Fenix+ (detectors → сработка → эвакуация →
finish T_END=1.25X) не переходил к эвакуации, хотя решатель чисто
останавливался и все данные сработки/рестарты были на месте. Сравнение с
рабочей донор-сессией (test/R28b_14-00, симбиоз с Fenix+) показало: донор
подписывался «EXCESSIVE+ R**» на трёх поверхностях — баннер консоли,
заголовок .out, строка FDSVERSION в .smv; NG печатал сток «5.5.3» и
«FDS5NG R29b». Fenix+ опознаёт свой решатель по этой строке.

Исправление: VERSION_STRING='EXCESSIVE+ R29o' (main.f90 — расходится в
консоль и .out) и та же строка в FDSVERSION .smv (dump.f90). Проверено на
room_fire под mpiexec: все три поверхности «EXCESSIVE+ R29o», STOP success.
Сборка 05.10.2026 22:09:03; `fds5ng.exe` и `dist/` синхронизированы.

## 15. R29p — семантика KILL-останова донора (CTRL_STOP + удаление .end)

Инцидент R29m/R29n/R29o_test: Fenix+ корректно дожидался выхода решателя на
сработке извещателя, рестарт-файлы обоих мешей и все данные сработки были на
месте, версия опознана («EXCESSIVE+»), но конвейер не переходил к стадии
finish: result_info.xml фиксировал Status=Interrupted.

Причина — три потери R29b-ребейза относительно донора R28x:

| Поверхность | Донор R28x | NG до R29p |
|---|---|---|
| Статус KILL-CTRL | PROCESS_STOP_STATUS=CTRL_STOP (отдельный от USER_STOP) | USER_STOP |
| STOP-сообщение | «STOP: FDS was stopped by KILL control function and completed successfully» | «STOP: FDS stopped by user» |
| Файл .end при KILL-останове и NO_STOP | УДАЛЯЕТСЯ (сигнал Fenix+ «успешно, продолжай») | остаётся → Interrupted |

Эталон FDS6 (tests/fds6_fenix_test): та же двухстадийная схема — leg 1 KILL с
сообщением «…stopped by KILL control function and completed successfully»,
leg 2 finish с RESTART; result_info.xml → Status=Completed.

Исправление (R29p):
- ctrl.f90: KILL-кейс EVALUATE_CONTROL → PROCESS_STOP_STATUS=CTRL_STOP;
- main.f90 END_FDS: CASE(CTRL_STOP) — донорское сообщение + BLOCK удаления
  .end (INQUIRE/OPEN/CLOSE STATUS='DELETE'); тот же BLOCK добавлен в
  CASE(NO_STOP); авто-цепочка R29h вооружается и при CTRL_STOP;
- идентификация: VERSION_STRING / FDSVERSION → «EXCESSIVE+ R29p».

Валидация (mpiexec -n 2, скретч D:/tmp/r29_repro, вход R29m_test):
1. Detectors-нога: KILL на t=42,40 с; рестарты обоих мешей (99 397 928 +
   23 166 548 Б); «STOP: FDS was stopped by KILL control function and
   completed successfully (CHID: b7896c0a)»; .end УДАЛЁН; чистый выход.
2. Finish-нога (RESTART=.True., T_END=50): подхват с t=42,93 с, досчёт до
   50,35 с, «STOP: FDS completed successfully», .end удалён.
3. Регресс room_fire 15 с: 439 шагов, steps.csv побитово совпадает с
   контролем R29k (DT/T: 0.357E-01/9.63930 … 0.432E-01/15.03384).
4. Сборка: пользовательская команда `cmd /c ""D:\Intel\oneAPI\setvars.bat"
   && cd …\impi_intel_win_ng && make_fds5.bat"` — «FDS5NG Build Successful!»,
   монолитный fds5_impi_intel_win.exe (7 729 664 Б) без внешних DLL.
   Батники make_fds5.bat / make_fds5_debug.bat созданы в impi_intel_win_ng
   (VPATH=Source/fds5ng, makefile5ng, автопропуск setvars если I_MPI_ROOT уже
   задан). fds5ng.exe и dist/fds5ng.exe синхронизированы.

## 16. R29q — порт EVAC+ из fds5 (R29a–R32)

Основание: EVAC+ в fds5 завершён (этапы 1–6 закрыты, открытых вопросов нет —
`Source/fds5/evac+_docs/03_PLAN.md`, `evac+_memory/SESSION_HANDOFF.md`, сессии
1–8, R29a…R32); fds5ng (план B) закрыт итоговым актом. Порт выполнен по
решению владельца после успешной валидации R29p в Fenix+ (прогон
`test/R29p_Test`: конвейер detectors→KILL→finish работает end-to-end, физика
без артефактов).

Перенесено:

| Элемент | Файл |
|---|---|
| Движок EVAC+ целиком (123 881 Б, 2602 строки, R32/EVP_REVNUM=297) | `Source/fds5ng/evac_plus.f90` (копия 1:1) |
| `USE EVACPLUS` + 9 hook-точек (инициализация, шаг, дампы, CSV, финальная статистика, обмен, версии) | `main.f90` |
| `USE EVACPLUS, ONLY: READ_EVAC_PLUS` + вызов после `READ_EVAC` + намлист `EVAC_PLUS` + дефолт | `read.f90` |
| `LOGICAL :: EVAC_PLUS=.FALSE.` | `cons.f90` |
| Ранний выход `FLUSH_EVACUATION_BUFFERS` при EVAC_PLUS | `dump.f90` |
| `evac_plus.obj` в obj_mpi5, зависимости, `EVACOMP = /Qopenmp` (компиляция+линковка) | `Build/makefile5ng` |

Не переносится (этап 5 EVAC+, за рамками v1): обмен поля пожар→эвакуация
(`EVAC_EXCHANGE` ранний `RETURN` при EVAC_PLUS — как у донора).

Сборка: `cmd /c ""D:\Intel\oneAPI\setvars.bat" && cd …\impi_intel_win_ng
&& make_fds5.bat"` — Build Successful; монолит 7 927 808 Б.
libiomp5md.dll скопирована рядом с exe и в dist/ (паритет с донором impi_intel_win;
/Qopenmp-link:static в ifx 2025.3 искажается GnuWin32-make и не применима).

Валидация порта (все прогнано реально, D:/tmp):

1. В1 (b1_evplus.fds, 100 чел. М0-3): «STOP: FDS completed successfully»;
   100/100 эвакуированы; последний выход 56.50 с — точно значение R30/R31/R32
   донора; баланс сегментов 0; OpenMP-фазы активны (3a move(OpenMP)).
2. Регресс room_fire 15 с: шаги/DT/T побитово на контроле R29k (439 шагов,
   0.432E-01/15.03384); ветки EVAC+ неактивны при EVAC_PLUS=.FALSE.
3. MPI detectors (mpiexec -n 3, вход R29p_Test): KILL на t≈49 с, все 3
   рестарта записаны, `.end` удалён — семантика R29p не затронута портом.
4. Идентификация для Fenix+ не изменена («EXCESSIVE+ R29p» на всех
   поверхностях; EVAC+ не меняет опознавание конвейера).

Известное ограничение: прогон R29p_Test в Fenix+ продолжает старый бинарник
R29p — R29q обратно совместим (порт полностью за флагом EVAC_PLUS=.FALSE. по
умолчанию), перезапуск не требуется.

## 17. R29r — порт cut-face (лёгкий вариант ccib) из fds5

«Лёгкий ccib» донора — `Source/fds5/cut_face.f90` (CUT_FACE_MODULE, 357 строк,
«Variant C+ Phase 2»): точная геометрия срезанных граней для OBST, не
совпадающих с сеткой. Не путать с полным FDS6 CC_SCALARS (ccib.f90, 23.5k строк,
не переносится). Решает проблему перекрытия/зарезания проёмов при грубой сетке:
OBST не снапится к сетке, вместо лестницы — точная доля площади грани
(ALPHA_CF_W) и объёма клетки (VOLUME_FACTOR).

Перенесено (R29r):

| Элемент | Файл fds5ng |
|---|---|
| cut_face.f90 1:1 (10 939 Б) | `Source/fds5ng/cut_face.f90` |
| OBSTRUCTION_TYPE: CUT_FACE (дефолт .TRUE., как в fds5), SUB_CELL_THIN, THIN_FRACTION(3), XB_INPUT(6) | type.f90 |
| MESH_TYPE/POINTERS: ALPHA_CF_W, CUT_FACE_FLAG, CFACE_CEN(3,NWC), VOLUME_FACTOR(I,J,K) | mesh.f90 |
| Аллокации ALPHA_CF_W=1/CUT_FACE_FLAG=F/CFACE_CEN=0; блок INIT_WALL_CELL (COMPUTE_CUT_FACE для точных, THIN_FRACTION для суб-ячеечных, прямоугольное пересечение для прочих; фантомные грани стыка мешей пропускаются) | init.f90 |
| Namelist &OBST +CUT_FACE; дефолт .TRUE.; XB_INPUT до снапа; детект SUB_CELL_THIN по I1==I2; THICKEN отключён для CUT_FACE; Phase 3 VOLUME_FACTOR по XB_INPUT после BLOCK_CELL; аллокация VOLUME_FACTOR=1 | read.f90 |
| NO_FLUX: ACF-смешивание FV* — (1-ACF)*FV + ACF*(безусловно-твёрдая грань); ACF=1 вырождается в прежнюю формулу | velo.f90 |
| DIVERGENCE_PART_1: DP /= VOLUME_FACTOR (<0.99) для частично занятых клеток | divg.f90 |
| cut_face.obj в obj_mpi5 + зависимости (velo/init/divg ← cut_face ← prec/cons/type/mesh) | Build/makefile5ng |

Семантика донора fds5 сохранена полностью: CUT_FACE=.TRUE. — глобальный
дефолт (решение владельца 06.10.2026: «CUT_FACE должен быть как в fds5 TRUE
по умолчанию. Он ведь решал проблему с перекрытием проёмов»). Явное
`CUT_FACE=.FALSE.` на OBST возвращает классический снап+THICKEN.

Валидация (все прогоны реальные, D:/tmp):

1. Регресс room_fire 15 с: 439 шагов, DT/T побитово на контроле R29k
   (0.357E-01/9.63930 … 0.432E-01/15.03384) — grid-aligned OBST дают
   ALPHA_CF_W=1, формулы вырождаются в прежние.
2. cutfire (наклонная стена XB=1.23–1.77 при сетке 0.5): прогоны с явным
   CUT_FACE=.TRUE. и с дефолтом .TRUE. идентичны; прогон штатный.
3. MPI detectors -n 3 (вход R29p_Test): KILL на ~49 с, 3 рестарта, .end
   удалён — конвейер R29p/R29q не задет.
4. Сборка: Build Successful, 7 941 632 Б; fds5ng.exe и dist/ синхронизированы.

Отличие от донора: NG не имеет донорских THICKEN_DIR-веток (у NG один THICKEN)
— гард `.NOT.OB%CUT_FACE` добавлен в существующие THICKEN-условия; collapse-
логика донора (ELSEIF .NOT.THICKEN_DIR → схлопывание тонких) не переносилась,
у NG исходное поведение тонких OBST сохранено.

## 8. Процедурные знания совместимости (извлечено из memory)

- Процедура Fenix+: `chid_detectors.fds` (стадия до сработки извещателя) →
  разбор сработок по CTRL из `.out`/`_ctrl.csv` → `chid_finish.fds` с полным
  T_END; на всех стадиях в каталоге живёт `chid_ctrl.csv` (структура выше).
  Авто-цепочка R29h делает тот же цикл внутри решателя без внешней оболочки.
- `COMPATIBILITY_REPORT.md` — 24-пунктный чек-лист: структура .smv, s3d
  Fortran-записи, 3 SMOKF3D на меш, порядок DEVICE_ACT до PL3D, игнор
  MPI_PROCESS, EXTERNAL-канал — всё перенесено в NG слоем R29b.
- Известные ограничения обмена: FDS5 требует точного выравнивания мешей на
  стыках (FDS6 допускает интерполяцию — EMBEDDED_MESH_EXCHANGE в замороженном
  реестре); `_zones.bin` не пишется (Fenix+ не использует); OBST-флаги видимости
  SmokeView захардкожены; срезы HCl пусты для топлив без CL в &SPEC.

## 9. Состояние среды

- Сборка `Build/impi_intel_win_ng` = R29p+R29q+R29r (EVAC+ + cut-face);
  `fds5ng.exe`, `dist/` и libiomp5md.dll синхронизированы. Кейсы EVAC+ —
  `Source/fds5/evac+_docs/cases/`; cut-face — `D:/tmp/r29r_cf/cutfire.fds`.
- Изменённые файлы R29g–R29n: `cons.f90`, `read.f90`, `main.f90`, `dump.f90`
  (теги в комментариях). `evac_plus.f90` и `Source/fds5/` не менялись;
  коммитов нет.
- Скретчи валидации: `Source/test/fds5ng_r29h/fenix0106/` (прогон R29j:
  полная цепочка 0→47,3→120 с), `Source/test/fds5ng_r29k/roomfire/` (R29k),
  `Source/test/fds5ng_r29l/` (R29l/R29m), `Source/test/fds5ng_r29n/` (R29n).


---
## 18. R29s — порт mesh-embedding (EMBEDDED_MESH_METHOD) из fds5 (донор R28l/R28m)

**Что перенесено:**
- `turb.f90`: активный модуль EMBEDDED_MESH_METHOD 1:1 из донора (1053 строки, 0 diff) —
  RESTRICT_MASS/DIV, VELOCITY_EMB, SCALARF/SCALAR_GHOST, MATCH_VELOCITY_EMB, LOCATE_MESH, SORT_MESH_LEVEL.
- `read.f90`: R28l-блоки — авто-детект вложенных мешей, компакция, контроли выравнивания/кратности,
  FIND_PARENT_MESH; диагностика "R29s: MESH n is embedded, parent=m" в stderr.
- `main.f90`: SORT_MESH_LEVEL (после INITIALIZE_MESH_EXCHANGE), 2 вызова EMBEDDED_MESH_EXCHANGE
  (predictor/corrector, после MATCH_VELOCITY — донорская позиция).
- `type.f90`/`mesh.f90`/`init.f90`: PARENT_MESH, MESH_LEVEL-поля, SCALAR_SAVE1-3, H_PREV,
  SOAP_PRHS_PREV/SOAP_HP_PREV/SOAP_FIRST_CALL, DHDX/DHDY/DHDZ (OMESH_TYPE).
- `pres.f90`: PRESSURE_SOLVER_SOAP (457 строк, 1:1 донор) + PRESS_METHOD в namelist &PRES;
  адаптивное pressure-blending (донорская позиция: ПОСЛЕ Poisson-решателя, условие
  PRESSURE_BLEND_ADAPTIVE .OR. EMBEDDED_MESH); pressure floor (opt-in, default OFF как у донора).
- `main.f90` PKG7-обмен: 7 компонент (FVX,FVY,FVZ,H,dHx,dHy,dHz) при EMBEDDED_MESH,
  4 компоненты (R29r-путь) без него; serial-ветка — градиенты только при embedding.
- `velo.f90` NO_FLUX / `pres.f90` PRESSURE_SOLVER: интерполяция H 2-го порядка (INTERP_OVERLAP_WEIGHT
  + градиентная коррекция) ТОЛЬКО при EMBEDDED_MESH; без него — прежнее усреднение (паритет R29r).
- `func.f90`: INTERP_OVERLAP_WEIGHT (MISC_FUNCTIONS).
- `cons.f90`: EMBEDDED_MESH default .FALSE. (opt-in, паритет R29r; у донора .TRUE.), PRESSURE_BLEND_*,
  PRESSURE_FLOOR_*, SOAP_*, CFL_DP_SCALE.

**Ключевая семантика (важно):**
- EMBEDDED_MESH=.FALSE. по умолчанию → все обычные прогоны идут кодовым путём R29r (побитово).
- Многоуровневое вложение (ребёнок в ребёнке) не поддерживается — как у донора (ошибка
  «multi-level embedding is not supported»). Рабочая схема: родитель + дети напрямую.
- Грани ребёнка обязаны выравниваться на сетку родителя; кратности целые (контроль при чтении).
- При embedding включается: SOAP-путь давления (1-я итерация), blending, 2-й порядок интерполяции H.

**Валидация (реальные прогоны):**
1. room_fire: побитово идентичен R29r-контролю (439 шагов, DT=0.432E-01, T=15.03384) — обычная
   физика НЕ затронута (директива юзера выполнена).
2. MPI detectors -n 3 (Fenix+ KILL-конвейер): STOP by KILL ~49 c, 3 рестарта, .end удалён — цел.
3. emb3 (embedding без горения, горячая плита): NG и донор оба проходят до конца (NG: T_fine=22.2 °C,
   донор: 21.5 °C на 20 с) — embedding-машина работает.
4. emb2 (embedding + пожар HRRPUA=300 на 0.25 м): NG падает ~5.9 с «Numerical Instability»,
   донор проходит (39.2 °C). Причина: связка embedding×горение; донорный COMBUSTION новее
   (ghost-HRR губернатор BUG#11 + антидетонационные лимиты, ~400 строк отличий) — НЕ перенесён.
   Это СЛЕДУЮЩИЙ шаг (R29t): порт COMBUSTION-стабилизаторов из донора.

**Сборка:** «FDS5NG Build Successful», exe 8 382 976 Б, синхронизирован в dist/ (fds5_impi_intel_win.exe,
fds5ng.exe). Debug-бинарник от 01:33 — устарел, при надобности пересобрать make_fds5_debug.bat.


---
## 19. R29t — стабилизация embedding×пожар + тест площадей горелки

**1. emb2 (пожар внутри embedding-ребёнка) — статус: НЕ СТАБИЛИЗИРОВАН, но полностью локализован.**
Проверено и исключено (всё инструментально, дебаг-код удалён):
- EMB-модуль turb.f90 — побайтово идентичен донору (0 diff, все 8 подпрограмм).
- Зоны давления: машина PRESSURE_ZONE/PBAR идентична (16/16 ссылок в divg.f90); различия только
  в LEAK-ветках (R26f N_LPZ / R27g semi-implicit) — в emb2 утечек нет, нерелевантно.
  Портирована диагностика PRINT_ZONE_PRESSURE_PROFILES (R26i) в dump.f90.
- H-интерполяция INTERPOLATED_ONLY (включая Phase-4 градиенты) — идентична донору в обоих решателях.
- CHECK_STABILITY портирован на донорскую семантику: скан 1..IBAR (без ghost) + вес CFL_DP_SCALE
  на DP-члене; CFL_DP_SCALE добавлен в &MISC-намлист (донорский параметр).
- Q_UPPER per-mesh корректен (0.25 м → 3.3 МВт/м³; 1.0 м → 2.7 МВт/м³).
- SOAP + PRESSURE_BLEND активны (alpha 0.37–1.0 в прогонах), DT_GUARD auto-on при embedding.
- ITMP-фикс (защита от отрицательных индексов Y2CPBAR_C): донорский clamp MAX(0,MIN(4999,…))
  перенесён в divg.f90 (4 места), velo.f90 (2), dump.f90 (8). Оборонительный фикс.
ФАКТИЧЕСКАЯ ПРИЧИНА (установлена): донор стабилен за счёт иного распределения теплоподвода —
его ghost-дожиг (HRR до 5×MLR·Hc на грубой сетке, кейс fa: 1451 кВт при впрыке 290 кВт)
размазывает Q по объёму и сглаживает DP. NG физичен (HRR ≈ MLR·Hc), поэтому Q сконцентрирован
у поверхности: тонкий меш перегревается (69 °C против 39 °C у донора), дивергенция растёт
4.9→29 1/с после выхода HRR на плато, DT коллапсирует на t≈5.9 c. CFL_DP_SCALE=0.5 и
DT_TARGET=0.05 (динамический лимит Q) не спасают — полевая (не шаговая) неустойчивость.
Donor-референс НЕ является физическим эталоном в этом режиме.
Следующая гипотеза (R29u): вертикальное размытие Q в тонком меше (ограничение Q на слой
по типу донорного flame-sheet распределения, но консервативное) либо ZETA-инициализация 0
у поверхности. Машина embedding как таковая работает (emb3/emb4/emb2_noemb проходят).

**2. Тест инвариантности площади vent-горелки (методика 1140 прил. 1) — ВЫПОЛНЕН, ПРОЙДЕН.**
Сценарии (одна &REAC PROPANE, TAU_Q=1.0, без SPREAD_RATE, MESH 0.25 м):
- area_a: 2×2 м @300 кВт/м² = 1200 кВт; area_b: 1×1 м @1200 кВт/м² = 1200 кВт (равный итог).
- area_s: 1×1 м @300 кВт/м² = 300 кВт (та же HRRPUA, меньшая площадь); area_d: RAMP_Q-вариант.
Результаты:
- Тотал HRR отслеживается ТОЧНО при любом разбиении (1200/1200; 300; 1200 RAMP≡TAU-эквивалент:
  area_d идентичен area_a бит-в-бит по HRR и T_above 239.6 °C).
- Равный тотал → дальнее поле идентично (T_far 22.88 против 22.78 °C), различие только в
  ближнем плюме (T_above 240 против 343 °C) — физика ширины ствола, не артефакт.
- Та же HRRPUA + меньшая площадь → строго пропорциональный пожар (HRR 300.0 кВт, без дрейфа,
  без артефактов частичных ячеек). «Точно те же результаты» при разных итоговых мощностях
  физически невозможны — правильная инвариантность (по заданному тоталу) соблюдена.

**3. Регресс и релиз-артефакты:**
- room_fire: побитово R29r (439 шагов, DT=0.432E-01, T=15.03384) на финальной сборке r29t16.
- MPI detectors (Fenix+ KILL): ПРОЙДЕН на финальном exe — 166 шагов, KILL-остановка штатная,
  рестарты 1-3 записаны, .end удалён конвейером (run_t.log).
- Сборка r29t16 чистая; exe 8 383 488 Б синхронизирован: fds5ng.exe + dist/ (13:33).
- Debug-бинарник устарел (01:33) — пересобрать make_fds5_debug.bat при надобности.

Вывод по релизу: NG готов как обычный солвер (побитовый паритет R29r); embedding —
opt-in, работает всюду кроме пожара в тонком ребёнке (R29u — одна конкретная задача).
