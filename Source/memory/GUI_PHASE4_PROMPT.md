# Промпт для кодера: Реализация Фазы 4 — Этажи, Иерархия, Помещения

**Дата:** 2026-06-13
**Предыдущая фаза:** Фаза 3 (Двери, Перекрытия, Лестницы) — ✅ завершена

---

## КОНТЕКСТ

Ты продолжаешь работу над **FDS Building Editor** — GUI-редактором зданий для FDS (Fire Dynamics Simulator).

**Ключевой документ:** `Source/memory/GUI_DEVELOPMENT_PROMPT.md` — полная архитектурная документация (~1150 строк), содержащая философию, стек, domain models, план реализации по фазам (0-9), примеры кода, и подробный статус каждой фазы. **Обязательно прочитай весь документ перед началом работы.**

---

## ТЕКУЩЕЕ СОСТОЯНИЕ

### ✅ Завершено (Фазы 0-3)

- **Фаза 0:** Скелет проекта — .NET 8 solution с 4 проектами (Core, Viewport, App, Tests)
- **Фаза 1:** OpenGL viewport + камера (Blender-style) + сетка + оси XYZ
- **Фаза 2:** Стены + Snap to Grid + Selection + Move/Rotate/Mirror/Delete + Undo/Redo + PropertyPanel
- **Фаза 3:** Двери (клик на стену), Перекрытия (drag прямоугольник), Лестницы (drag + авто-ступени) + рендеринг + picking + selection + Move + PropertyPanel для всех типов

**Запуск:**
```bash
dotnet run --project Source/fds5/GUI/FdsGui.App/FdsGui.App.csproj
```

**Что работает сейчас:**
- Меню (File, Edit, View, Simulate, Help)
- Toolbar (Select, Wall, Door, Slab, Stair, Move, Rotate, Mirror H/V, Snap, Grid+/-)
- Панель иерархии (слева) — статическое дерево: Building > Floor 1 > Walls/Doors/Slabs/Stairs/Rooms
- 3D viewport (центр) — можно рисовать стены, двери, перекрытия, лестницы
- Панель свойств (справа) — показывает свойства выбранного объекта (Wall/Door/Slab/Stair)
- Status bar — счётчики Walls/Doors/Slabs/Stairs

### ⚠️ Проблемы текущей реализации, которые нужно решить в Фазе 4

1. **Иерархия статическая** — TreeView в MainWindow.axaml захардкожен. Не обновляется при добавлении/удалении объектов.
2. **Только один этаж** — `ActiveFloor` всегда `Building.Floors[0]`. Нет UI для добавления/переключения этажей.
3. **Нет помещений** — модель `Room` существует, но нет инструмента для создания.
4. **Нет Z-offset** — все объекты рисуются на Z=0 (уровень первого этажа). Нет привязки к уровню этажа.
5. **Нет ПКМ-меню** — контекстные меню для здания и этажа не реализованы.

---

## ЗАДАЧА: ФАЗА 4 — Этажи + Иерархия + Помещения

### Требования из `GUI_DEVELOPMENT_PROMPT.md` (раздел "Фаза 4")

1. **Hierarchy panel (TreeView):** Здание → Этаж → Стены/Двери/Перекрытия/Лестницы/Помещения — **динамическое** дерево, обновляется при изменениях
2. **ПКМ по зданию:** Переименовать, Добавить этаж, Свойства (placeholder)
3. **ПКМ по этажу:** Свойства (высота, уровень), Добавить подложку (placeholder), Переименовать, Дублировать, Удалить
4. **Новый этаж** ставится точно над верхним существующим (Level = prev.Level + prev.Height)
5. **Видимость:** выбран 1 этаж → виден только он. Shift → несколько. Ctrl+A → все этажи
6. **Помещение — произвольная область:** кликать точки мышью, формируя полигон. Замыкание = двойной клик или Enter
7. **Помещение — авто по стенам:** клик внутри замкнутого контура стен → flood-fill находит область. Двери игнорируются
8. **Room renderer:** полупрозрачный объём (box или extruded polygon)
9. **Room selection/picking**
10. **Property panel:** свойства помещения (имя, BottomZ, TopZ, Mode)

### Существующие модели (уже есть в `FdsGui.Core/Models/`)

```csharp
// Room.cs — уже существует
public class Room
{
    public string Name { get; set; } = "";
    public Polygon2D Outline { get; set; } = new();
    public double BottomZ { get; set; } = 0.0;
    public double TopZ { get; set; } = 3.0;
    public RoomCreationMode Mode { get; set; } = RoomCreationMode.Manual;
    public double[] ToXb() => [Outline.MinX, Outline.MaxX, Outline.MinY, Outline.MaxY, BottomZ, TopZ];
}

// Enums.cs — уже содержит:
public enum RoomCreationMode { Manual, AutoFromWalls }

// Floor.cs — уже содержит:
public class Floor
{
    public string Name { get; set; } = "Этаж 1";
    public double Height { get; set; } = 3.0;
    public double Level { get; set; } = 0.0;
    public List<Wall> Walls { get; set; } = new();
    public List<Door> Doors { get; set; } = new();
    public List<Slab> Slabs { get; set; } = new();
    public List<StairFlight> Stairs { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<Background> Backgrounds { get; set; } = new();
}
```

---

## ЧТО НУЖНО РЕАЛИЗОВАТЬ

### 1. Динамическая иерархия (TreeView) — ПРИОРИТЕТ 1

**Текущее состояние:** TreeView в `MainWindow.axaml` полностью статический — захардкоженные TreeViewItem'ы.

**Что нужно:**
- Создать `HierarchyViewModel` (ObservableObject) с реактивным деревом
- Каждый узел дерева = `HierarchyNode` с: Header, Icon, Children (ObservableCollection), Tag (object reference)
- Корень: Building → ObservableCollection<FloorNode> → для каждого Floor: WallsNode, DoorsNode, SlabsNode, StairsNode, RoomsNode
- Подписки на события EditorState (SelectionChanged, + новые: FloorAdded, FloorRemoved, ObjectAdded, ObjectRemoved)
- Двойной клик на узле объекта → выбрать этот объект в viewport
- Клик на узле этажа → сделать его ActiveFloor
- Shift+клик → добавить к видимым этажам

**Новые файлы:**
```
FdsGui.App/
├── ViewModels/
│   └── HierarchyViewModel.cs     # ViewModel для дерева иерархии
├── Views/
│   └── HierarchyView.axaml       # UserControl с TreeView (или встроить в MainWindow)
│   └── HierarchyView.axaml.cs
```

**Альтернативный подход (проще):** Вместо отдельного UserControl, обновлять TreeView программно в MainWindow.axaml.cs:
- Подписаться на события EditorState
- При изменении — перестраивать TreeView items
- Выбрать этот подход если отдельный UserControl слишком сложно

### 2. Многоэтажность — ПРИОРИТЕТ 2

**Что нужно:**
- `EditorState.ActiveFloor` → `EditorState.ActiveFloors` (список видимых этажей) или `SelectedFloors`
- Кнопка «Добавить этаж» (toolbar или ПКМ по зданию)
- При добавлении: `Level = previousFloor.Level + previousFloor.Height`, `Height = 3.0`
- Кнопка «Удалить этаж» (только если этажей > 1)
- Все операции рисования (стена/дверь/перекрытие/лестница) используют `ActiveFloor.Level` как `BottomZ`

**Модификация EditorState:**
```csharp
// Было:
public Floor ActiveFloor => Building.Floors[0];

// Стало:
public int ActiveFloorIndex { get; set; } = 0;
public Floor ActiveFloor => Building.Floors[ActiveFloorIndex];
public HashSet<int> VisibleFloorIndices { get; } = new() { 0 };

public void AddFloor() { ... }
public void RemoveFloor(int index) { ... }
public void SetActiveFloor(int index) { ... }
```

### 3. Контекстные меню (ПКМ) — ПРИОРИТЕТ 3

**ПКМ по зданию (корень дерева):**
- Переименовать → inline edit или диалог
- Добавить этаж
- Свойства (placeholder → MessageBox или пустое окно)

**ПКМ по этажу:**
- Переименовать
- Свойства (высота этажа, уровень) → простой диалог или inline edit
- Добавить подложку (placeholder → заглушка, реализация в Фазе 5)
- Дублировать этаж (копировать все объекты)
- Удалить этаж (только если этажей > 1)

**Реализация:** Avalonia `ContextMenu` в AXAML:
```xml
<TreeViewItem Header="{Binding Name}">
    <TreeViewItem.ContextMenu>
        <ContextMenu>
            <MenuItem Header="Rename" Click="OnRenameFloor"/>
            <MenuItem Header="Properties..." Click="OnFloorProperties"/>
            <Separator/>
            <MenuItem Header="Duplicate" Click="OnDuplicateFloor"/>
            <MenuItem Header="Delete" Click="OnDeleteFloor"/>
        </ContextMenu>
    </TreeViewItem.ContextMenu>
</TreeViewItem>
```

### 4. Видимость этажей — ПРИОРИТЕТ 4

**Логика:**
- По умолчанию виден только активный этаж
- Клик на другой этаж в дереве → переключить активный этаж (виден только он)
- Shift+клик → toggle видимости этажа
- Ctrl+A → показать все этажи
- Рендеринг: рисовать только объекты из VisibleFloorIndices
- Z-offset: каждый этаж рисовать со своим Level (не все на Z=0)

### 5. Помещения (Room) — ПРИОРИТЕТ 5

#### Ручное рисование (Manual mode)

**Workflow:**
1. Кнопка `Room` или горячая клавиша (нет буквы — использовать `Shift+R` или `Ctrl+R`)
2. Первый клик → первая точка полигона
3. Последующие клики → добавление вершин
4. Двойной клик или Enter → замыкание полигона
5. ESC → отмена

**Геометрия:**
- Room = Polygon2D (произвольный полигон)
- Extrude от BottomZ до TopZ

**Рендеринг:**
- Полупрозрачный объём (alpha = 0.15)
- Цвет: голубой [100, 150, 255]
- Wireframe outline

#### Авто по стенам (AutoFromWalls mode)

**Workflow:**
1. Кнопка «Auto Room» или переключатель в Property Panel
2. Клик внутри замкнутого контура стен
3. Алгоритм flood-fill определяет замкнутую область
4. Создание Room с найденным полигоном

**Алгоритм (упрощённый):**
- Найти все стены на текущем этаже
- Построить граф: стены как рёбра, endpoint'ы как вершины
- Найти замкнутый контур, содержащий точку клика
- Двери считаются частью стены (не размыкают контур)

**Реализация RoomDetector:**
```
FdsGui.Core/
├── Generation/
│   └── RoomDetector.cs    # Flood-fill: стены → замкнутые помещения
```

### 6. Room renderer + picking + selection

**Новые файлы:**
```
FdsGui.Viewport/
├── Renderers/
│   └── RoomRenderer.cs    # Полупрозрачный extruded polygon
```

**Подход:**
- GenerateRoomVertices(Room room) → float[] для extruded polygon
- Top face + bottom face + side faces
- Alpha = 0.15 (полупрозрачность)
- Wireframe outline для selection

**ObjectPicker:**
- PickRoom → ray-polygon intersection (используя bounding box из ToXb())

---

## ПОРЯДОК ДЕЙСТВИЙ

1. **Прочитай `GUI_DEVELOPMENT_PROMPT.md`** — весь документ, особенно секции Фазы 0-3
2. **Прочитай все существующие файлы** (EditorState, GlViewportControl, MainWindow, PropertyPanelViewModel)
3. **Запусти приложение** и убедись, что всё работает
4. **Реализуй в таком порядке:**
   - Multi-floor support в EditorState (AddFloor, RemoveFloor, ActiveFloorIndex, VisibleFloorIndices)
   - Z-offset: все операции рисования используют ActiveFloor.Level
   - Динамический TreeView (HierarchyViewModel или программное обновление)
   - ПКМ контекстные меню для Building и Floor
   - Видимость этажей (клик, Shift+клик, рендеринг только видимых)
   - Room model extensions (если нужно)
   - AddRoomCommand
   - RoomRenderer (полупрозрачный extruded polygon)
   - Room tool (Manual): клики → полигон → Enter
   - ObjectPicker.PickRoom
   - Room selection
   - PropertyPanel для Room
   - RoomDetector (AutoFromWalls) — можно оставить на конец
   - Undo/Redo для всех операций с этажами
   - Move/Delete для Room
5. **Тестируй после каждого шага**
6. **Коммить часто**

---

## КРИТЕРИИ ПРИЁМКИ

- [ ] TreeView динамически обновляется при добавлении/удалении объектов
- [ ] Можно добавить новый этаж (кнопка или ПКМ)
- [ ] Новый этаж имеет Level = предыдущий.Level + предыдущий.Height
- [ ] Можно переключать активный этаж кликом в TreeView
- [ ] Объекты рисуются на правильном Z-уровне (с учётом ActiveFloor.Level)
- [ ] ПКМ по зданию → контекстное меню (Переименовать, Добавить этаж)
- [ ] ПКМ по этажу → контекстное меню (Переименовать, Свойства, Дублировать, Удалить)
- [ ] Можно удалить этаж (если этажей > 1)
- [ ] Видимость: по умолчанию виден только активный этаж
- [ ] Shift+клик на этаж → toggle видимости
- [ ] Рендеринг: видны только объекты из видимых этажей
- [ ] Room tool (Manual): клики → полигон → Enter → помещение создано
- [ ] Room отрисовывается как полупрозрачный объём
- [ ] Можно выбрать Room ЛКМ (picking)
- [ ] PropertyPanel показывает свойства Room (имя, BottomZ, TopZ)
- [ ] Delete (X) работает для Room
- [ ] Undo/Redo работает для всех новых операций
- [ ] ESC отменяет активный инструмент
- [ ] Приложение запускается без ошибок
- [ ] Все изменения закоммичены

---

## ТЕХНИЧЕСКИЕ ПОДСКАЗКИ

### Динамический TreeView

**Подход 1 (MVVM, рекомендуется):**
- HierarchyViewModel с ObservableCollection<HierarchyNode>
- Каждый HierarchyNode: `string Header`, `string Icon`, `ObservableCollection<HierarchyNode> Children`, `object? Tag`
- AXAML: `<TreeView ItemsSource="{Binding RootNodes}">` с `TreeDataTemplate`

**Подход 2 (Code-behind, проще):**
- В MainWindow.axaml.cs: метод `RebuildTreeView()`
- Вызывать при каждом изменении (добавление/удаление объекта/этажа)
- Программно создавать TreeViewItem'ы

### Z-offset для этажей

Текущий код: `wall.BottomZ = ActiveFloor.Level` — уже правильно!
Нужно убедиться, что все инструменты (Wall, Door, Slab, Stair) используют `ActiveFloor.Level`.

### Avalonia ContextMenu

```xml
<TreeViewItem.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Rename" Click="OnRename"/>
    </ContextMenu>
</TreeViewItem.ContextMenu>
```

### Горячие клавиши для Room

Нет свободной буквы → использовать `Shift+R` или выбрать другую:
- `Shift+R` = Room tool
- Или переключатель в toolbar без горячей клавиши

### Room extruded polygon rendering

Для простого прямоугольного Room (4 точки):
- 8 вершин (4 bottom + 4 top) = стандартный box, как Slab
- Для произвольного полигона (N точек):
  - 2N вершин (N bottom + N top)
  - Side faces: 2N треугольников (N quads)
  - Top/bottom: триангуляция (fan triangulation для выпуклых, ear clipping для вогнутых)

Для MVP можно ограничиться **прямоугольными** помещениями (как Slab) и добавить поддержку произвольных полигонов позже.

---

## ИЗВЕСТНЫЕ ПРОБЛЕМЫ И РЕШЕНИЯ

### MenuItem не находятся через GetVisualDescendants()
**Решение:** MenuItem в Avalonia 11.2 Menu находятся в **логическом** дереве. Использовать `Click=` обработчики прямо в AXAML.

### TreeView не обновляется
**Решение:** Использовать `ObservableCollection` для Children и подписки на события EditorState. Или вызывать `RebuildTreeView()` после каждого изменения.

### PropertyPanel recursion при программном обновлении
**Решение:** `_updating` flag в PropertyPanelViewModel (уже реализовано в Фазе 3).

### ActiveFloor всегда Floors[0]
**Решение:** Добавить `ActiveFloorIndex` property, изменить `ActiveFloor` getter на `Building.Floors[ActiveFloorIndex]`. Все операции рисования уже используют ActiveFloor.Level.

---

**Удачи! Фаза 4 превращает одноэтажный редактор в полноценный многоэтажный.**
