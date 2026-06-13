using CommunityToolkit.Mvvm.ComponentModel;
using FdsGui.Core.Models;
using FdsGui.Viewport;

namespace FdsGui.App.ViewModels;

public partial class ToolbarViewModel : ObservableObject
{
    private readonly EditorState _editor;

    [ObservableProperty]
    private bool _snapEnabled = true;

    [ObservableProperty]
    private string _snapLabel = "Snap: ON (0.5m)";

    [ObservableProperty]
    private string _toolLabel = "Select";

    [ObservableProperty]
    private bool _isWallTool;

    [ObservableProperty]
    private string _statusText = "Ready";

    public ToolbarViewModel(EditorState editor)
    {
        _editor = editor;
        _editor.ToolChanged += UpdateToolLabel;
        UpdateToolLabel();
    }

    public void ToggleSnap()
    {
        _editor.SnapEngine.IsEnabled = !_editor.SnapEngine.IsEnabled;
        SnapEnabled = _editor.SnapEngine.IsEnabled;
        UpdateSnapLabel();
    }

    public void IncreaseGridStep()
    {
        _editor.SnapEngine.IncreaseGridStep();
        UpdateSnapLabel();
    }

    public void DecreaseGridStep()
    {
        _editor.SnapEngine.DecreaseGridStep();
        UpdateSnapLabel();
    }

    public void SelectTool()
    {
        _editor.SetTool(ToolMode.Select);
        UpdateToolLabel();
    }

    public void WallTool()
    {
        _editor.SetTool(ToolMode.Wall);
        IsWallTool = true;
        UpdateToolLabel();
    }

    public void UpdateToolLabel()
    {
        ToolLabel = _editor.CurrentTool switch
        {
            ToolMode.Select => "Select",
            ToolMode.Wall => "Wall",
            ToolMode.Door => "Door",
            ToolMode.Slab => "Slab",
            ToolMode.Stair => "Stair",
            ToolMode.Room => "Room",
            _ => "Select"
        };
        IsWallTool = _editor.CurrentTool == ToolMode.Wall;
        StatusText = $"Tool: {ToolLabel} | Walls: {_editor.ActiveFloor.Walls.Count}";
    }

    private void UpdateSnapLabel()
    {
        string state = _editor.SnapEngine.IsEnabled ? "ON" : "OFF";
        SnapLabel = $"Snap: {state} ({_editor.SnapEngine.GridStep}m)";
    }
}
