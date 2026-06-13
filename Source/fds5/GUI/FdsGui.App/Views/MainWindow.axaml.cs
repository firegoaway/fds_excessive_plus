using Avalonia.Controls;
using Avalonia.Interactivity;
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
    }

    private T? Find<T>(string name) where T : Control
    {
        return this.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
    }

    private void WireButtons()
    {
        Find<Button>("BtnSelect")!.Click += (_, _) => { Editor.SetTool(ToolMode.Select); ToolbarVM.UpdateToolLabel(); };
        Find<Button>("BtnWall")!.Click += (_, _) => { Editor.SetTool(ToolMode.Wall); ToolbarVM.UpdateToolLabel(); };

        var viewport = this.GetVisualDescendants().OfType<GlViewportControl>().FirstOrDefault();
        Find<Button>("BtnMove")!.Click += (_, _) => viewport?.StartMove();
        Find<Button>("BtnRotate")!.Click += (_, _) => viewport?.StartRotate();

        Find<Button>("BtnMirrorH")!.Click += (_, _) => Editor.MirrorHorizontal();
        Find<Button>("BtnMirrorV")!.Click += (_, _) => Editor.MirrorVertical();
        Find<Button>("BtnSnapToggle")!.Click += (_, _) => ToolbarVM.ToggleSnap();
        Find<Button>("BtnGridPlus")!.Click += (_, _) => ToolbarVM.IncreaseGridStep();
        Find<Button>("BtnGridMinus")!.Click += (_, _) => ToolbarVM.DecreaseGridStep();
    }

    // Menu Click handlers (defined in AXAML)
    private void OnMenuUndo(object? sender, RoutedEventArgs e) => Editor.Undo();
    private void OnMenuRedo(object? sender, RoutedEventArgs e) => Editor.Redo();
    private void OnMenuDelete(object? sender, RoutedEventArgs e) => Editor.DeleteSelected();
    private void OnMenuSnapToggle(object? sender, RoutedEventArgs e) => ToolbarVM.ToggleSnap();

    private void UpdateStatusBar()
    {
        var statusText = Find<TextBlock>("StatusText");
        var wallCount = Find<TextBlock>("WallCount");
        var gridInfo = Find<TextBlock>("GridInfo");

        if (statusText != null)
            statusText.Text = ToolbarVM?.ToolLabel ?? "Ready";
        if (wallCount != null)
            wallCount.Text = $"  |  Walls: {Editor.ActiveFloor.Walls.Count}";
        if (gridInfo != null)
            gridInfo.Text = $"  |  Grid: {Editor.SnapEngine.GridStep}m";
    }
}
