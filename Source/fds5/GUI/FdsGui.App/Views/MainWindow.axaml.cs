using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FdsGui.App.Controls;
using FdsGui.App.ViewModels;
using FdsGui.Core.Models;
using FdsGui.Viewport;

namespace FdsGui.App.Views;

public partial class MainWindow : Window
{
    public EditorState Editor { get; } = new();
    public PropertyPanelViewModel PropertyVM { get; private set; } = null!;
    public ToolbarViewModel ToolbarVM { get; private set; } = null!;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        PropertyVM = new PropertyPanelViewModel(Editor);
        ToolbarVM = new ToolbarViewModel(Editor);

        var propPanel = this.GetVisualDescendants().OfType<PropertyPanelView>().FirstOrDefault();
        if (propPanel != null)
            propPanel.DataContext = PropertyVM;

        var viewport = this.GetVisualDescendants().OfType<GlViewportControl>().FirstOrDefault();
        if (viewport != null)
            viewport.Editor = Editor;

        WireButtons();

        Editor.SelectionChanged += UpdateStatusBar;
        Editor.ToolChanged += UpdateStatusBar;
        Editor.HierarchyChanged += RebuildTreeView;

        RebuildTreeView();
        UpdateStatusBar();
    }

    private T? Find<T>(string name) where T : Control
    {
        return this.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
    }

    private void WireButtons()
    {
        Find<Button>("BtnSelect")!.Click += (_, _) => { Editor.SetTool(ToolMode.Select); ToolbarVM.UpdateToolLabel(); };
        Find<Button>("BtnWall")!.Click += (_, _) => { Editor.SetTool(ToolMode.Wall); ToolbarVM.UpdateToolLabel(); };
        Find<Button>("BtnDoor")!.Click += (_, _) => { Editor.SetTool(ToolMode.Door); ToolbarVM.UpdateToolLabel(); };
        Find<Button>("BtnSlab")!.Click += (_, _) => { Editor.SetTool(ToolMode.Slab); ToolbarVM.UpdateToolLabel(); };
        Find<Button>("BtnStair")!.Click += (_, _) => { Editor.SetTool(ToolMode.Stair); ToolbarVM.UpdateToolLabel(); };
        Find<Button>("BtnRoom")!.Click += (_, _) => { Editor.SetTool(ToolMode.Room); ToolbarVM.UpdateToolLabel(); };

        var viewport = this.GetVisualDescendants().OfType<GlViewportControl>().FirstOrDefault();
        Find<Button>("BtnMove")!.Click += (_, _) => viewport?.StartMove();
        Find<Button>("BtnRotate")!.Click += (_, _) => viewport?.StartRotate();

        Find<Button>("BtnMirrorH")!.Click += (_, _) => Editor.MirrorHorizontal();
        Find<Button>("BtnMirrorV")!.Click += (_, _) => Editor.MirrorVertical();
        Find<Button>("BtnSnapToggle")!.Click += (_, _) => ToolbarVM.ToggleSnap();
        Find<Button>("BtnGridPlus")!.Click += (_, _) => ToolbarVM.IncreaseGridStep();
        Find<Button>("BtnGridMinus")!.Click += (_, _) => ToolbarVM.DecreaseGridStep();

        Find<Button>("BtnAddFloor")!.Click += (_, _) => { Editor.AddFloor(); UpdateStatusBar(); };
        Find<Button>("BtnShowAllFloors")!.Click += (_, _) => { Editor.ShowAllFloors(); UpdateStatusBar(); };
        Find<Button>("BtnBgToggle")!.Click += (_, _) =>
        {
            Editor.ShowBackgrounds = !Editor.ShowBackgrounds;
            Editor.BackgroundsDirty = true;
            var btn = Find<Button>("BtnBgToggle");
            if (btn != null) btn.Content = Editor.ShowBackgrounds ? "BG" : "BG (off)";
        };
    }

    private void OnMenuUndo(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnMenuRedo(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void OnMenuDelete(object? sender, RoutedEventArgs e) => Editor.DeleteSelected();
    private void OnMenuSnapToggle(object? sender, RoutedEventArgs e) => ToolbarVM.ToggleSnap();

    // ===================== Dynamic TreeView =====================

    private void RebuildTreeView()
    {
        var tree = Find<TreeView>("HierarchyTree");
        if (tree == null) return;

        var buildingNode = new TreeViewItem
        {
            Header = $"🏢 {Editor.Building.Name}",
            IsExpanded = true
        };

        // Building context menu
        var buildingMenu = new ContextMenu();
        buildingMenu.Items.Add(CreateMenuItem("Add Floor", (_, _) => { Editor.AddFloor(); UpdateStatusBar(); }));
        buildingMenu.Items.Add(new Separator());
        buildingMenu.Items.Add(CreateMenuItem("Rename...", (_, _) => ShowRenameDialog(Editor.Building)));
        buildingNode.ContextMenu = buildingMenu;

        for (int fi = 0; fi < Editor.Building.Floors.Count; fi++)
        {
            var floor = Editor.Building.Floors[fi];
            int floorIndex = fi;

            var isActive = floorIndex == Editor.ActiveFloorIndex;
            var isVisible = Editor.VisibleFloorIndices.Contains(floorIndex);

            var floorHeader = $"{(isActive ? "▶ " : "")}{floor.Name} (Z={floor.Level:F1})";
            if (isVisible && !isActive)
                floorHeader = $"👁 {floorHeader}";

            var floorNode = new TreeViewItem
            {
                Header = floorHeader,
                IsExpanded = true,
                Tag = floorIndex
            };

            // Floor context menu
            var floorMenu = new ContextMenu();
            floorMenu.Items.Add(CreateMenuItem("Add Background...", async (_, _) =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select background image",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
                    }
                });
                if (files.Count > 0)
                {
                    var filePath = files[0].Path.LocalPath;
                    var bg = new Background
                    {
                        Name = Path.GetFileName(filePath),
                        ImagePath = filePath,
                        RealWidth = 10.0,
                        RealHeight = 8.0,
                        Opacity = 0.5,
                        Visible = true,
                        Origin = new Point2D(0, 0)
                    };
                    floor.Backgrounds.Add(bg);
                    Editor.BackgroundsDirty = true;
                    RebuildTreeView();
                    UpdateStatusBar();
                }
            }));
            floorMenu.Items.Add(new Separator());
            floorMenu.Items.Add(CreateMenuItem("Properties...", (_, _) => ShowFloorProperties(floor)));
            floorMenu.Items.Add(CreateMenuItem("Rename...", (_, _) => ShowRenameDialog(floor)));
            floorMenu.Items.Add(new Separator());
            floorMenu.Items.Add(CreateMenuItem("Duplicate", (_, _) => { Editor.DuplicateFloor(floorIndex); UpdateStatusBar(); }));
            floorMenu.Items.Add(CreateMenuItem("Delete", (_, _) =>
            {
                if (Editor.Building.Floors.Count > 1)
                { Editor.RemoveFloor(floorIndex); UpdateStatusBar(); }
            }));
            floorNode.ContextMenu = floorMenu;

            // Click handler: normal = set active, Shift = toggle visibility
            floorNode.PointerPressed += (_, args) =>
            {
                if (args.GetCurrentPoint(floorNode).Properties.IsLeftButtonPressed)
                {
                    if (args.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
                    {
                        Editor.ToggleFloorVisibility(floorIndex);
                        RebuildTreeView();
                    }
                    else
                    {
                        Editor.SetActiveFloor(floorIndex);
                        UpdateStatusBar();
                    }
                }
            };

            // Child category nodes
            var bgsNode = new TreeViewItem { Header = $"🖼️ Backgrounds ({floor.Backgrounds.Count})", Tag = "backgrounds" };
            foreach (var bg in floor.Backgrounds)
            {
                var bgItem = new TreeViewItem { Header = bg.Name, Tag = bg };
                var bgMenu = new ContextMenu();
                var bgRef = bg;
                bgMenu.Items.Add(CreateMenuItem("Properties", (_, _) =>
                {
                    Editor.SelectBackground(bgRef);
                }));
                bgMenu.Items.Add(CreateMenuItem("Delete", (_, _) =>
                {
                    floor.Backgrounds.Remove(bgRef);
                    Editor.BackgroundsDirty = true;
                    Editor.SelectBackground(null);
                    RebuildTreeView();
                    UpdateStatusBar();
                }));
                bgItem.ContextMenu = bgMenu;
                bgItem.PointerPressed += (_, args) =>
                {
                    if (args.GetCurrentPoint(bgItem).Properties.IsLeftButtonPressed)
                        Editor.SelectBackground(bgRef);
                };
                bgsNode.Items.Add(bgItem);
            }

            var wallsNode = new TreeViewItem { Header = $"🧱 Walls ({floor.Walls.Count})", Tag = "walls" };
            var doorsNode = new TreeViewItem { Header = $"🚪 Doors ({floor.Doors.Count})", Tag = "doors" };
            var slabsNode = new TreeViewItem { Header = $"⬛ Slabs ({floor.Slabs.Count})", Tag = "slabs" };
            var stairsNode = new TreeViewItem { Header = $"🪜 Stairs ({floor.Stairs.Count})", Tag = "stairs" };
            var roomsNode = new TreeViewItem { Header = $"🏠 Rooms ({floor.Rooms.Count})", Tag = "rooms" };

            floorNode.Items.Add(bgsNode);
            floorNode.Items.Add(wallsNode);
            floorNode.Items.Add(doorsNode);
            floorNode.Items.Add(slabsNode);
            floorNode.Items.Add(stairsNode);
            floorNode.Items.Add(roomsNode);

            buildingNode.Items.Add(floorNode);
        }

        tree.Items.Clear();
        tree.Items.Add(buildingNode);
    }

    private static MenuItem CreateMenuItem(string header, EventHandler<RoutedEventArgs> handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
    }

    private async void ShowRenameDialog(object target)
    {
        string currentName = target is Building b ? b.Name : target is Floor f ? f.Name : "";

        var dialog = new Window
        {
            Title = "Rename",
            Width = 300, Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 8 };
        var textBox = new TextBox { Text = currentName, Watermark = "Enter name..." };
        var btnOk = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, MinWidth = 80 };

        btnOk.Click += (_, _) =>
        {
            if (target is Building building) building.Name = textBox.Text ?? "";
            else if (target is Floor floor) floor.Name = textBox.Text ?? "";
            RebuildTreeView();
            dialog.Close();
        };

        panel.Children.Add(new TextBlock { Text = "New name:" });
        panel.Children.Add(textBox);
        panel.Children.Add(btnOk);
        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    private async void ShowFloorProperties(Floor floor)
    {
        var dialog = new Window
        {
            Title = $"Properties: {floor.Name}",
            Width = 320, Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 6 };

        var heightBox = new NumericUpDown
        {
            Value = (decimal)floor.Height, Minimum = 1, Maximum = 100,
            Increment = 0.1m, FormatString = "F2"
        };
        var levelBox = new NumericUpDown
        {
            Value = (decimal)floor.Level, Minimum = -100, Maximum = 1000,
            Increment = 0.1m, FormatString = "F2"
        };

        var btnOk = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, MinWidth = 80, Margin = new Avalonia.Thickness(0, 8, 0, 0) };
        btnOk.Click += (_, _) =>
        {
            floor.Height = (double)(heightBox.Value ?? 3m);
            floor.Level = (double)(levelBox.Value ?? 0m);
            RebuildTreeView();
            dialog.Close();
        };

        panel.Children.Add(new TextBlock { Text = "Floor height (m):" });
        panel.Children.Add(heightBox);
        panel.Children.Add(new TextBlock { Text = "Level (Z offset, m):" });
        panel.Children.Add(levelBox);
        panel.Children.Add(btnOk);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }

    // ===================== Status Bar =====================

    private void UpdateStatusBar()
    {
        var statusText = Find<TextBlock>("StatusText");
        var floorInfo = Find<TextBlock>("FloorInfo");
        var wallCount = Find<TextBlock>("WallCount");
        var doorCount = Find<TextBlock>("DoorCount");
        var slabCount = Find<TextBlock>("SlabCount");
        var stairCount = Find<TextBlock>("StairCount");
        var roomCount = Find<TextBlock>("RoomCount");
        var bgCount = Find<TextBlock>("BgCount");
        var gridInfo = Find<TextBlock>("GridInfo");

        if (statusText != null)
            statusText.Text = ToolbarVM?.ToolLabel ?? "Ready";
        if (floorInfo != null)
            floorInfo.Text = $"  |  Floor: {Editor.ActiveFloorIndex + 1}/{Editor.Building.Floors.Count}";
        if (wallCount != null)
            wallCount.Text = $"  |  Walls: {Editor.ActiveFloor.Walls.Count}";
        if (doorCount != null)
            doorCount.Text = $"  |  Doors: {Editor.ActiveFloor.Doors.Count}";
        if (slabCount != null)
            slabCount.Text = $"  |  Slabs: {Editor.ActiveFloor.Slabs.Count}";
        if (stairCount != null)
            stairCount.Text = $"  |  Stairs: {Editor.ActiveFloor.Stairs.Count}";
        if (roomCount != null)
            roomCount.Text = $"  |  Rooms: {Editor.ActiveFloor.Rooms.Count}";
        if (bgCount != null)
            bgCount.Text = $"  |  BG: {Editor.ActiveFloor.Backgrounds.Count}";
        if (gridInfo != null)
            gridInfo.Text = $"  |  Grid: {Editor.SnapEngine.GridStep}m";
    }
}
