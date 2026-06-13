# Промпт для кодера: Диагностика и исправление проблем ввода в FDS Building Editor

**Дата:** 2026-06-13
**Предыдущая работа:** Фазы 0-5 реализованы, код компилируется, шейдеры работают
**Статус:** КРИТИЧЕСКАЯ ПРОБЛЕМА — UI не реагирует на ввод

---

## ОПИСАНИЕ ПРОБЛЕМЫ

Приложение **FDS Building Editor** (C# + Avalonia 11.2.7 + OpenTK 4.9.3 + .NET 8) запускается, но:

1. **OpenGL viewport работает:** видна world grid (сетка) и 3 оси XYZ (красная, зелёная, синяя)
2. **UI отображается:** toolbar сверху, TreeView слева, PropertyPanel справа
3. **КНОПКИ НЕ РАБОТАЮТ:** ни одна кнопка в toolbar не реагирует на клики
4. **VIEWPORT НЕ РЕАГИРУЕТ:** мышь/клавиатура не обрабатываются в viewport (нельзя нарисовать стену, покрутить камеру)
5. **Всё выглядит серым/уродливым** — пользователь описывает UI как "чёрно-серый, безжизненный"

---

## ЧТО МЫ УЖЕ ЗНАЕМ (диагностика проведена)

### ✅ Работает:
- **Шейдеры компилируются:** `#version 300 es` (исправлено с `330 core`)
- **OpenGL ES 3.0 (ANGLE):** `OpenGL ES 3.0.0 (ANGLE 2.1.22045 git hash: 4a177dd16290)`
- **OnOpenGlInit вызывается** — VAO/VBO создаются
- **OnOpenGlRender вызывается** — ~50 FPS
- **OnLoaded вызывается** — PropertyPanelView и GlViewportControl найдены
- **74 кнопки найдены** в visual tree (включая BtnSelect, BtnWall, BtnDoor и т.д.)
- **WireButtons завершается** — event handlers подключены
- **TreeView rebuild работает** — иерархия отображается

### ❌ Не работает:
- Кнопки не реагируют на клики (Click event не срабатывает?)
- Viewport не реагирует на мышь (PointerPressed/PointerMoved/PointerReleased?)
- Viewport не реагирует на клавиатуру (KeyDown?)
- UI выглядит "уродливым" (возможно тема не загружается правильно?)

---

## АРХИТЕКТУРА ПРИЛОЖЕНИЯ

### Структура solution:
```
Source/fds5/GUI/FdsGui.sln
├── FdsGui.Core/          (domain models, commands)
├── FdsGui.Viewport/      (OpenGL rendering, input handling)
│   ├── Camera.cs
│   ├── GridRenderer.cs
│   ├── EditorState.cs    (центральное состояние)
│   ├── Picking/ObjectPicker.cs
│   ├── Snapping/SnapEngine.cs
│   └── Renderers/        (Wall, Door, Slab, Stair, Room, Background)
├── FdsGui.App/           (Avalonia UI)
│   ├── Controls/GlViewportControl.cs  (OpenGlControlBase наследник)
│   ├── ViewModels/       (PropertyPanel, Toolbar)
│   └── Views/            (MainWindow, PropertyPanelView)
└── FdsGui.Tests/
```

### Ключевые файлы:
- **`GlViewportControl.cs`** (816 строк) — наследует `OpenGlControlBase`, обрабатывает GL init/render + mouse/keyboard input
- **`MainWindow.axaml.cs`** (351 строк) — wire buttons, TreeView, dialogs
- **`MainWindow.axaml`** (133 строк) — layout: Menu → Toolbar (StackPanel) → StatusBar → Grid (TreeView | Viewport | PropertyPanel)
- **`EditorState.cs`** (951 строк) — domain logic, selection, transforms

### Как подключены кнопки:
```csharp
// MainWindow.axaml.cs, WireButtons()
Find<Button>("BtnWall")!.Click += (_, _) => { Editor.SetTool(ToolMode.Wall); ToolbarVM.UpdateToolLabel(); };
// ... аналогично для других кнопок
```

### Как обрабатывается ввод в viewport:
```csharp
// GlViewportControl.cs
protected override void OnPointerPressed(PointerPressedEventArgs e) { ... }
protected override void OnPointerMoved(PointerEventArgs e) { ... }
protected override void OnPointerReleased(PointerReleasedEventArgs e) { ... }
protected override void OnKeyDown(KeyEventArgs e) { ... }
```

---

## ГИПОТЕЗЫ (что может быть не так)

### 1. **OpenGlControlBase перехватывает pointer events**
OpenGlControlBase в Avalonia 11.2 создаёт отдельный rendering surface. Возможно:
- Этот surface перехватывает все pointer events на уровне ОС
- Events не доходят до кнопок в toolbar (даже если toolbar выше в Z-order)
- **Проверка:** добавить `Debug.WriteLine` в `OnPointerPressed` GlViewportControl — вызывается ли он вообще?

### 2. **Click routed events не доходят до кнопок**
В Avalonia 11.x Click — это routed event с `RoutingStrategies.Bubble`. Возможно:
- Какой-то parent элемент перехватывает PointerPressed и не пробрасывает дальше
- **Проверка:** использовать `AddHandler(Button.ClickEvent, handler, RoutingStrategies.Tunnel)` вместо `Click +=`

### 3. **Z-order проблемы**
Возможно GlViewportControl перекрывает toolbar:
- OpenGlControlBase может иметь `ZIndex` или рендериться в отдельном слое
- **Проверка:** установить `GlViewportControl.IsHitTestVisible = false` временно

### 4. **Фокус не на правильном элементе**
Возможно окно не получает фокус:
- **Проверка:** добавить `this.Focus()` в `OnLoaded` MainWindow

### 5. **Avalonia backend проблемы**
Avalonia 11.2 на Windows может использовать WinUI3 backend:
- Возможно Click events работают по-другому в WinUI3
- **Проверка:** добавить `MessageBox.Show("Clicked")` в обработчик кнопки

### 6. **HiDPI проблемы**
Если у пользователя HiDPI монитор:
- Координаты мыши могут быть неправильными
- **Проверка:** убрать `app.manifest` или изменить `dpiAware`

### 7. **Тема не загружается**
Пользователь говорит UI "уродливый":
- Возможно FluentTheme не загружается правильно
- **Проверка:** добавить `<FluentTheme Mode="Dark" />` явно

### 8. **DispatcherTimer не работает**
В GlViewportControl конструкторе:
```csharp
DispatcherTimer.Run(() => { RequestNextFrameRendering(); return true; }, TimeSpan.FromMilliseconds(16));
```
Возможно это блокирует UI thread?

---

## РЕКОМЕНДУЕМЫЕ ШАГИ ДЛЯ ДИАГНОСТИКИ

### Шаг 1: Проверить что Click event срабатывает
Добавить в `MainWindow.axaml.cs` → `WireButtons()`:
```csharp
Find<Button>("BtnWall")!.Click += (_, _) => {
    System.Diagnostics.Debug.WriteLine("[CLICK] BtnWall clicked");
    System.Windows.Forms.MessageBox.Show("Wall button clicked!"); // если WinForms доступен
    // или
    var dialog = new Window { Title = "Test", Width = 200, Height = 100 };
    dialog.Content = new TextBlock { Text = "Button clicked!" };
    dialog.ShowDialog(this);
    
    Editor.SetTool(ToolMode.Wall);
    ToolbarVM.UpdateToolLabel();
};
```

### Шаг 2: Проверить что pointer events доходят до viewport
Добавить в `GlViewportControl.cs`:
```csharp
protected override void OnPointerPressed(PointerPressedEventArgs e)
{
    System.Diagnostics.Debug.WriteLine($"[POINTER] OnPointerPressed at {e.GetPosition(this)}");
    base.OnPointerPressed(e);
    // ... rest of code
}
```

### Шаг 3: Проверить Z-order
Временно добавить в `MainWindow.axaml`:
```xml
<controls:GlViewportControl Name="Viewport" ZIndex="-1"/>
```

### Шаг 4: Проверить фокус
Добавить в `MainWindow.axaml.cs` → `OnLoaded()`:
```csharp
this.Focus();
var viewport = this.GetVisualDescendants().OfType<GlViewportControl>().FirstOrDefault();
viewport?.Focus();
```

### Шаг 5: Упростить до минимума
Создать отдельный test проект:
- MainWindow с одной кнопкой
- Одна GlViewportControl
- Проверить работает ли кнопка + viewport

### Шаг 6: Проверить Avalonia версию
Возможно нужно обновить до Avalonia 11.3+ или откатиться до 11.1:
```bash
dotnet add package Avalonia --version 11.1.0
```

### Шаг 7: Проверить backend
Добавить в `Program.cs`:
```csharp
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } })
        .LogToTrace();
```

---

## ТЕХНИЧЕСКИЕ ДЕТАЛИ

### Версии пакетов:
- `Avalonia` 11.2.7
- `Avalonia.Desktop` 11.2.7
- `Avalonia.Themes.Fluent` 11.2.7
- `OpenTK` 4.9.3
- `CommunityToolkit.Mvvm` 8.4.0
- .NET 8.0

### Запуск:
```bash
dotnet run --project Source/fds5/GUI/FdsGui.App/FdsGui.App.csproj
```

### OpenGL контекст:
- **Версия:** OpenGL ES 3.0.0 (ANGLE)
- **Шейдеры:** `#version 300 es` (работают)
- **VAO/VBO:** создаются в `OnOpenGlInit`

### Логи (уже собраны):
- `OnOpenGlInit` вызывается ✅
- Шейдеры компилируются ✅
- `OnOpenGlRender` вызывается ~50 FPS ✅
- `OnLoaded` вызывается ✅
- Все кнопки найдены ✅
- `WireButtons` завершается ✅

---

## ПРИОРИТЕТ

**КРИТИЧЕСКИЙ** — приложение полностью нефункционально. Пользователь не может:
- Нарисовать стену
- Выбрать объект
- Повернуть камеру
- Использовать любую функцию

---

## КОНТЕКСТ

Это **Phase 5** проекта. Фазы 0-5 реализованы:
- Фаза 0: Скелет проекта
- Фаза 1: Viewport + камера + grid
- Фаза 2: Стены + snap + selection + transform
- Фаза 3: Двери, перекрытия, лестницы
- Фаза 4: Multi-floor + hierarchy + rooms
- Фаза 5: Подложки (Backgrounds)

**Коммит:** `7b56c90ca0` (Phase 5 code) + `7af430a4a3` (docs)

---

## ВОПРОСЫ ДЛЯ УТОЧНЕНИЯ У ПОЛЬЗОВАТЕЛЯ

1. Какая версия Windows? (10/11?)
2. Какой монитор? (HiDPI/4K?)
3. Есть ли другие Avalonia приложения которые работают?
4. Что значит "уродливый" UI? (скриншот?)
5. Пробовали ли запускать из Visual Studio с отладкой?

---

## ФИНАЛЬНАЯ ЗАДАЧА

1. **Диагностировать** корневую причину (pointer events? Click events? Z-order? фокус?)
2. **Исправить** проблему
3. **Протестировать** что:
   - Кнопки реагируют на клики
   - Viewport реагирует на мышь (рисование стен, камера)
   - Viewport реагирует на клавиатуру (W, D, G, R, X)
4. **Закоммитить** исправление

---

**Удачи! Это блокирующая проблема для всего проекта.**
