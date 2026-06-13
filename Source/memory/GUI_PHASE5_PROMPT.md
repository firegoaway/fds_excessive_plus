# Промпт для кодера: Реализация Фазы 5 — Подложки (Backgrounds)

**Дата:** 2026-06-13
**Предыдущая фаза:** Фаза 4 (Этажи + Иерархия + Помещения) — ✅ завершена (commit `7888c02022`)

---

## КОНТЕКСТ

Ты продолжаешь работу над **FDS Building Editor** — GUI-редактором зданий для FDS.

**Ключевой документ:** `Source/memory/GUI_DEVELOPMENT_PROMPT.md` — архитектурная документация (~1170 строк). **Обязательно прочитай весь документ перед началом работы.**

---

## ТЕКУЩЕЕ СОСТОЯНИЕ

### ✅ Завершено (Фазы 0-4)

- **Фаза 0:** Скелет .NET 8 solution (4 проекта: Core, Viewport, App, Tests)
- **Фаза 1:** OpenGL viewport + камера (Blender-style) + сетка + оси XYZ
- **Фаза 2:** Стены + Snap to Grid + Selection + Move/Rotate/Mirror/Delete + Undo/Redo + PropertyPanel
- **Фаза 3:** Двери (клик на стену), Перекрытия (drag), Лестницы (drag + ступени) + рендеринг + picking + selection
- **Фаза 4:** Multi-floor (Add/Remove/Duplicate), динамический TreeView, ПКМ контекстные меню, видимость этажей, Room tool (M), Room renderer, Room picking, RoomDetector

**Запуск:**
```bash
dotnet run --project Source/fds5/GUI/FdsGui.App/FdsGui.App.csproj
```

**Что работает сейчас:**
- Toolbar: Select, Wall(W), Door(D), Slab(P), Stair(L), Room(M), Move(G), Rotate(R), Mirror H/V, Snap, Grid+/-
- TreeView (динамический): Building → Floor → Walls/Doors/Slabs/Stairs/Rooms (с counts)
- ПКМ по Building: Add Floor, Rename
- ПКМ по Floor: Properties (height, level), Rename, Duplicate, Delete
- Кнопки: "+" (Add Floor), "Show All" (все этажи)
- Клик на этаж = активный, Shift+клик = toggle видимости, Ctrl+A = все
- Room tool (M): клики → полигон → Enter замкнуть → Esc отмена
- Property panel: Wall/Door/Slab/Stair/Room секции
- Status bar: Floor info, Walls/Doors/Slabs/Stairs/Rooms counts, Grid info

---

## ЗАДАЧА: ФАЗА 5 — Подложки (Backgrounds)

### Что такое подложка

**Подложка (Background)** — это изображение плана этажа (PNG, JPG, BMP), загруженное из файла и отображаемое как полупрозрачный фон под объектами строительства. Пользователь загружает план из CAD/архитектурного чертежа и трассирует стены поверх него.

### Существующая модель (уже есть в коде)

```csharp
// FdsGui.Core/Models/Background.cs — УЖЕ СУЩЕСТВУЕТ
public class Background
{
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public double RealWidth { get; set; } = 10.0;     // Реальная ширина (м)
    public double RealHeight { get; set; } = 8.0;     // Реальная высота (м)
    public double Rotation { get; set; } = 0.0;       // Поворот (градусы)
    public double CorrectionAngle { get; set; } = 0.0;// Коррекция искажения
    public double Opacity { get; set; } = 0.5;        // Прозрачность (0-1)
    public bool Visible { get; set; } = true;
    public Point2D Origin { get; set; } = new(0, 0); // Позиция левого-нижнего угла
}

// Floor.cs — УЖЕ содержит:
public List<Background> Backgrounds { get; set; } = new();
```

### Требования

1. **Загрузка файла:** ПКМ по этажу → «Добавить подложку» → OpenFileDialog → выбор PNG/JPG/BMP
2. **Рендеринг:** textured quad на Z = уровень этажа (чуть ниже, Z = Level - 0.01, чтобы не z-fight)
3. **Texture loading:** загрузка изображения → OpenGL texture (GL.GenTexture, GL.TexImage2D)
4. **Свойства подложки:** RealWidth, RealHeight, Origin (X/Y offset), Rotation, Opacity, Visible
5. **TreeView:** узел «🖼️ Backgrounds (N)» под каждым этажом с дочерними подложками
6. **PropertyPanel:** секция для Background (все свойства)
7. **Selection/Picking:** ЛКМ на подложку → selection (outline)
8. **Toggle:** кнопка «🖼️ BG» в toolbar — показать/скрыть ВСЕ подложки
9. **Move:** подложку можно перемещать мышью (G)
10. **Delete:** подложку можно удалить (X)

---

## ЧТО НУЖНО РЕАЛИЗОВАТЬ

### 1. BackgroundRenderer — ПРИОРИТЕТ 1

**Новый файл:** `FdsGui.Viewport/Renderers/BackgroundRenderer.cs`

**Задача:** рендерить textured quad (прямоугольник с текстурой) на уровне этажа.

**Подход:**
- Каждый Background имеет OpenGL texture ID (загружается один раз)
- Quad = 4 вершины (bottom-left, bottom-right, top-right, top-left) на Z = floor.Level - 0.01
- Размер quad = RealWidth × RealHeight, позиция = Origin
- Поворот: вращать 4 вершины вокруг Origin на Rotation градусов
- Opacity: alpha в цвете вершин
- **Важно:** нужен separate render pass для текстур (shader с sampler2D) ИЛИ использовать vertex colors (простой подход — quad без текстуры, только цветной полупрозрачный прямоугольник)

**Упрощённый MVP-подход (рекомендуется для начала):**
- Вместо текстуры → цветной полупрозрачный прямоугольник (как Slab но с другим цветом)
- Это проще: не нужен texture loading, sampler2D, separate shader
- Позже можно добавить texture loading

**Для MVP:**
```
BackgroundRenderer.GenerateVertices(Background bg, float floorLevel) → float[]
// 4 вершины прямоугольника с RGBA цветом
// R,G,B = 0.8, 0.8, 0.8 (серый placeholder)
// A = bg.Opacity
```

**Для полной реализации (texture):**
- Нужен отдельный shader с `uniform sampler2D uTexture`
- TexCoord в вершинах: (0,0), (1,0), (1,1), (0,1)
- Texture loading: `System.Drawing.Common` или `ImageSharp` NuGet для чтения PNG
- GL.TexImage2D для загрузки в GPU
- GL.BindTexture перед рендерингом

### 2. Texture Loading — ПРИОРИТЕТ 2 (опционально для MVP)

**NuGet пакет:** `SixLabors.ImageSharp` (cross-platform, MIT) или `System.Drawing.Common`

```csharp
// Загрузка изображения в byte[]
using var image = Image.Load<Rgba32>(background.ImagePath);
byte[] pixels = new byte[image.Width * image.Height * 4];
image.CopyPixelDataTo(pixels);
// → GL.TexImage2D(...)
```

**Texture cache:** Dictionary<string, int> для path → GL texture ID, чтобы не перезагружать.

### 3. EditorState — ПРИОРИТЕТ 3

**Модификации:**
- `SelectedBackgrounds: HashSet<Background>`
- `SelectBackground(Background? bg, bool addToSelection)`
- `SingleSelectedBackground` property
- `VisibleBackgrounds` — коллекция фонов с видимых этажей
- `BackgroundsDirty` flag
- Background picking через ObjectPicker.PickBackground

### 4. ObjectPicker.PickBackground — ПРИОРИТЕТ 4

**Добавить в ObjectPicker.cs:**
```csharp
public static Background? PickBackground(double screenX, double screenY, ...,
    IEnumerable<(Background bg, double floorLevel)> backgrounds)
{
    // Ray-AABB intersection с quad на floorLevel
    // AABB = Origin.X, Origin.X + RealWidth, Origin.Y, Origin.Y + RealHeight, floorLevel - 0.02, floorLevel
}
```

### 5. GlViewportControl — ПРИОРИТЕТ 5

**Модификации:**
- `_roomVao` → добавить `_bgVao, _bgVbo, _bgEbo` для background rendering
- `RenderBackgrounds()` метод — рендерить подложки из видимых этажей
- Render order: backgrounds → walls → doors → slabs → stairs → rooms → ghost → selection
- HandleSelectionClick: добавить PickBackground в priority chain
- Background selection outline (wireframe quad)

### 6. TreeView — ПРИОРИТЕТ 6

**Модификация RebuildTreeView() в MainWindow.axaml.cs:**
- Добавить `🖼️ Backgrounds (N)` узел под каждым этажом
- Каждый Background = дочерний узел с именем файла
- ПКМ по Background → Properties, Delete

### 7. PropertyPanel — ПРИОРИТЕТ 7

**Добавить в PropertyPanelViewModel:**
- `SelectedBackground`, `IsBackgroundSelected`
- Properties: BgName, BgRealWidth, BgRealHeight, BgOriginX, BgOriginY, BgRotation, BgOpacity, BgImagePath
- Partial methods для каждого свойства с undo

**Добавить в PropertyPanelView.axaml:**
- Секция «Background» с IsVisible binding

### 8. ПКМ → «Добавить подложку» — ПРИОРИТЕТ 8

**Модификация контекстного меню этажа:**
```csharp
// Добавить в floorMenu.Items:
floorMenu.Items.Add(CreateMenuItem("Add Background...", async (_, _) =>
{
    var dialog = new OpenFileDialog
    {
        Title = "Select background image",
        Filters = new List<FileDialogFilter>
        {
            new() { Name = "Images", Extensions = { "png", "jpg", "jpeg", "bmp" } }
        }
    };
    var result = await dialog.ShowDialog(this);
    if (result != null && result.Length > 0)
    {
        var bg = new Background
        {
            Name = Path.GetFileName(result[0]),
            ImagePath = result[0],
            // Auto-detect size or use defaults
        };
        floor.Backgrounds.Add(bg);
        RebuildTreeView();
    }
}));
```

### 9. Toolbar Toggle — ПРИОРИТЕТ 9

**Добавить кнопку «🖼️ BG» в toolbar:**
- Toggle: показать/скрыть все подложки
- Состояние: `EditorState.ShowBackgrounds` (bool)
- Когда false → RenderBackgrounds() не вызывается

---

## ПОРЯДОК ДЕЙСТВИЙ

1. **Прочитай `GUI_DEVELOPMENT_PROMPT.md`** — весь документ
2. **Прочитай все ключевые файлы:** EditorState.cs, GlViewportControl.cs, MainWindow.axaml.cs, ObjectPicker.cs, SlabRenderer.cs (как пример), PropertyPanelViewModel.cs
3. **Запусти приложение** и убедись что всё работает
4. **Реализуй в таком порядке:**
   - `EditorState`: SelectedBackgrounds, SelectBackground, SingleSelectedBackground, BackgroundsDirty, ShowBackgrounds
   - `ObjectPicker.PickBackground`
   - `BackgroundRenderer` (MVP: цветной quad без текстуры)
   - `GlViewportControl`: RenderBackgrounds(), bg VAO/VBO/EBO, HandleSelectionClick с PickBackground
   - `MainWindow.axaml.cs`: ПКМ «Add Background...», TreeView Backgrounds узлы
   - `PropertyPanelViewModel`: Background секция
   - `PropertyPanelView.axaml`: Background UI
   - Toolbar toggle button «BG»
   - Background Move/Delete support
   - (Опционально) Texture loading с ImageSharp
5. **Тестируй после каждого шага**
6. **Коммить часто**

---

## КРИТЕРИИ ПРИЁМКИ

- [ ] ПКМ по этажу → «Add Background...» → OpenFileDialog → файл выбран
- [ ] Подложка отображается в viewport (цветной quad или texture)
- [ ] Подложка полупрозрачная (Opacity)
- [ ] Можно выбрать подложку ЛКМ (picking)
- [ ] PropertyPanel показывает свойства Background (имя, RealWidth, RealHeight, Origin, Rotation, Opacity)
- [ ] Изменение свойств в PropertyPanel обновляет подложку
- [ ] TreeView показывает «🖼️ Backgrounds (N)» с дочерними узлами
- [ ] Кнопка «BG» в toolbar toggle видимость всех подложек
- [ ] Подложку можно удалить (Delete/X)
- [ ] Подложку можно переместить (Move/G)
- [ ] Render order: backgrounds под стенами
- [ ] Приложение запускается без ошибок
- [ ] Все изменения закоммичены

---

## ТЕХНИЧЕСКИЕ ПОДСКАЗКИ

### Texture Loading (ImageSharp)

```bash
dotnet add FdsGui.Viewport/FdsGui.Viewport.csproj package SixLabors.ImageSharp
```

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static int LoadTexture(string path)
{
    using var image = Image.Load<Rgba32>(path);
    int texId = GL.GenTexture();
    GL.BindTexture(TextureTarget.Texture2D, texId);

    var pixels = new byte[image.Width * image.Height * 4];
    image.CopyPixelDataTo(pixels);

    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
        image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

    GL.TexParameter(TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
    GL.TexParameter(TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    GL.TexParameter(TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
    GL.TexParameter(TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

    return texId;
}
```

### Textured Quad Shader (для полной реализации)

```glsl
// Vertex
#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
uniform mat4 uMVP;
void main() { gl_Position = uMVP * vec4(aPos, 1.0); TexCoord = aTexCoord; }

// Fragment
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform float uOpacity;
void main() { FragColor = texture(uTexture, TexCoord) * vec4(1.0, 1.0, 1.0, uOpacity); }
```

**ВАЖНО:** Текущий шейдер (в GlViewportControl) использует `in vec4 aColor` (location=1). Для текстуры нужен другой layout. Решение: либо отдельный shader program для текстур, либо MVP-подход без текстур (цветной quad).

### MVP-подход без текстур

Для первой итерации можно НЕ загружать текстуру, а рисовать подложку как полупрозрачный прямоугольник с цветом на основе изображения (или просто серый). Это полностью совместимо с текущим шейдером (xyz + rgba формат вершин).

```csharp
// BackgroundRenderer: 4 вершины quad
public static float[] GenerateBackgroundVertices(Background bg, double floorLevel)
{
    double z = floorLevel - 0.01; // чуть ниже чтобы не z-fight
    float r = 0.7f, g = 0.85f, b = 0.7f; // светло-зелёный placeholder
    float a = (float)bg.Opacity;

    double x1 = bg.Origin.X, y1 = bg.Origin.Y;
    double x2 = bg.Origin.X + bg.RealWidth, y2 = bg.Origin.Y + bg.RealHeight;

    // TODO: применить Rotation вокруг Origin

    return [
        (float)x1, (float)y1, (float)z, r, g, b, a,
        (float)x2, (float)y1, (float)z, r, g, b, a,
        (float)x2, (float)y2, (float)z, r, g, b, a,
        (float)x1, (float)y2, (float)z, r, g, b, a,
    ];
}

// Indices: 2 triangles = quad
public static ushort[] GetQuadIndices() => [0, 1, 2, 0, 2, 3];
```

### Render Order

В `OnOpenGlRender`:
```csharp
RenderBackgrounds();  // ПОД объектами
RenderWalls();
RenderDoors();
RenderSlabs();
RenderStairs();
RenderRooms();
RenderGhost();
RenderSelection();
```

---

## ИЗВЕСТНЫЕ ПРОБЛЕМЫ И РЕШЕНИЯ

### Z-fighting подложки с grid
**Решение:** Z подложки = floor.Level - 0.01 (чуть выше grid, но ниже стен)

### Texture не загружается
**Решение для MVP:** не использовать текстуру, рисовать цветной quad. Texture loading добавить позже.

### Avalonia OpenFileDialog
```csharp
var dialog = new OpenFileDialog();
dialog.Filters.Add(new FileDialogFilter { Name = "Images", Extensions = { "png", "jpg", "bmp" } });
var result = await dialog.ShowDialog(this); // requires Window reference
```

### ImageSharp vs System.Drawing.Common
**ImageSharp** — cross-platform, MIT, рекомендуется.
**System.Drawing.Common** — Windows-only в .NET 6+, НЕ рекомендуется.

---

**Удачи! Фаза 5 позволяет трассировать стены поверх реальных планов этажей.**
