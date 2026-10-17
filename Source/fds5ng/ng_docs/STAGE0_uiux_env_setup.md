# Этап 0 (подготовка): UI/UX-среда разработки для GUI «FDS EXCESSIVE+ EVAC+»

Дата: 06.10.2026
Статус: ЗАВЕРШЁН. Среда развёрнута и верифицирована гейтами 44/44.
Родитель: `NEXT_SESSION_PROMPT_gui.md` (та же папка).

## 1. Что сделано (суть)

Развёрнут и адаптирован под Windows профессиональный дизайн-набор Agent Skills
для последующей разработки GUI 3в1 (построитель пожарной модели + построитель
эвакуационной модели + генератор DOCX-отчётов, Fenix+ alike).

Отобраны из 5 кандидатов (все склонированы и изучены в D:/tmp/uiskill_eval):

| Репозиторий | Вердикт | Причина |
|---|---|---|
| plugin87/ux-ui-agent-skills v2.8.0 | **ПРИНЯТ, ядро** | 43 objective gates, DTCG-токены (14 файлов, 450 токенов), 42 компонента, 138 дизайн-систем, data-viz/data-dashboard, image-to-code, evals. MIT. |
| jakubkrehel/skills | **ПРИНЯТ, дополнение** | Уникальные `break` (стресс-тест компонента) и `variant` (3 варианта на одном axis). MIT. |
| connerkward/ckw-design-skill | отбракован | Дублирует ядро (render-then-critique уже есть). MIT. |
| rknall/claude-skills (ui-design-review) | отбракован | НЕТ LICENSE — юридический риск для производного GUI. |
| content-designer/ux-writing-skill | отбракован | Перекрыт `content/voice-tone.md` ядра. MIT. |

## 2. Где лежит

```
E:\FIREGOAWAY\GitHub\ngui_designkit\          ← рабочая копия (вне fds-репо)
├── ux-ui-agent-skills\  (форк, pin f2e2f7fcc5cb9d99bbd8ec5e0d75cebb98e21e7e)
└── jk-skills\           (pin 267330e1adfc66a718fb65fa6918c1f06d0a689e)
```

Скилл-хаб: Hermes `software-development/fenix-gui-design` (автозагружаемый
манифест: золотые правила, команды гейтов, Windows-фиксы, Fenix-RE-процесс).

## 3. Windows-адаптация (6 фиксов в форке ядра)

1. `python3` → `python` во всех .mjs-вызывателях гейтов (на хосте `python3` —
   заглушка Microsoft Store, реальный Python 3.11.9 на D:/Python311).
2. `chromium.launch({channel:'chrome'})` → `chromium.launch()` в 17 скриптах:
   системного Chrome нет; установлен управляемый Playwright chromium
   (`npx playwright install chromium`).
3. Windows-баг `URL.pathname` (`E:\E:\...`) в `evals/run.mjs` и
   `scripts/build_tokens.mjs` → `fileURLToPath`.
4. `head -1` (POSIX) в evals → `readFileSync` (cmd.exe не знает head).
5. `accuracy_report.mjs`: cmd.exe не раскрывает `*.html`-глобы → JS-раскрытие;
   гигантская команда Industry templates (13 388 симв. > лимита cmd.exe 8191)
   разбита на массив из 20 пер-файловых цепочек, раннер научен array-записям.
6. Каскад мелких переполнений (+2..6px при 280px @1.25x root font) в трёх
   демо-файлах — метрики шрифтов Chromium vs Chrome: search.html
   `.field{grid-template-columns:minmax(0,1fr)}`, data-display.html `.capbar`
   flex-wrap, checkout.html `.pay label` flex-wrap+min-inline-size:0.

## 3a. Верификация (реальные прогоны, не утверждения)

- `node scripts/accuracy_report.mjs` → **44/44 PASS** (полный лог:
  /d/tmp/final_run.log; последние прогоны 06.10.2026).
- Негативный контроль: bad-фикстура `tests/fixtures/bad/clipped.html` корректно
  роняет verify_overflow (гейт ловит брак, exit 1).
- `node evals/run.mjs --self-test` → все 14 гейтов зелёные на reference app.
- Дважды верифицировано: все 20 industry-templates цепочек PASS точечным
  раннером (D:/tmp/rerun_gates.cjs).
- Полный прогон занимает ~10 мин (600+ сек): запускать фоном или частями.

## 4. Канон разработки GUI (из доктрины ядра, обязателен для проекта)

1. Токены DTCG 3 уровня (primitive→semantic→component); хардкод hex/px/ms
   запрещён, `lint_hardcodes.py` роняет билд.
2. 8 состояний интерактива (Default/Hover/Focus/Active/Disabled/Loading/Error/
   Selected) с реальным промером контраста.
3. Token by intent (Delete → action.destructive; одна primary-кнопка на экран).
4. Анти-слоп: без эмодзи (только lucide SVG), без «SECTION 0N», без цветных
   левых полос, без тире в UI-копирайте, variance mandate по layout-ам.
5. RENDER-AND-LOOK: гейты не доказывают пиксели — скриншот + клик-проверка
   каждого состояния.
6. Число качества — только из реального прогона гейта; иначе «не проверено».
7. Гейт «SKIPPED» ≠ PASS (DS_REQUIRE_BROWSER=1 в accuracy_report уже стоит).
8. Русский UI-копирайт по voice-tone + правила ЧВП (без сокращений ЧВП).

## 5. Как клонировать UI Fenix+ (процесс image-to-code)

```
скриншоты Fenix+ 3.7.1.1 (главное окно, меню, модалки, ховеры)
  → E:\...\gui-repo\reference\fenix\   (gitkeep-папка по канону templates/product-design)
  → vision_analyze: декомпозиция Atomic Design (Sidebar/Canvas/Toolbar/Inspector)
  → design-systems/library/{fluent,shadcn}/DESIGN.md: токены платформы
  → код flex/grid + семантические токены, ru-копирайт
  → гейты + adversarial critique (.claude/agents/design-critic.md как процедура)
```

RE-часть (изучать конвейер Fenix+ — detectors-стадия, result_info.xml,
сигнальная поверхность): скиллы `re-infra-stack` / `re_tools` (MCP: IDA/Ghidra/
x64dbg). Obf-имена меняются между версиями — авто-детект по содержимому.

## 6. Что подготовлено для GUI-разработки (готово к этапу 1)

- 42 компонентных спека (atoms→templates) + data-viz + data-dashboard — под
  живые графики HRR/DT/T и статус мешей.
- tokens/data-viz.json — палитры диаграмм (категориальные/последовательные/
  дивергентные), оси/сетка под плоты.
- Эталоны dense-инженерных экранов: examples/showcase, examples/terminal,
  templates/{mes,editor,emr,dispatch,atc,soc} — референсы лейаутов GUI.
- evals/run.mjs — cold-start скорер: можно оценивать сгенерированные экраны GUI
  по 14 гейтам (`node evals/run.mjs <brief-id> --dir <папка>`).
- Starter-шаблон проекта: `templates/product-design/` (`npx ux-ui-skills new`),
  в т.ч. `reference/` под скриншоты Fenix+ и design-tokens.json под наш бренд.

## 7. Открытые вопросы к владельцу (перенос из NEXT_SESSION_PROMPT_gui.md)

1. Технология GUI: Electron/Tauri vs Qt/PySide vs C#/WinUI (вопрос 1 родителя).
2. Рендер 3D дыма: SmokeView vs собственный WebGL s3d/sf-вьювер.
3. Импорт .fnx Fenix+ 3.7.1.1 — нужен ли в v1.
4. Этап 5 EVAC+ (блокировка маршрутов) — в v1 или отложить.

## 8. Правила эксплойтации среды (для будущих сессий)

- Гейты запускать из `E:\FIREGOAWAY\GitHub\ngui_designkit\ux-ui-agent-skills\`.
- Полный accuracy_report ~10 мин — фоном (`terminal background=true
  notify_on_complete=true`) или точечно по гейтам.
- НЕ `python3` (заглушка) — только `python`.
- axe_audit требует интернет (CDN axe-core) или локальный `npm i -D axe-core`.
- Держать пины: ядро f2e2f7f, jk 267330e; upstream-merge делать аккуратно,
  6 Windows-фиксов терять нельзя (список в скилле fenix-gui-design).
