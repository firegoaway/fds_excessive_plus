using CommunityToolkit.Mvvm.ComponentModel;
using FdsGui.Core.Commands;
using FdsGui.Core.Models;
using FdsGui.Viewport;

namespace FdsGui.App.ViewModels;

public partial class PropertyPanelViewModel : ObservableObject
{
    private readonly EditorState _editor;
    private bool _updating;

    // Wall properties
    [ObservableProperty] private Wall? _selectedWall;
    [ObservableProperty] private string _wallName = "";
    [ObservableProperty] private double _thickness = 200;
    [ObservableProperty] private double _height = 3.0;
    [ObservableProperty] private double _bottomZ;
    [ObservableProperty] private string _material = "CONCRETE";
    [ObservableProperty] private string _colorHex = "#B4B4B4";
    [ObservableProperty] private bool _isWallSelected;

    // Door properties
    [ObservableProperty] private Door? _selectedDoor;
    [ObservableProperty] private string _doorName = "";
    [ObservableProperty] private double _doorWidth = 900;
    [ObservableProperty] private double _doorHeight = 2100;
    [ObservableProperty] private double _doorBottomZ;
    [ObservableProperty] private double _doorRotation;
    [ObservableProperty] private bool _isDoorSelected;

    // Slab properties
    [ObservableProperty] private Slab? _selectedSlab;
    [ObservableProperty] private string _slabName = "";
    [ObservableProperty] private double _slabThickness = 220;
    [ObservableProperty] private double _slabBottomZ;
    [ObservableProperty] private bool _isSlabSelected;

    // Stair properties
    [ObservableProperty] private StairFlight? _selectedStair;
    [ObservableProperty] private string _stairName = "";
    [ObservableProperty] private double _stairHeight = 3.0;
    [ObservableProperty] private double _stairWidth = 1200;
    [ObservableProperty] private double _stairBottomZ;
    [ObservableProperty] private double _stairStepHeight = 150;
    [ObservableProperty] private int _stairDirectionIndex;
    [ObservableProperty] private bool _isStairSelected;

    // Room properties
    [ObservableProperty] private Room? _selectedRoom;
    [ObservableProperty] private string _roomName = "";
    [ObservableProperty] private double _roomBottomZ;
    [ObservableProperty] private double _roomTopZ = 3.0;
    [ObservableProperty] private int _roomPointCount;
    [ObservableProperty] private string _roomMode = "Manual";
    [ObservableProperty] private bool _isRoomSelected;

    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _placeholderText = "Select an object to view properties";

    private static readonly string[] AvailableMaterials = ["CONCRETE", "BRICK", "GYPSUM", "WOOD"];
    public string[] Materials => AvailableMaterials;

    public string[] DirectionOptions => ["Up", "Down", "Left", "Right"];

    public PropertyPanelViewModel(EditorState editor)
    {
        _editor = editor;
        _editor.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        _updating = true;
        try
        {
            IsWallSelected = false; IsDoorSelected = false; IsSlabSelected = false;
            IsStairSelected = false; IsRoomSelected = false;
            SelectedWall = null; SelectedDoor = null; SelectedSlab = null;
            SelectedStair = null; SelectedRoom = null;

            if (_editor.SingleSelectedWall is { } wall)
            {
                IsWallSelected = true; SelectedWall = wall; HasSelection = true;
                WallName = wall.Name;
                Thickness = wall.Thickness * 1000;
                Height = wall.Height; BottomZ = wall.BottomZ;
                Material = wall.Material;
                ColorHex = $"#{wall.Rgb[0]:X2}{wall.Rgb[1]:X2}{wall.Rgb[2]:X2}";
                PlaceholderText = "";
            }
            else if (_editor.SingleSelectedDoor is { } door)
            {
                IsDoorSelected = true; SelectedDoor = door; HasSelection = true;
                DoorName = door.Name;
                DoorWidth = door.Width * 1000;
                DoorHeight = door.Height * 1000;
                DoorBottomZ = door.BottomZ;
                DoorRotation = door.Rotation;
                PlaceholderText = "";
            }
            else if (_editor.SingleSelectedSlab is { } slab)
            {
                IsSlabSelected = true; SelectedSlab = slab; HasSelection = true;
                SlabName = slab.Name;
                SlabThickness = slab.Thickness * 1000;
                SlabBottomZ = slab.BottomZ;
                PlaceholderText = "";
            }
            else if (_editor.SingleSelectedStair is { } stair)
            {
                IsStairSelected = true; SelectedStair = stair; HasSelection = true;
                StairName = stair.Name;
                StairHeight = stair.Height;
                StairWidth = stair.Width * 1000;
                StairBottomZ = stair.BottomZ;
                StairStepHeight = stair.StepHeight * 1000;
                StairDirectionIndex = (int)stair.Direction;
                PlaceholderText = "";
            }
            else if (_editor.SingleSelectedRoom is { } room)
            {
                IsRoomSelected = true; SelectedRoom = room; HasSelection = true;
                RoomName = room.Name;
                RoomBottomZ = room.BottomZ;
                RoomTopZ = room.TopZ;
                RoomPointCount = room.Outline.Points.Count;
                RoomMode = room.Mode == RoomCreationMode.AutoFromWalls ? "Auto" : "Manual";
                PlaceholderText = "";
            }
            else
            {
                HasSelection = false;
                PlaceholderText = _editor.HasAnySelection ? "Multiple objects selected" : "Select an object to view properties";
            }
        }
        finally { _updating = false; }
    }

    // Wall property changes
    partial void OnWallNameChanged(string value)
    {
        if (SelectedWall == null || _updating) return;
        var old = SelectedWall.Name;
        _editor.History.Execute(new ChangePropertyCommand(() => SelectedWall.Name = value, () => SelectedWall.Name = old));
    }

    partial void OnThicknessChanged(double value)
    {
        if (SelectedWall == null || value <= 0 || _updating) return;
        var old = SelectedWall.Thickness; double nv = value / 1000.0;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedWall.Thickness = nv; _editor.WallsDirty = true; },
            () => { SelectedWall.Thickness = old; _editor.WallsDirty = true; }));
    }

    partial void OnHeightChanged(double value)
    {
        if (SelectedWall == null || value <= 0 || _updating) return;
        var old = SelectedWall.Height;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedWall.Height = value; _editor.WallsDirty = true; },
            () => { SelectedWall.Height = old; _editor.WallsDirty = true; }));
    }

    partial void OnBottomZChanged(double value)
    {
        if (SelectedWall == null || _updating) return;
        var old = SelectedWall.BottomZ;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedWall.BottomZ = value; _editor.WallsDirty = true; },
            () => { SelectedWall.BottomZ = old; _editor.WallsDirty = true; }));
    }

    partial void OnMaterialChanged(string value)
    {
        if (SelectedWall == null || _updating) return;
        var old = SelectedWall.Material;
        _editor.History.Execute(new ChangePropertyCommand(() => SelectedWall.Material = value, () => SelectedWall.Material = old));
    }

    partial void OnColorHexChanged(string value)
    {
        if (SelectedWall == null || _updating) return;
        try
        {
            var hex = value.TrimStart('#');
            if (hex.Length != 6) return;
            int r = Convert.ToInt32(hex[..2], 16), g = Convert.ToInt32(hex[2..4], 16), b = Convert.ToInt32(hex[4..6], 16);
            var old = SelectedWall.Rgb.ToArray();
            _editor.History.Execute(new ChangePropertyCommand(
                () => { SelectedWall.Rgb = [r, g, b]; _editor.WallsDirty = true; },
                () => { SelectedWall.Rgb = old; _editor.WallsDirty = true; }));
        }
        catch { }
    }

    // Door property changes
    partial void OnDoorNameChanged(string value)
    {
        if (SelectedDoor == null || _updating) return;
        var old = SelectedDoor.Name;
        _editor.History.Execute(new ChangePropertyCommand(() => SelectedDoor.Name = value, () => SelectedDoor.Name = old));
    }

    partial void OnDoorWidthChanged(double value)
    {
        if (SelectedDoor == null || value <= 0 || _updating) return;
        var old = SelectedDoor.Width; double nv = value / 1000.0;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedDoor.Width = nv; _editor.DoorsDirty = true; },
            () => { SelectedDoor.Width = old; _editor.DoorsDirty = true; }));
    }

    partial void OnDoorHeightChanged(double value)
    {
        if (SelectedDoor == null || value <= 0 || _updating) return;
        var old = SelectedDoor.Height; double nv = value / 1000.0;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedDoor.Height = nv; _editor.DoorsDirty = true; },
            () => { SelectedDoor.Height = old; _editor.DoorsDirty = true; }));
    }

    partial void OnDoorBottomZChanged(double value)
    {
        if (SelectedDoor == null || _updating) return;
        var old = SelectedDoor.BottomZ;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedDoor.BottomZ = value; _editor.DoorsDirty = true; },
            () => { SelectedDoor.BottomZ = old; _editor.DoorsDirty = true; }));
    }

    partial void OnDoorRotationChanged(double value)
    {
        if (SelectedDoor == null || _updating) return;
        var old = SelectedDoor.Rotation;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedDoor.Rotation = value; _editor.DoorsDirty = true; },
            () => { SelectedDoor.Rotation = old; _editor.DoorsDirty = true; }));
    }

    // Slab property changes
    partial void OnSlabNameChanged(string value)
    {
        if (SelectedSlab == null || _updating) return;
        var old = SelectedSlab.Name;
        _editor.History.Execute(new ChangePropertyCommand(() => SelectedSlab.Name = value, () => SelectedSlab.Name = old));
    }

    partial void OnSlabThicknessChanged(double value)
    {
        if (SelectedSlab == null || value <= 0 || _updating) return;
        var old = SelectedSlab.Thickness; double nv = value / 1000.0;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedSlab.Thickness = nv; _editor.SlabsDirty = true; },
            () => { SelectedSlab.Thickness = old; _editor.SlabsDirty = true; }));
    }

    partial void OnSlabBottomZChanged(double value)
    {
        if (SelectedSlab == null || _updating) return;
        var old = SelectedSlab.BottomZ;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedSlab.BottomZ = value; _editor.SlabsDirty = true; },
            () => { SelectedSlab.BottomZ = old; _editor.SlabsDirty = true; }));
    }

    // Stair property changes
    partial void OnStairNameChanged(string value)
    {
        if (SelectedStair == null || _updating) return;
        var old = SelectedStair.Name;
        _editor.History.Execute(new ChangePropertyCommand(() => SelectedStair.Name = value, () => SelectedStair.Name = old));
    }

    partial void OnStairHeightChanged(double value)
    {
        if (SelectedStair == null || value <= 0 || _updating) return;
        var old = SelectedStair.Height;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedStair.Height = value; _editor.StairsDirty = true; },
            () => { SelectedStair.Height = old; _editor.StairsDirty = true; }));
    }

    partial void OnStairWidthChanged(double value)
    {
        if (SelectedStair == null || value <= 0 || _updating) return;
        var old = SelectedStair.Width; double nv = value / 1000.0;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedStair.Width = nv; _editor.StairsDirty = true; },
            () => { SelectedStair.Width = old; _editor.StairsDirty = true; }));
    }

    partial void OnStairBottomZChanged(double value)
    {
        if (SelectedStair == null || _updating) return;
        var old = SelectedStair.BottomZ;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedStair.BottomZ = value; _editor.StairsDirty = true; },
            () => { SelectedStair.BottomZ = old; _editor.StairsDirty = true; }));
    }

    partial void OnStairStepHeightChanged(double value)
    {
        if (SelectedStair == null || value <= 0 || _updating) return;
        var old = SelectedStair.StepHeight; double nv = value / 1000.0;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedStair.StepHeight = nv; _editor.StairsDirty = true; },
            () => { SelectedStair.StepHeight = old; _editor.StairsDirty = true; }));
    }

    partial void OnStairDirectionIndexChanged(int value)
    {
        if (SelectedStair == null || _updating) return;
        var old = SelectedStair.Direction;
        var newDir = (Direction)value;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedStair.Direction = newDir; _editor.StairsDirty = true; },
            () => { SelectedStair.Direction = old; _editor.StairsDirty = true; }));
    }

    // Room property changes
    partial void OnRoomNameChanged(string value)
    {
        if (SelectedRoom == null || _updating) return;
        var old = SelectedRoom.Name;
        _editor.History.Execute(new ChangePropertyCommand(() => SelectedRoom.Name = value, () => SelectedRoom.Name = old));
    }

    partial void OnRoomBottomZChanged(double value)
    {
        if (SelectedRoom == null || _updating) return;
        var old = SelectedRoom.BottomZ;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedRoom.BottomZ = value; _editor.RoomsDirty = true; },
            () => { SelectedRoom.BottomZ = old; _editor.RoomsDirty = true; }));
    }

    partial void OnRoomTopZChanged(double value)
    {
        if (SelectedRoom == null || _updating) return;
        var old = SelectedRoom.TopZ;
        _editor.History.Execute(new ChangePropertyCommand(
            () => { SelectedRoom.TopZ = value; _editor.RoomsDirty = true; },
            () => { SelectedRoom.TopZ = old; _editor.RoomsDirty = true; }));
    }
}
