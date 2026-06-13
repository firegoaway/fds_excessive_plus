using CommunityToolkit.Mvvm.ComponentModel;
using FdsGui.Core.Commands;
using FdsGui.Core.Models;
using FdsGui.Viewport;

namespace FdsGui.App.ViewModels;

public partial class PropertyPanelViewModel : ObservableObject
{
    private readonly EditorState _editor;

    [ObservableProperty]
    private Wall? _selectedWall;

    [ObservableProperty]
    private string _wallName = "";

    [ObservableProperty]
    private double _thickness = 200;

    [ObservableProperty]
    private double _height = 3.0;

    [ObservableProperty]
    private double _bottomZ;

    [ObservableProperty]
    private string _material = "CONCRETE";

    [ObservableProperty]
    private string _colorHex = "#B4B4B4";

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _placeholderText = "Select an object to view properties";

    private static readonly string[] AvailableMaterials = ["CONCRETE", "BRICK", "GYPSUM", "WOOD"];
    public string[] Materials => AvailableMaterials;

    public PropertyPanelViewModel(EditorState editor)
    {
        _editor = editor;
        _editor.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        var wall = _editor.SingleSelectedWall;
        SelectedWall = wall;
        HasSelection = wall != null;

        if (wall != null)
        {
            WallName = wall.Name;
            Thickness = wall.Thickness * 1000; // m -> mm
            Height = wall.Height;
            BottomZ = wall.BottomZ;
            Material = wall.Material;
            ColorHex = $"#{wall.Rgb[0]:X2}{wall.Rgb[1]:X2}{wall.Rgb[2]:X2}";
            PlaceholderText = "";
        }
        else
        {
            PlaceholderText = _editor.SelectedWalls.Count > 1
                ? "Multiple objects selected"
                : "Select an object to view properties";
        }
    }

    partial void OnWallNameChanged(string value)
    {
        if (SelectedWall == null) return;
        var old = SelectedWall.Name;
        _editor.History.Execute(new ChangePropertyCommand(
            () => SelectedWall.Name = value,
            () => SelectedWall.Name = old));
    }

    partial void OnThicknessChanged(double value)
    {
        if (SelectedWall == null || value <= 0) return;
        var old = SelectedWall.Thickness;
        double newVal = value / 1000.0; // mm -> m
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedWall.Thickness = newVal; _editor.WallsDirty = true; },
            () => { SelectedWall.Thickness = old; _editor.WallsDirty = true; }));
    }

    partial void OnHeightChanged(double value)
    {
        if (SelectedWall == null || value <= 0) return;
        var old = SelectedWall.Height;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedWall.Height = value; _editor.WallsDirty = true; },
            () => { SelectedWall.Height = old; _editor.WallsDirty = true; }));
    }

    partial void OnBottomZChanged(double value)
    {
        if (SelectedWall == null) return;
        var old = SelectedWall.BottomZ;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedWall.BottomZ = value; _editor.WallsDirty = true; },
            () => { SelectedWall.BottomZ = old; _editor.WallsDirty = true; }));
    }

    partial void OnMaterialChanged(string value)
    {
        if (SelectedWall == null) return;
        var old = SelectedWall.Material;
        _editor.History.Execute(new ChangePropertyCommand(
            () => SelectedWall.Material = value,
            () => SelectedWall.Material = old));
    }

    partial void OnColorHexChanged(string value)
    {
        if (SelectedWall == null) return;
        try
        {
            var hex = value.TrimStart('#');
            if (hex.Length != 6) return;
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);

            var old = SelectedWall.Rgb.ToArray();
            _editor.History.Execute(new ChangePropertyCommand(
                () => { SelectedWall.Rgb = [r, g, b]; _editor.WallsDirty = true; },
                () => { SelectedWall.Rgb = old; _editor.WallsDirty = true; }));
        }
        catch { /* ignore invalid hex */ }
    }
}
