# Промпт для кодера: Реализация FDS Building Editor (GUI)

**Дата создания:** 2026-06-12  
**Последнее обновление:** 2026-06-13  
**Версия FDS:** EXCESSIVE+ R16  
**Задача:** Простой, элегантный GUI-редактор зданий для FDS. Визуальное строительство топологии мышью → автоматическая генерация `.fds` → запуск расчёта → просмотр результатов.

---

## СТАТУС РЕАЛИЗАЦИИ (2026-06-13)

### ✅ Завершено: Фазы 0-2

**Коммиты:**
- `3e94f0eccd` — Фазы 0-1 (скелет + viewport)
- `b194faa99d` — Фаза 2 (стены + snap + selection + transform + undo/redo)
- `0a166ca1a4` — фикс NullReferenceException в MenuItem

#### Фаза 0: Скелет проекта
- Создан .NET 8 solution `Source/fds5/GUI/FdsGui.sln`
- Установлен .NET 8 SDK (8.0.422)
- Созданы 4 проекта:
  - `FdsGui.Core` — domain models (Building, Floor, Wall, Door, Slab, StairFlight, Room, Background, ProjectSettings, Geometry, Enums)
  - `FdsGui.Viewport` — 3D rendering (Camera, GridRenderer)
  - `FdsGui.App` — Avalonia UI приложение (MainWindow, GlViewportControl)
  - `FdsGui.Tests` — unit tests (xUnit)
- Добавлены NuGet пакеты: Avalonia 11.2.7, OpenTK 4.9.3, CommunityToolkit.Mvvm 8.4.0
- Создана базовая структура MainWindow с 3-panel layout (hierarchy / viewport / properties)

#### Фаза 1: Viewport + Камера + Grid
- Реализован `GlViewportControl` на основе `OpenGlControlBase` (Avalonia 11.2)
- Интеграция OpenTK через `AvaloniaGlBindings` bridge (GlInterface → IBindingsContext)
- GLSL шейдеры: vertex + fragment для отрисовки линий с цветами
- **Сетка (Grid):** 50×50 м, шаг 0.5 м, серый полупрозрачный
- **Оси XYZ:** X=красная, Y=зелёная, Z=синяя, длина 5 м, толщина 2.5 px
- **Камера (Blender-style):**
  - СКМ + drag → orbit | Shift+СКМ → pan | ПКМ → pan | колёсико → zoom
  - Preset views: Numpad 1 (front), 3 (right), 7 (top), 5 (toggle perspective/ortho), Home (fit all)

**Запуск приложения:**
```bash
dotnet run --project Source/fds5/GUI/FdsGui.App/FdsGui.App.csproj
```

**Технические заметки:**
- Avalonia 11.2: класс `OpenGlControlBase` (НЕ `OpenGlControl`)
- OpenTK GL4 bindings загружаются через `GL.LoadBindings(new AvaloniaGlBindings(gl))`
- Формат вершин: `float[7]` = `xyz` (3 floats) + `rgba` (4 floats), stride = 28 байт
- Разрешены `unsafe` блоки для GL buffer operations
- MenuItem в Avalonia Menu находятся в **логическом** дереве, а не визуальном — использовать `Click=` в AXAML

#### Фаза 2: Рисование стен + Snap to Grid (завершено)

**Коммит:** `b194faa99d`

**Созданные файлы (21 новый/модифицированный):**

FdsGui.Core:
- `Commands/ICommand.cs` — интерфейс Execute/Undo
- `Commands/CommandHistory.cs` — undo/redo stacks
- `Commands/AddWallCommand.cs` — добавление стены
- `Commands/MoveObjectCommand.cs` — перемещение стены
- `Commands/RotateObjectCommand.cs` — поворот стены
- `Commands/DeleteCommand.cs` — удаление объектов
- `Commands/ChangePropertyCommand.cs` — изменение свойства (generic)

FdsGui.Viewport:
- `Snapping/SnapEngine.cs` — grid/endpoint/midpoint/intersection snap, F4 toggle, `[`/`]` шаг
- `Renderers/WallRenderer.cs` — batched 3D box rendering (VBO/VAO/EBO), 8 вершин × 36 индексов на стену
- `Renderers/GhostRenderer.cs` — полупрозрачный preview при рисовании/трансформации
- `Renderers/SelectionRenderer.cs` — outline wireframe + handles на концах стен
- `Picking/ObjectPicker.cs` — ray-AABB intersection для выбора стен мышью, ScreenToGroundPlane
- `EditorState.cs` — центральное состояние: tools, transforms, selection, snap, walls

FdsGui.App:
- `ViewModels/PropertyPanelViewModel.cs` — свойства стены (имя, толщина, высота, Z, материал, цвет)
- `ViewModels/ToolbarViewModel.cs` — tool mode state, snap state, status bar
- `Views/PropertyPanelView.axaml` + `.cs` — UI панели свойств
- `Views/MainWindow.axaml` + `.cs` — toolbar кнопки, wiring, menu handlers
- `Controls/GlViewportControl.cs` — полная переработка: рендеринг стен/ghost/selection, input handling

**Реализованные возможности:**
- Wall tool (W): click → drag → создание стены с preview и snap
- Snap engine: grid/endpoint/midpoint/intersection, F4 toggle, `[`/`]` изменение шага (0.1/0.25/0.5/1.0)
- Selection: ЛКМ клик выбор стены (ray picking), Shift+ЛКМ добавление
- Move (G): Blender-style перемещение с snap
- Rotate (R): поворот вокруг центра стены
- Mirror H/V: отражение относительно центра
- Delete (X/Delete): удаление выбранных стен
- Undo/Redo (Ctrl+Z / Ctrl+Shift+Z): полная история операций
- Property panel: редактирование имени, толщины (мм), высоты, Z, материала, цвета (hex)
- Wall renderer: batched 3D boxes с обновлением VBO при изменениях
- Ghost renderer: полупрозрачный preview стены при рисовании
- Selection renderer: outline glow (wireframe) + endpoint handles

**Известные исправления:**
- MenuItem в Avalonia 11.2 Menu не являются visual descendants — `GetVisualDescendants()` не находит их. Использовать `Click=` обработчики прямо в AXAML.

### ⏳ Следующая фаза: Фаза 3 — Двери, Перекрытия, Лестницы

---

## ФИЛОСОФИЯ

> **Справится даже блондинка.**
> GUI скрывает сложность FDS. Пользователь рисует здание как в простом 3D-редакторе — стены, двери, перекрытия, лестницы. FDS namelist'ы генерируются автоматически. Никаких `/OBST XB=.../` — только визуальное строительство.

**Принципы:**
- Минимум кликов до результата
- Всё видно в 3D viewport
- Привычные Blender-управления камерой
- Строительные термины, а не FDS-жаргон
- Один инструмент — одна кнопка

---

## ВЫБРАННЫЙ СТЕК

### C# + Avalonia UI + OpenTK (OpenGL 4.5) + .NET 8

| Критерий | Значение |
|----------|----------|
| Установка | .NET 8 SDK ~500 МБ, NuGet ~20 МБ |
| Итоговый binary | ~30-50 МБ (self-contained single-file .exe) |
| Лицензия | MIT — свободное использование |
| Cross-platform | Windows / Linux / macOS |
| 3D рендеринг | OpenTK (OpenGL 4.5, шейдеры) |
| UI | TreeView, DataGrid, PropertyGrid — из коробки |
| MVVM | CommunityToolkit.Mvvm |

### Почему НЕ другие варианты

| Вариант | Проблема |
|---------|----------|
| Blender fork | 300+ МБ, GPL, overhead, API ломается |
| Qt + C++ | SDK 5-10 ГБ, LGPL сложная |
| Electron + Three.js | Chromium ~200 МБ RAM, WebGL only |
| Python + PyQt6 | GIL, медленный 3D, нет AOT |
| WPF + HelixToolkit | Windows-only, устаревает |

---

## КОНТЕКСТ ПРОЕКТА

### Рабочие каталоги

```
FDS5 source:        Source/fds5/
FDS6 reference:     Source/*.f90
Build:              Build/
SmokeView source:   Source/smv/               ← ИЗУЧИТЬ ГЛУБОКО (см. ниже)
FDS5 executable:    Build/impi_intel_win/fds5_impi_intel_win.exe
SmokeView:          D:\progz\Fenix+ 3.6.0.2\FDS6\smokeview.exe
Intel oneAPI:       D:\Intel\oneAPI\
MPI:                D:\progz\Fenix+ 3.6.0.2\FDS6\mpiexec.exe

GUI codebase:       Source/fds5/GUI/          ← ВСЯ КОДОВАЯ БАЗА GUI
GUI build output:   Build/win_GUI/            ← КОМПИЛЯЦИЯ GUI
```

---

## ИСХОДНЫЙ КОД SMOKEVIEW — ОБЯЗАТЕЛЬНО К ИЗУЧЕНИЮ

**Расположение:** `E:\FIREGOAWAY\GitHub\FDS_source\FIREMODELS\fds\Source\smv`

### Что изучить и зачем

SmokeView — это рабочий визуализатор FDS-результатов с 20+ лет разработки. Вместо того чтобы изобретать велосипед, нужно **глубоко изучить его исходный код** и взять готовые решения:

#### 1. Парсеры бинарных форматов (высший приоритет)
- `.smv` — master-файл SmokeView (текстовый, но специфичный формат)
- `.sf` (slice files) — 2D данные с colormap
- `.s3d` (smoke3d) — объёмный дым, RLE-компрессия
- `.bf` (boundary files) — данные на поверхностях
- `.iso` (isosurfaces) — триангулированные изоповерхности
- `.prt5` (particles) — частицы (спринклеры, дым)
- `.fed`, `.csv` — HRR, данные устройств

**Что взять:** готовые алгоритмы чтения, порядок байт, обработку заголовков, RLE-декомпрессию. Перенести в C# (BinaryReader).

#### 2. Рендеринг дыма и огня
- Алгоритм volume rendering (billboards vs. ray marching)
- Colormap'ы: как SmokeView мапит значения в цвета
- Изоповерхности: генерация и отрисовка
- Alpha blending для дыма
- Отрисовка slice planes с текстурой данных

**Что взять:** шейдерные подходы, формулы colormap'ов, параметры прозрачности.

#### 3. Рендеринг геометрии
- Как SmokeView рисует OBST (solid boxes, текстуры, cut-face)
- Как рисует VENT (цветные грани, стрелки)
- Wireframe vs. solid режимы
- Выбор объекта мышью (picking)

**Что взять:** алгоритмы отрисовки боксов, picking через ray casting.

#### 4. Камера и навигация
- Orbit/Pan/Zoom — как реализовано в SmokeView
- Projection switching (perspective/orthographic)
- Fit-to-view алгоритмы
- Анимация камеры

#### 5. UI-решения
- Timeline / playback controls для анимации результатов
- Colorbar (шкала значений)
- Visibility toggles для slice/isosurface/boundary

### Порядок изучения SmokeView

1. **Сначала** — найти main loop и rendering pipeline
2. **Потом** — парсеры файлов (ReadSMV, ReadSlice, ReadSmoke3D, ...)
3. **Потом** — рендеринг (DrawSmoke, DrawSlice, DrawIso, ...)
4. **Потом** — камера и навигация
5. **Наконец** — UI и timeline

### Интеграция в GUI

SmokeView source используется как **референс**, не копируется напрямую. Мы берём:
- Алгоритмы → переписываем на C#
- Формулы → адаптируем в шейдеры GLSL
- Структуры данных → мапим в C# классы

---

## СТРУКТУРА ЗДАНИЯ (Domain Model)

### Иерархия объектов

```
🏢 Здание: "Жилой дом"
├── 📐 Этаж 1
│   ├── 🧱 Стены
│   │   ├── Стена-1 (толщина 200мм, высота 3.0м)
│   │   ├── Стена-2
│   │   └── ...
│   ├── 🚪 Двери
│   │   ├── Дверь-1 (ширина 900мм, высота 2.1м)
│   │   └── ...
│   ├── ⬜ Перекрытия / Площадки
│   │   ├── Перекрытие-1 (толщина 220мм)
│   │   └── ...
│   ├── 🪜 Лестницы
│   │   ├── Марш-1 (подъём на 3.0м, направление ↑)
│   │   └── ...
│   ├── 🏠 Помещения
│   │   ├── Комната-1 (авто, по контуру стен)
│   │   ├── Комната-2 (ручная отрисовка)
│   │   └── ...
│   └── 🖼️ Подложки
│       ├── Подложка-1 (plan_1f.png, масштаб 1:100)
│       └── ...
│
├── 📐 Этаж 2
│   ├── 🧱 Стены ...
│   ├── 🚪 Двери ...
│   └── ...
│
└── 📐 Этаж 3
    └── ...
```

### Связь с FDS

| GUI объект | FDS namelist | Как генерируется |
|-----------|-------------|-----------------|
| Стена | `&OBST` | XB из геометрии стены + толщина, SURF=материал стены |
| Дверь | `&HOLE` в стене | XB из геометрии двери (вырез в OBST стены) |
| Перекрытие | `&OBST` | XB из геометрии перекрытия + толщина |
| Лестница | Серия `&OBST` | Ступени автоматически из высоты марша |
| Помещение | `&ZONE` | XB из bounding box помещения |
| Этаж | Группировка | Уровень этажа → Z-offset для всех объектов |
| Здание | `&HEAD` | Имя здания → CHID |
| Подложка | Только GUI | Не экспортируется в .fds |

**Ключевой момент:** Пользователь НЕ видит namelist'ы. Он рисует стены и двери. Генератор `.fds` делает всю работу.

---

## ОБЪЕКТЫ СТРОИТЕЛЬСТВА (подробно)

### Стена

| Свойство | По умолчанию | Единицы | Описание |
|----------|-------------|---------|----------|
| Толщина | 200 | мм | Толщина стены |
| Высота | (наследует этаж) | м | Высота стены. По умолчанию = высота этажа |
| Нижний Z | (наследует этаж) | м | Нижняя отметка. По умолчанию = уровень этажа |
| Материал | Бетон | — | SURF_ID для FDS |
| Цвет | Серый | RGB | Отображение в viewport |

**Отрисовка:** ЛКМ на grid → drag → ЛКМ отпустить. Стена = линия на плане × высота. При drag видна preview-линия с snap to grid.

### Дверь

| Свойство | По умолчанию | Единицы | Описание |
|----------|-------------|---------|----------|
| Ширина | 900 | мм | Ширина проёма |
| Высота | 2100 | мм | Высота проёма |
| Нижний Z | (уровень этажа) | м | Отметка низа проёма |

**Отрисовка:** Клик на стену → дверь врезается в стену. Дверь автоматически создаёт HOLE в OBST стены. Если дверь на внешней стене — это вход/выход.

### Перекрытие / Площадка

| Свойство | По умолчанию | Единицы | Описание |
|----------|-------------|---------|----------|
| Толщина | 220 | мм | Толщина плиты |
| Нижний Z | (наследует этаж) | м | Нижняя отметка |

**Отрисовка:** Аналогично стене, но горизонтально. ЛКМ → drag по плану → отпускание. Перекрытие = прямоугольник на плане × толщина.

### Лестница (марш)

| Свойство | По умолчанию | Единицы | Описание |
|----------|-------------|---------|----------|
| Высота подъёма | (высота этажа) | м | На сколько поднимается марш |
| Нижний Z | (уровень этажа) | м | Стартовая отметка |
| Направление | ↑ | — | Кнопка переключения: ↑ / ↓ / ← / → |
| Ширина марша | 1200 | мм | Ширина лестницы |
| Кол-во ступеней | (авто) | шт | Автоматически из высоты (h=150мм по умолчанию) |

**Отрисовка:** Клик → drag (длина марша на плане). Ступени генерируются автоматически. Кнопка «Сменить направление» на панели свойств.

### Помещение

| Свойство | По умолчанию | Единицы | Описание |
|----------|-------------|---------|----------|
| Способ отрисовки | Авто | — | См. ниже |
| Имя | Комната-N | — | Идентификатор |

**Два способа отрисовки (кнопки на панели свойств):**

1. **Произвольная область** — кликать точки мышью, формируя полигон. Замыкание = двойной клик или Enter.
2. **Авто по стенам** — клик внутри замкнутого контура стен. Алгоритм flood-fill находит замкнутую область, ограниченную стенами. Двери игнорируются (считаются частью стены для поиска контура).

---

## ЭТАЖ

| Свойство | По умолчанию | Единицы | Описание |
|----------|-------------|---------|----------|
| Высота этажа | 3.0 | м | Высота стен по умолчанию при создании |
| Уровень этажа | (авто) | м | Нижняя отметка. При добавлении = уровень предыдущего + высота предыдущего |

**Контекстное меню (ПКМ по этажу):**
- Добавить подложку
- Свойства этажа (высота, уровень)
- Переименовать
- Дублировать этаж
- Удалить этаж

**Видимость:**
- Выбран один этаж → виден только он
- Shift + выбор нескольких этажей → видны все выбранные
- Ctrl+A → все этажи

---

## ЗДАНИЕ

**Контекстное меню (ПКМ по зданию):**
- Переименовать
- Добавить этаж (новый этаж ставится точно над верхним существующим)
- Свойства (кнопка, содержимое — placeholder)

---

## ПОДЛОЖКИ

### Что это
Подложка — это фоновое изображение (план этажа из CAD, PDF, фото) для трассировки стен.

### Как добавляется
ПКМ по этажу → «Добавить подложку» → выбор файла изображения.

### Где отображается
- Открывается **вкладка рядом с 3D viewport** (или split view)
- В этой вкладке — **ортогональный вид сверху** (не перспектива!)
- Подложка отображается на плоскости Z = уровень этажа

### Свойства подложки

| Свойство | Описание |
|----------|----------|
| Файл | Путь к изображению |
| Линейные размеры | Указать реальные размеры (например: «10м × 8м») для масштабирования |
| Поворот | Угол поворота в градусах |
| Угол коррекции | Исправить перспективное искажение (если фото криво) |
| Прозрачность | 0-100%, чтобы видеть стены поверх подложки |
| Видимость | Вкл/выкл отображение подложки |

### Переключатель подложки в viewport
Среди инструментов viewport (camera pan, zoom и т.д.) должна быть кнопка **🖼️ Подложка: Вкл/Выкл** — toggle отображения подложки в основном 3D viewport.

---

## ИНТЕРФЕЙС (Layout)

### Главный экран

```
┌──────────────────────────────────────────────────────────────────────────┐
│ File | Edit | View | Simulate | Help                                      │
│ [New] [Open] [Save] [Save As] | [▶ Run] [⏹ Stop] [👁 Results]           │
├──────────┬─────────────────────────────────────────┬─────────────────────┤
│          │  ┌─ Toolbar Viewport ─────────────────┐ │                     │
│ Иерархия │  │ [🔄 Pan] [🔍+] [🔍-] [↻ Rotate]   │ │ Свойства            │
│          │  │ [G Move] [R Rotate] [S Scale]      │ │                     │
│ ▼ 🏢 Дом │  │ [📐 Snap: ON] [F4]                 │ │ 🧱 Стена-1          │
│  ▼ 📐 Э1 │  │ [🖼️ Подложка: ON]                 │ │                     │
│   ▼ 🧱   │  ├─────────────────────────────────────┤ │ Толщина: [200] мм  │
│    Ст-1  │  │                                     │ │ Высота:  [3.0] м   │
│    Ст-2  │  │                                     │ │ Нижний Z:[0.0] м   │
│   ▼ 🚪   │  │                                     │ │                     │
│    Дв-1  │  │         3D VIEWPORT                 │ │ Материал:           │
│   ▼ ⬜    │  │                                     │ │  [Бетон      ▼]   │
│    Пр-1  │  │    ┌──────────────────┐             │ │                     │
│   ▼ 🪜   │  │    │                  │             │ │ Цвет:               │
│    Мр-1  │  │    │   [здание]       │             │ │  [████████] [#80]   │
│   ▼ 🏠   │  │    │                  │             │ │                     │
│    Км-1  │  │    └──────────────────┘             │ │ [↺][↻] Поворот     │
│  ▼ 📐 Э2 │  │                                     │ │ [⇔]  Отраж. гориз. │
│   ...    │  │    X(красн.) Y(зелён.) Z(син.)     │ │ [⇕]  Отраж. верт.  │
│          │  │    Grid: 0.5m                       │ │                     │
│          │  └─────────────────────────────────────┘ │ [Удалить]          │
├──────────┴─────────────────────────────────────────┴─────────────────────┤
│ Статус: Этаж 1/3 | Стен: 12 | Дверей: 4 | Mesh cells: ~45,000           │
│ [▶ Запустить расчёт] [⏹ Остановить] [👁 Результаты] [✓ Проверить]       │
└──────────────────────────────────────────────────────────────────────────┘
```

### Панель инструментов строительства

```
┌────────────────────────────────────────────┐
│  [🧱 Стена]  [🚪 Дверь]  [⬜ Перекрытие]  │
│  [🪜 Лестница]  [🏠 Помещение]             │
└────────────────────────────────────────────┘
```

Выбор инструмента:
- Клик по кнопке → курсор меняется → рисуем мышью в viewport
- ESC → отмена инструмента, возврат к select mode
- Горячие клавиши: `W` = стена, `D` = дверь, `P` = перекрытие, `L` = лестница, `R` = помещение

### Панель иерархии (левая панель)

```
🏢 Здание: "Жилой дом"        [ПКМ → Переименовать, Добавить этаж, Свойства]
├── 📐 Этаж 1                  [ПКМ → Свойства этажа, Добавить подложку]
│   ├── 🧱 Стены
│   │   ├── Стена-1
│   │   ├── Стена-2
│   │   └── Стена-3
│   ├── 🚪 Двери
│   │   └── Дверь-1
│   ├── ⬜ Перекрытия
│   │   └── Перекрытие-1
│   ├── 🪜 Лестницы
│   │   └── Марш-1
│   ├── 🏠 Помещения
│   │   └── Комната-1
│   └── 🖼️ Подложки
│       └── План-1
├── 📐 Этаж 2
│   └── ...
└── 📐 Этаж 3
    └── ...
```

---

## УПРАВЛЕНИЕ КАМЕРОЙ (Blender-style)

### Мышь

| Действие | Результат |
|----------|-----------|
| СКМ (колёсико) + drag | Orbit (вращение вокруг точки интереса) |
| Shift + СКМ + drag | Pan (перемещение камеры) |
| Колёсико | Zoom in/out |
| ПКМ + drag | Pan (альтернатива) |
| ЛКМ + drag (select mode) | Рамка выделения |
| ЛКМ клик (select mode) | Выбор объекта |

### Клавиатура (Blender бинды)

| Клавиша | Действие |
|---------|----------|
| `Numpad 1` | Вид спереди |
| `Numpad 3` | Вид справа |
| `Numpad 7` | Вид сверху |
| `Numpad 5` | Переключение Perspective ↔ Ortho |
| `Numpad 0` | Вид от камеры |
| `Numpad .` | Focus на выбранном объекте |
| `Home` | Fit All (вместить всё в viewport) |
| `F4` | Вкл/Выкл привязку к сетке (snap to grid) |
| `G` | Move (перемещение выбранного объекта, как в Blender) |
| `R` | Rotate (поворот выбранного объекта) |
| `S` | Scale (масштабирование) |
| `X` / `Delete` | Удалить |
| `Escape` | Отмена текущего инструмента / снять выделение |
| `Ctrl+Z` | Undo |
| `Ctrl+Shift+Z` | Redo |
| `Ctrl+D` | Дублировать объект |
| `Ctrl+A` | Выделить всё |
| `Shift + клик` | Добавить к выделению |

### Toolbar камеры (кнопки в viewport)

```
[🔄 Pan]  [🔍+]  [🔍-]  [↻ Rotate]  [🏠 Iso]  [Top]  [Front]  [Right]
```

Это кнопки-дубликаты горячих клавиш. Для тех, кто предпочитает кликать.

### Toolbar редактора (в viewport)

```
[G Move]  [R Rotate]  [S Scale]  [⇔ Mirror H]  [⇕ Mirror V]  [↺ 90°]  [↻ 90°]
```

---

## ПРИВЯЗКА К СЕТКЕ (Snap to Grid)

### Поведение

- **По умолчанию:** ВКЛ
- **Toggle:** Клавиша `F4` или кнопка `[📐 Snap: ON/OFF]` в toolbar
- **Шаг сетки:** Настраиваемый (0.1м, 0.25м, 0.5м, 1.0м) — переключение в toolbar или `[` / `]`
- **При snap ON:** Все точки привязываются к ближайшему узлу сетки. При создании стены курсор «прилипает» к узлам.
- **При snap OFF:** Свободное размещение. Но snap к существующим объектам (endpoint, midpoint, пересечение) всё равно работает.

### Типы привязок (snap targets)

| Snap | Описание |
|------|----------|
| Grid | Узлы сетки |
| Endpoint | Концы существующих стен |
| Midpoint | Середины стен |
| Intersection | Пересечения стен |
| Perpendicular | Перпендикуляр к стене |

### Визуальная обратная связь
При snap — курсор подсвечивается маленьким кружком с иконкой типа snap (как в CAD).

---

## ГЕНЕРАЦИЯ .fds ФАЙЛА

### Когда генерируется
- **Автоматически** перед запуском расчёта (кнопка «▶ Запустить расчёт»)
- **Вручную:** File → Export → `.fds`

### Маппинг GUI → FDS

```
Здание:
  CHID = "{building_name}"
  &HEAD TITLE='{building_name}' /

Этаж:
  Z-offset = уровень_этажа

Стена:
  &OBST XB=x1, x1+thickness, y1, y2, z_bottom, z_bottom+height /
  SURF_ID = материал_стены

Дверь:
  &HOLE XB=door_x1, door_x2, door_y-eps, door_y+eps, door_z, door_z+height /

Перекрытие:
  &OBST XB=x1, x2, y1, y2, z, z+thickness /

Лестница:
  Серия &OBST для каждой ступени (автоматически)

Помещение:
  &ZONE XB=bbox_x1, bbox_x2, bbox_y1, bbox_y2, bbox_z1, bbox_z2 /

MESH (автоматически):
  Генерируется один или несколько &MESH, покрывающих bounding box здания.
  IJK рассчитывается автоматически исходя из заданного размера ячейки
  (настраивается в свойствах проекта: "Размер ячейки: 0.1м").
```

### Настройки проекта (глобальные, для генерации .fds)

| Настройка | По умолчанию | Описание |
|-----------|-------------|----------|
| Размер ячейки | 0.1 м | Базовый размер ячейки mesh |
| Время расчёта | 600 с | T_END |
| Шаг вывода | 5 с | DT_SLCF |
| Материал по умолчанию | Бетон | Для новых стен |
| Температура среды | 20°C | TMPA |

Эти настройки доступны через `File → Project Settings` или через кнопку `⚙️` в status bar.

---

## ЗАПУСК РАСЧЁТА И РЕЗУЛЬТАТЫ

### Запуск

```
Кнопка [▶ Запустить расчёт]:
  1. Валидация (есть ли замкнутые помещения, нет ли пересечений)
  2. Генерация .fds файла
  3. Запуск FDS как subprocess
  4. Progress bar (парсинг stdout: "Time Step: ...")
  5. Лог вывода в нижней панели
```

### Результаты

```
Кнопка [👁 Результаты]:
  - Открытие SmokeView (внешний запуск) — быстрый вариант
  - ИЛИ встроенный viewer (долгосрочная цель):
    - Чтение .smv/.sf/.s3d/.bf/.iso
    - Slice planes с colormap
    - Smoke volume rendering
    - Timeline (play/pause/scrub)
    - Colorbar (шкала значений)
```

**Приоритет:** Сначала — внешний запуск SmokeView. Встроенный viewer — отдельная большая задача, основанная на глубоком изучении исходников smv.

---

## АРХИТЕКТУРА ПРОЕКТА

### Структура solution

```
FdsGui/
├── FdsGui.sln
│
├── FdsGui.Core/                   # Domain model + генерация .fds (без UI)
│   ├── FdsGui.Core.csproj
│   ├── Models/
│   │   ├── Building.cs            # Здание (root)
│   │   ├── Floor.cs               # Этаж
│   │   ├── Wall.cs                # Стена
│   │   ├── Door.cs                # Дверь
│   │   ├── Slab.cs                # Перекрытие / Площадка
│   │   ├── StairFlight.cs         # Лестничный марш
│   │   ├── Room.cs                # Помещение
│   │   ├── Background.cs          # Подложка
│   │   ├── ProjectSettings.cs     # Глобальные настройки (mesh size, T_END, ...)
│   │   └── Enums.cs               # Direction, MaterialType, SnapMode, ...
│   ├── Generation/
│   │   ├── FdsGenerator.cs        # Building → .fds текст
│   │   ├── MeshGenerator.cs       # Auto-mesh: bounding box → MESH namelist
│   │   ├── StairGenerator.cs      # StairFlight → серия OBST (ступени)
│   │   └── RoomDetector.cs        # Flood-fill: стены → замкнутые помещения
│   ├── Import/
│   │   ├── FdsParser.cs           # .fds → Building (для открытия существующих)
│   │   └── NamelistTokenizer.cs   # Токенайзер namelist синтаксиса
│   ├── Export/
│   │   └── FdsWriter.cs           # Building → .fds (alias для FdsGenerator)
│   ├── Validation/
│   │   ├── BuildingValidator.cs   # Проверка перед запуском
│   │   └── IntersectionChecker.cs # Пересечения стен
│   ├── Services/
│   │   ├── FdsRunner.cs           # Запуск FDS subprocess
│   │   ├── SimulationMonitor.cs   # Мониторинг stdout
│   │   ├── SmvLauncher.cs         # Запуск SmokeView
│   │   └── ProjectService.cs      # New / Open / Save / SaveAs
│   └── Commands/
│       ├── ICommand.cs
│       ├── AddWallCommand.cs
│       ├── MoveObjectCommand.cs
│       ├── DeleteCommand.cs
│       ├── ChangePropertyCommand.cs
│       └── CommandHistory.cs      # Undo/Redo
│
├── FdsGui.Viewport/               # OpenTK 3D rendering
│   ├── FdsGui.Viewport.csproj
│   ├── Camera.cs                  # Orbit/Pan/Zoom (Blender-style)
│   ├── ShaderProgram.cs           # GLSL compilation
│   ├── MeshBuffer.cs              # VAO/VBO management
│   ├── Scene.cs                   # Scene graph
│   ├── GridOverlay.cs             # Grid plane + snap visualization
│   ├── Renderers/
│   │   ├── WallRenderer.cs        # Стены (solid boxes + wireframe)
│   │   ├── DoorRenderer.cs        # Двери (вырезы в стенах)
│   │   ├── SlabRenderer.cs        # Перекрытия (horizontal slabs)
│   │   ├── StairRenderer.cs       # Лестницы (ступени)
│   │   ├── RoomRenderer.cs        # Помещения (transparent volumes)
│   │   ├── BackgroundRenderer.cs  # Подложки (textured quads)
│   │   ├── SelectionRenderer.cs   # Выделение (outline glow + handles)
│   │   └── GhostRenderer.cs       # Preview объекта при рисовании
│   ├── Picking/
│   │   └── ObjectPicker.cs        # Ray casting для выбора объектов мышью
│   ├── Snapping/
│   │   └── SnapEngine.cs          # Grid/endpoint/midpoint/intersection snap
│   ├── Shaders/
│   │   ├── basic.vert / basic.frag
│   │   ├── lit.vert / lit.frag       # Phong lighting
│   │   ├── outline.vert / outline.frag # Selection glow
│   │   └── ghost.vert / ghost.frag   # Semi-transparent preview
│   └── Colormaps/                 # Для визуализации результатов (Фаза 6)
│       ├── ColormapGenerator.cs
│       └── ThermalColormap.cs
│
├── FdsGui.App/                    # Avalonia application (UI)
│   ├── FdsGui.App.csproj
│   ├── App.axaml
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── HierarchyViewModel.cs     # Дерево иерархии
│   │   ├── PropertyPanelViewModel.cs # Свойства выбранного объекта
│   │   ├── ViewportViewModel.cs      # 3D viewport
│   │   ├── ToolbarViewModel.cs       # Панель инструментов
│   │   ├── BackgroundViewModel.cs    # Редактор подложек
│   │   ├── FloorPropertiesViewModel.cs
│   │   └── SimulationViewModel.cs   # Запуск/мониторинг
│   ├── Views/
│   │   ├── MainWindow.axaml
│   │   ├── HierarchyView.axaml       # Панель иерархии (TreeView)
│   │   ├── PropertyPanelView.axaml   # Панель свойств
│   │   ├── ViewportView.axaml        # 3D viewport (host GLControl)
│   │   ├── BackgroundView.axaml      # Редактор подложек (орто вид)
│   │   ├── FloorPropertiesDialog.axaml
│   │   ├── ProjectSettingsView.axaml
│   │   └── SimulationView.axaml      # Прогресс расчёта
│   ├── Controls/
│   │   ├── GlViewportControl.cs      # OpenTK GLControl host
│   │   └── NumericInput.cs           # Числовой ввод с единицами
│   └── Converters/
│       └── ...
│
└── FdsGui.Tests/
    ├── FdsGui.Tests.csproj
    ├── Generation/
    │   ├── FdsGeneratorTests.cs
    │   ├── MeshGeneratorTests.cs
    │   └── RoomDetectorTests.cs
    ├── Import/
    │   └── FdsParserTests.cs
    └── TestData/
        ├── simple_room.fds
        └── multi_floor.fds
```

---

## DATA MODEL (C# классы)

```csharp
// === Building (root) ===
public class Building
{
    public string Name { get; set; } = "Новое здание";
    public string FilePath { get; set; } = "";
    public ProjectSettings Settings { get; set; } = new();
    public List<Floor> Floors { get; set; } = new();
}

// === Floor ===
public class Floor
{
    public string Name { get; set; } = "Этаж 1";
    public double Height { get; set; } = 3.0;          // Высота этажа (м)
    public double Level { get; set; } = 0.0;            // Нижняя отметка (м)
    public List<Wall> Walls { get; set; } = new();
    public List<Door> Doors { get; set; } = new();
    public List<Slab> Slabs { get; set; } = new();
    public List<StairFlight> Stairs { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<Background> Backgrounds { get; set; } = new();
}

// === Wall ===
public class Wall
{
    public string Name { get; set; } = "";
    // Геометрия на плане (линия стены)
    public Point2D Start { get; set; }
    public Point2D End { get; set; }
    // Свойства
    public double Thickness { get; set; } = 0.2;        // м (200мм)
    public double Height { get; set; } = 3.0;            // м
    public double BottomZ { get; set; } = 0.0;           // м
    public string Material { get; set; } = "CONCRETE";
    public int[] Rgb { get; set; } = new[] { 180, 180, 180 };

    // Вычисляется
    public double Length => Start.DistanceTo(End);
    public Point2D Direction => (End - Start).Normalized();
    public Point2D Normal => new(-Direction.Y, Direction.X);
    // XB для FDS (с учётом толщины и нормали)
    public double[] ToXb() { ... }
}

// === Door ===
public class Door
{
    public string Name { get; set; } = "";
    public Point2D Position { get; set; }               // Центр двери на плане
    public double Width { get; set; } = 0.9;             // м (900мм)
    public double Height { get; set; } = 2.1;            // м (2100мм)
    public double BottomZ { get; set; } = 0.0;
    public string ParentWallId { get; set; } = "";       // Стена, в которую врезана
    public double Rotation { get; set; } = 0.0;          // Поворот на плане (градусы)
}

// === Slab (Перекрытие / Площадка) ===
public class Slab
{
    public string Name { get; set; } = "";
    public Polygon2D Outline { get; set; }               // Контур на плане
    public double Thickness { get; set; } = 0.22;        // м (220мм)
    public double BottomZ { get; set; } = 0.0;
}

// === StairFlight (Лестничный марш) ===
public class StairFlight
{
    public string Name { get; set; } = "";
    public Point2D Start { get; set; }                   // Начало марша на плане
    public Point2D End { get; set; }                     // Конец марша на плане
    public double Height { get; set; } = 3.0;            // Высота подъёма
    public double BottomZ { get; set; } = 0.0;
    public double Width { get; set; } = 1.2;             // Ширина марша
    public Direction Direction { get; set; } = Direction.Up;
    public double StepHeight { get; set; } = 0.15;       // Высота ступени (м)
    public int StepCount => (int)Math.Ceiling(Height / StepHeight);
    // Генерирует серию OBST (ступеней) для FDS
    public List<double[]> GenerateSteps() { ... }
}

// === Room (Помещение) ===
public class Room
{
    public string Name { get; set; } = "";
    public Polygon2D Outline { get; set; }               // Контур помещения
    public double BottomZ { get; set; } = 0.0;
    public double TopZ { get; set; } = 3.0;
    public RoomCreationMode Mode { get; set; }           // Manual or Auto

    // Bounding box для ZONE
    public double[] ToXb() => new[] {
        Outline.MinX, Outline.MaxX,
        Outline.MinY, Outline.MaxY,
        BottomZ, TopZ
    };
}

// === Background (Подложка) ===
public class Background
{
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public double RealWidth { get; set; } = 10.0;         // Реальная ширина (м)
    public double RealHeight { get; set; } = 8.0;         // Реальная высота (м)
    public double Rotation { get; set; } = 0.0;           // Поворот (градусы)
    public double CorrectionAngle { get; set; } = 0.0;    // Коррекция искажения
    public double Opacity { get; set; } = 0.5;            // Прозрачность (0-1)
    public bool Visible { get; set; } = true;
    public Point2D Origin { get; set; } = new(0, 0);     // Позиция левого-нижнего угла
}

// === ProjectSettings ===
public class ProjectSettings
{
    public double MeshCellSize { get; set; } = 0.1;       // м
    public double SimulationTime { get; set; } = 600.0;   // с
    public double OutputInterval { get; set; } = 5.0;     // с
    public double AmbientTemperature { get; set; } = 20.0; // °C
    public string DefaultMaterial { get; set; } = "CONCRETE";
    public string FdsExecutablePath { get; set; } = "";
    public string SmokeViewPath { get; set; } = "";
    public string MpiExecPath { get; set; } = "";
    public int MpiProcesses { get; set; } = 1;
}

// === Утилиты ===
public record Point2D(double X, double Y)
{
    public double DistanceTo(Point2D other) => Math.Sqrt(
        Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
    public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public Point2D Normalized() { var len = Math.Sqrt(X*X + Y*Y); return new(X/len, Y/len); }
}

public class Polygon2D
{
    public List<Point2D> Points { get; set; } = new();
    public double MinX => Points.Min(p => p.X);
    public double MaxX => Points.Max(p => p.X);
    public double MinY => Points.Min(p => p.Y);
    public double MaxY => Points.Max(p => p.Y);
    public bool ContainsPoint(Point2D point) { ... }     // Point-in-polygon test
}

public enum Direction { Up, Down, Left, Right }
public enum RoomCreationMode { Manual, AutoFromWalls }
```

---

## ПОШАГОВЫЙ ПЛАН РЕАЛИЗАЦИИ

### Фаза 0: Скелет (1 день)

Создать solution, проекты, NuGet-зависимости. Пустое окно Avalonia с меню.

```bash
dotnet new sln -n FdsGui
dotnet new classlib -n FdsGui.Core
dotnet new classlib -n FdsGui.Viewport
dotnet new console -n FdsGui.App
dotnet new xunit -n FdsGui.Tests
# + зависимости Avalonia, OpenTK, CommunityToolkit.Mvvm
```

**Итог:** Пустое окно. Коммит.

---

### Фаза 1: Viewport + Камера + Grid (3-5 дней)

1. OpenTK GLControl внутри Avalonia (NativeControlHost)
2. Орбитальная камера (Blender-style: СКМ=orbit, Shift+СКМ=pan, колёсико=zoom)
3. Grid plane с настраиваемым шагом
4. Оси XYZ (RGB)
5. Кнопки toolbar камеры (Pan, Zoom+, Zoom-, Rotate, Iso, Top, Front, Right)
6. Переключение Perspective ↔ Ortho (Numpad 5)
7. Fit All (Home)

**Итог:** Пустой 3D viewport с полным управлением камерой.

---

### Фаза 2: Рисование стен + Snap (5-7 дней)

1. Snap engine (grid, endpoint, midpoint, intersection)
2. F4 toggle snap ON/OFF
3. Инструмент «Стена»: клик → drag → отпускание. Preview-линия при drag.
4. Wall renderer: 3D box из линии × толщина × высота
5. Selection: ЛКМ выбор стены, подсветка, handles
6. Move (G): перемещение стены (Blender-style)
7. Rotate (R), Mirror (⇔⇕) для выбранной стены
8. Delete (X/Delete)
9. Undo/Redo (Ctrl+Z / Ctrl+Shift+Z)
10. Property panel: толщина, высота, нижний Z, материал, цвет

**Итог:** Можно нарисовать план здания из стен.

---

### Фаза 3: Двери, Перекрытия, Лестницы (5-7 дней)

1. **Дверь:** клик на стену → врезка. Door renderer. Property: ширина, высота, Z.
2. **Перекрытие:** рисование прямоугольника на плане. Slab renderer. Property: толщина, Z.
3. **Лестница:** рисование линии марша. Stair renderer (авто-ступени). Property: высота, Z, ширина, направление. Кнопка «Сменить направление».
4. Поворот/отражение для всех объектов.

**Итог:** Полный набор строительных инструментов.

---

### Фаза 4: Этажи + Иерархия + Помещения (5-7 дней)

1. **Hierarchy panel (TreeView):** Здание → Этаж → Стены/Двери/...
2. **ПКМ по зданию:** Переименовать, Добавить этаж, Свойства (placeholder).
3. **ПКМ по этажу:** Свойства (высота, уровень), Добавить подложку, Переименовать.
4. **Новый этаж** ставится точно над верхним существующим.
5. **Видимость:** выбран 1 этаж → виден только он. Shift → несколько.
6. **Помещения:**
   - Кнопка «Произвольная область» — кликать точки, полигон.
   - Кнопка «Авто по стенам» — flood-fill внутри замкнутого контура стен. Двери игнорируются.
7. Room renderer (полупрозрачный объём).

**Итог:** Полноценный многоэтажный редактор.

---

### Фаза 5: Подложки (3-5 дней)

1. ПКМ по этажу → «Добавить подложку» → выбор файла.
2. Background view (вкладка рядом с viewport): ортогональный вид сверху.
3. Свойства подложки: реальные размеры, поворот, угол коррекции, прозрачность.
4. Кнопка 🖼️ Вкл/Выкл подложку в основном viewport.
5. Background renderer (textured quad на Z = уровень этажа).

**Итог:** Можно загрузить план из CAD и трассировать стены поверх.

---

### Фаза 6: Генерация .fds + Запуск FDS (3-5 дней)

1. **FdsGenerator:** Building → текст `.fds`
   - Стена → `&OBST` с правильным XB
   - Дверь → `&HOLE`
   - Перекрытие → `&OBST`
   - Лестница → серия `&OBST`
   - Помещение → `&ZONE`
   - Auto-mesh: bounding box здания → `&MESH` с IJK
2. **Валидация:** замкнутые помещения, пересечения стен, наличие mesh.
3. **FdsRunner:** запуск FDS subprocess, progress bar, лог.
4. **SmvLauncher:** запуск SmokeView для просмотра результатов.

**Итог:** Нарисовал здание → нажал ▶ → FDS считает → SmokeView показывает результат.

---

### Фаза 7: Импорт существующих .fds (3-5 дней)

1. **NamelistTokenizer** — разбиение .fds на namelist'ы.
2. **FdsParser** — OBST/VENT/HOLE → Wall/Door/Slab (обратное преобразование).
3. **Roundtrip test:** открыть .fds → отредактировать → сохранить → запустить.

**Итог:** Можно открыть существующий .fds файл и редактировать его визуально.

---

### Фаза 8: Визуализация результатов (10-20 дней)

**Предварительное условие:** Глубокое изучение исходного кода SmokeView (`Source/smv/`).

1. **Изучить smv source:**
   - Парсеры: ReadSMV, ReadSlice, ReadSmoke3D, ReadBoundary, ReadIso
   - Рендеринг: DrawSmoke, DrawSlice, DrawIso, DrawBoundary
   - Colormap'ы и colorbar
   - Timeline / playback

2. **Реализовать в GUI (C# + OpenGL):**
   - SmvParser (C#) — чтение .smv файла
   - SliceReader — чтение .sf, рендер slice plane с colormap текстурой
   - Smoke3DReader — чтение .s3d (RLE decompression)
   - SmokeVolumeRenderer — volume rendering (billboard particles или ray marching)
   - IsosurfaceRenderer — чтение .iso, отрисовка триангулированных поверхностей
   - BoundaryReader — чтение .bf, отрисовка данных на стенах
   - Timeline controls: play/pause/scrub/скорость
   - Colorbar overlay

3. **Сначала:** использовать внешний SmokeView (SmvLauncher).
4. **Потом:** постепенная замена на встроенный viewer.

**Итог:** Полностью встроенная визуализация результатов.

---

### Фаза 9: Полировка (5-10 дней)

- Автосохранение
- Drag & drop файлов .fds в окно
- Шаблоны проектов (квартира, офис, склад)
- Undo/Redo: полная история
- Copy/Paste между этажами
- Экспорт в другие форматы
- Горячие клавиши: справка (F1)
- Туториал для нового пользователя

---

## ИТОГО

| Фаза | Дней | Результат |
|-------|------|-----------|
| 0. Скелет | 1 | Пустое окно |
| 1. Viewport + камера | 3-5 | 3D навигация |
| 2. Стены + snap | 5-7 | Рисование стен |
| 3. Двери, перекрытия, лестницы | 5-7 | Полный набор инструментов |
| 4. Этажи + иерархия + помещения | 5-7 | Многоэтажный редактор |
| 5. Подложки | 3-5 | Трассировка по планам |
| 6. Генерация .fds + запуск FDS | 3-5 | End-to-end: от рисунка до расчёта |
| 7. Импорт .fds | 3-5 | Открытие существующих файлов |
| 8. Визуализация результатов | 10-20 | Встроенный SmokeView |
| 9. Полировка | 5-10 | Production quality |
| **Всего** | **43-72** | **Полноценный GUI** |

---

## ПОРЯДОК ДЕЙСТВИЙ

1. **Начать с Фазы 0** — скелет проекта.
2. **Каждая фаза = рабочий прототип.** Коммитить после каждой.
3. **Тесты:** unit tests для генераторов и парсеров.
4. **SmokeView source:** начать изучать параллельно с Фазой 2-3, чтобы к Фазе 8 быть готовым.
5. **Тестировать на реальных задачах:** после Фазы 6 — попробовать нарисовать реальное здание и запустить FDS.

---

## ССЫЛКИ

- [Avalonia UI](https://docs.avaloniaui.net/)
- [OpenTK](https://opentk.net/learn/index.html)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [FDS GitHub](https://github.com/firemodels/fds)
- SmokeView source: `Source/smv/`
- FDS5 source: `Source/fds5/`

---

**Начни с Фазы 0. Каждый шаг — рабочий прототип. GUI должен быть простым и понятным.**

**Никаких параллельных чтений и поисков. Строго последовательное чтение и поиск.**