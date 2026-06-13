using FdsGui.Core.Commands;
using FdsGui.Core.Models;
using FdsGui.Viewport.Renderers;
using FdsGui.Viewport.Snapping;

namespace FdsGui.Viewport;

public enum TransformMode
{
    None,
    Move,
    Rotate
}

public class EditorState
{
    // Domain model
    public Building Building { get; set; } = new();

    // Multi-floor support
    public int ActiveFloorIndex { get; set; } = 0;
    public HashSet<int> VisibleFloorIndices { get; } = new() { 0 };

    public Floor ActiveFloor
    {
        get
        {
            if (Building.Floors.Count == 0)
                return CreateDefaultFloor();
            ActiveFloorIndex = Math.Clamp(ActiveFloorIndex, 0, Building.Floors.Count - 1);
            return Building.Floors[ActiveFloorIndex];
        }
    }

    // Command history
    public CommandHistory History { get; } = new();

    // Tool state
    public ToolMode CurrentTool { get; set; } = ToolMode.Select;

    // Selection
    public HashSet<Wall> SelectedWalls { get; } = new();
    public HashSet<Door> SelectedDoors { get; } = new();
    public HashSet<Slab> SelectedSlabs { get; } = new();
    public HashSet<StairFlight> SelectedStairs { get; } = new();
    public HashSet<Room> SelectedRooms { get; } = new();

    // Snap
    public SnapEngine SnapEngine { get; } = new();

    // Transform state
    public TransformMode ActiveTransform { get; set; } = TransformMode.None;
    public Point2D TransformStart { get; set; } = new(0, 0);
    public Point2D TransformOriginalStart { get; set; } = new(0, 0);
    public Point2D TransformOriginalEnd { get; set; } = new(0, 0);
    public double TransformOriginalAngle { get; set; }
    public Point2D TransformOriginalDoorPos { get; set; } = new(0, 0);
    public List<Point2D> TransformOriginalSlabPoints { get; set; } = new();
    public Point2D TransformOriginalStairStart { get; set; } = new(0, 0);
    public Point2D TransformOriginalStairEnd { get; set; } = new(0, 0);
    public List<Point2D> TransformOriginalRoomPoints { get; set; } = new();

    // Renderers
    public WallRenderer WallRenderer { get; } = new();
    public DoorRenderer DoorRenderer { get; } = new();
    public SlabRenderer SlabRenderer { get; } = new();
    public StairRenderer StairRenderer { get; } = new();
    public GhostRenderer GhostRenderer { get; } = new();
    public RoomRenderer RoomRenderer { get; } = new();

    // Dirty flags
    public bool WallsDirty { get; set; } = true;
    public bool DoorsDirty { get; set; } = true;
    public bool SlabsDirty { get; set; } = true;
    public bool StairsDirty { get; set; } = true;
    public bool RoomsDirty { get; set; } = true;
    public bool SelectionDirty { get; set; } = true;

    // Room drawing state
    public List<Point2D> RoomDrawPoints { get; set; } = new();

    // Events
    public event Action? SelectionChanged;
    public event Action? ToolChanged;
    public event Action? HierarchyChanged;

    // --- Floor management ---

    public void AddFloor()
    {
        var lastFloor = Building.Floors.Count > 0
            ? Building.Floors[^1]
            : null;

        double newLevel = lastFloor != null ? lastFloor.Level + lastFloor.Height : 0.0;
        var floor = new Floor
        {
            Name = $"Этаж {Building.Floors.Count + 1}",
            Height = 3.0,
            Level = newLevel
        };

        History.Execute(new AddFloorCommand(Building, floor));
        ActiveFloorIndex = Building.Floors.Count - 1;
        VisibleFloorIndices.Clear();
        VisibleFloorIndices.Add(ActiveFloorIndex);
        MarkAllDirty();
        HierarchyChanged?.Invoke();
    }

    public void RemoveFloor(int index)
    {
        if (Building.Floors.Count <= 1) return;
        if (index < 0 || index >= Building.Floors.Count) return;

        var floor = Building.Floors[index];
        // Simple remove (undo not yet wired for floor removal via command)
        Building.Floors.RemoveAt(index);

        // Adjust active index
        if (ActiveFloorIndex >= Building.Floors.Count)
            ActiveFloorIndex = Building.Floors.Count - 1;

        // Rebuild visible indices
        VisibleFloorIndices.Clear();
        VisibleFloorIndices.Add(ActiveFloorIndex);
        MarkAllDirty();
        HierarchyChanged?.Invoke();
    }

    public void DuplicateFloor(int index)
    {
        if (index < 0 || index >= Building.Floors.Count) return;
        var source = Building.Floors[index];

        var copy = new Floor
        {
            Name = source.Name + " (копия)",
            Height = source.Height,
            Level = source.Level + source.Height,
            Walls = source.Walls.Select(w => new Wall
            {
                Name = w.Name + "-copy",
                Start = w.Start, End = w.End,
                Thickness = w.Thickness, Height = w.Height,
                BottomZ = source.Level + source.Height,
                Material = w.Material, Rgb = w.Rgb.ToArray()
            }).ToList(),
            Doors = source.Doors.Select(d => new Door
            {
                Name = d.Name + "-copy",
                Position = d.Position, Width = d.Width, Height = d.Height,
                BottomZ = source.Level + source.Height,
                ParentWallId = d.ParentWallId + "-copy", Rotation = d.Rotation
            }).ToList(),
            Slabs = source.Slabs.Select(s => new Slab
            {
                Name = s.Name + "-copy",
                Thickness = s.Thickness, BottomZ = source.Level + source.Height,
                Outline = new Polygon2D { Points = s.Outline.Points.Select(p => p).ToList() }
            }).ToList(),
            Stairs = source.Stairs.Select(st => new StairFlight
            {
                Name = st.Name + "-copy",
                Start = st.Start, End = st.End,
                Height = st.Height, BottomZ = source.Level + source.Height,
                Width = st.Width, StepHeight = st.StepHeight, Direction = st.Direction
            }).ToList(),
            Rooms = source.Rooms.Select(r => new Room
            {
                Name = r.Name + "-copy",
                Outline = new Polygon2D { Points = r.Outline.Points.Select(p => p).ToList() },
                BottomZ = source.Level + source.Height,
                TopZ = source.Level + source.Height + source.Height,
                Mode = r.Mode
            }).ToList()
        };

        // Insert after the source floor
        int insertIdx = index + 1;
        // Recalculate levels for floors above
        for (int i = insertIdx; i < Building.Floors.Count; i++)
            Building.Floors[i].Level += source.Height;

        Building.Floors.Insert(insertIdx, copy);
        ActiveFloorIndex = insertIdx;
        VisibleFloorIndices.Clear();
        VisibleFloorIndices.Add(ActiveFloorIndex);
        MarkAllDirty();
        HierarchyChanged?.Invoke();
    }

    public void SetActiveFloor(int index)
    {
        if (index < 0 || index >= Building.Floors.Count) return;
        ActiveFloorIndex = index;
        VisibleFloorIndices.Clear();
        VisibleFloorIndices.Add(index);
        MarkAllDirty();
        HierarchyChanged?.Invoke();
    }

    public void ToggleFloorVisibility(int index)
    {
        if (index < 0 || index >= Building.Floors.Count) return;
        if (VisibleFloorIndices.Contains(index))
            VisibleFloorIndices.Remove(index);
        else
            VisibleFloorIndices.Add(index);
        MarkAllDirty();
    }

    public void ShowAllFloors()
    {
        VisibleFloorIndices.Clear();
        for (int i = 0; i < Building.Floors.Count; i++)
            VisibleFloorIndices.Add(i);
        MarkAllDirty();
        HierarchyChanged?.Invoke();
    }

    public void ShowActiveFloorOnly()
    {
        VisibleFloorIndices.Clear();
        VisibleFloorIndices.Add(ActiveFloorIndex);
        MarkAllDirty();
    }

    private void MarkAllDirty()
    {
        WallsDirty = true; DoorsDirty = true; SlabsDirty = true;
        StairsDirty = true; RoomsDirty = true; SelectionDirty = true;
    }

    /// Get all walls from visible floors
    public IEnumerable<Wall> VisibleWalls
    {
        get
        {
            foreach (var idx in VisibleFloorIndices)
                if (idx >= 0 && idx < Building.Floors.Count)
                    foreach (var w in Building.Floors[idx].Walls)
                        yield return w;
        }
    }

    public IEnumerable<Door> VisibleDoors
    {
        get
        {
            foreach (var idx in VisibleFloorIndices)
                if (idx >= 0 && idx < Building.Floors.Count)
                    foreach (var d in Building.Floors[idx].Doors)
                        yield return d;
        }
    }

    public IEnumerable<Slab> VisibleSlabs
    {
        get
        {
            foreach (var idx in VisibleFloorIndices)
                if (idx >= 0 && idx < Building.Floors.Count)
                    foreach (var s in Building.Floors[idx].Slabs)
                        yield return s;
        }
    }

    public IEnumerable<StairFlight> VisibleStairs
    {
        get
        {
            foreach (var idx in VisibleFloorIndices)
                if (idx >= 0 && idx < Building.Floors.Count)
                    foreach (var st in Building.Floors[idx].Stairs)
                        yield return st;
        }
    }

    public IEnumerable<Room> VisibleRooms
    {
        get
        {
            foreach (var idx in VisibleFloorIndices)
                if (idx >= 0 && idx < Building.Floors.Count)
                    foreach (var r in Building.Floors[idx].Rooms)
                        yield return r;
        }
    }

    private Floor CreateDefaultFloor()
    {
        var floor = new Floor { Name = "Этаж 1", Height = 3.0, Level = 0.0 };
        Building.Floors.Add(floor);
        HierarchyChanged?.Invoke();
        return floor;
    }

    // --- Wall tool ---

    public void BeginWallDraw(Point2D start)
    {
        var ghost = new Wall
        {
            Name = "ghost", Start = start, End = start,
            Thickness = 0.2, Height = ActiveFloor.Height,
            BottomZ = ActiveFloor.Level, Rgb = [180, 180, 180]
        };
        GhostRenderer.GhostWall = ghost;
    }

    public void UpdateWallDraw(Point2D end)
    {
        if (GhostRenderer.GhostWall != null)
            GhostRenderer.GhostWall.End = end;
    }

    public Wall? FinishWallDraw(Point2D end)
    {
        if (GhostRenderer.GhostWall == null) return null;
        var start = GhostRenderer.GhostWall.Start;
        GhostRenderer.Clear();
        if (start.DistanceTo(end) < 0.05) return null;

        var wall = new Wall
        {
            Name = $"Stena-{ActiveFloor.Walls.Count + 1}",
            Start = start, End = end, Thickness = 0.2,
            Height = ActiveFloor.Height, BottomZ = ActiveFloor.Level,
            Material = Building.Settings.DefaultMaterial, Rgb = [180, 180, 180]
        };
        History.Execute(new AddWallCommand(ActiveFloor, wall));
        WallsDirty = true;
        return wall;
    }

    // --- Door tool ---

    public Door? PlaceDoor(Wall targetWall, Point2D clickPos)
    {
        var dir = targetWall.Direction;
        var toClick = clickPos - targetWall.Start;
        double t = toClick.X * dir.X + toClick.Y * dir.Y;
        t = Math.Clamp(t, 0.45, targetWall.Length - 0.45);
        var position = targetWall.Start + dir * t;

        double doorHeight = Math.Min(2.1, targetWall.Height - 0.1);

        var door = new Door
        {
            Name = $"Dver-{ActiveFloor.Doors.Count + 1}",
            Position = position, Width = 0.9, Height = doorHeight,
            BottomZ = ActiveFloor.Level, ParentWallId = targetWall.Name,
            Rotation = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI
        };

        History.Execute(new AddDoorCommand(ActiveFloor, door));
        DoorsDirty = true;
        return door;
    }

    // --- Slab tool ---

    public void BeginSlabDraw(Point2D corner1)
    {
        GhostRenderer.GhostSlab = new Slab
        {
            Name = "ghost", Thickness = 0.22, BottomZ = ActiveFloor.Level,
            Outline = new Polygon2D { Points = [corner1, corner1, corner1, corner1] }
        };
    }

    public void UpdateSlabDraw(Point2D corner2)
    {
        if (GhostRenderer.GhostSlab == null) return;
        var c1 = GhostRenderer.GhostSlab.Outline.Points[0];
        GhostRenderer.GhostSlab.Outline.Points =
        [
            c1, new Point2D(corner2.X, c1.Y),
            corner2, new Point2D(c1.X, corner2.Y)
        ];
    }

    public Slab? FinishSlabDraw(Point2D corner2)
    {
        if (GhostRenderer.GhostSlab == null) return null;
        var c1 = GhostRenderer.GhostSlab.Outline.Points[0];
        GhostRenderer.Clear();

        if (Math.Abs(corner2.X - c1.X) < 0.1 || Math.Abs(corner2.Y - c1.Y) < 0.1) return null;

        var slab = new Slab
        {
            Name = $"Perekrytie-{ActiveFloor.Slabs.Count + 1}",
            Thickness = 0.22, BottomZ = ActiveFloor.Level,
            Outline = new Polygon2D
            {
                Points =
                [
                    c1, new Point2D(corner2.X, c1.Y),
                    corner2, new Point2D(c1.X, corner2.Y)
                ]
            }
        };

        History.Execute(new AddSlabCommand(ActiveFloor, slab));
        SlabsDirty = true;
        return slab;
    }

    // --- Stair tool ---

    public void BeginStairDraw(Point2D start)
    {
        GhostRenderer.GhostStair = new StairFlight
        {
            Name = "ghost", Start = start, End = start,
            Height = ActiveFloor.Height, BottomZ = ActiveFloor.Level,
            Width = 1.2, StepHeight = 0.15
        };
    }

    public void UpdateStairDraw(Point2D end)
    {
        if (GhostRenderer.GhostStair != null)
            GhostRenderer.GhostStair.End = end;
    }

    public StairFlight? FinishStairDraw(Point2D end)
    {
        if (GhostRenderer.GhostStair == null) return null;
        var start = GhostRenderer.GhostStair.Start;
        GhostRenderer.Clear();
        if (start.DistanceTo(end) < 0.3) return null;

        var stair = new StairFlight
        {
            Name = $"Lestnica-{ActiveFloor.Stairs.Count + 1}",
            Start = start, End = end,
            Height = ActiveFloor.Height, BottomZ = ActiveFloor.Level,
            Width = 1.2, StepHeight = 0.15
        };

        History.Execute(new AddStairCommand(ActiveFloor, stair));
        StairsDirty = true;
        return stair;
    }

    // --- Room tool ---

    public void AddRoomPoint(Point2D point)
    {
        RoomDrawPoints.Add(point);
    }

    public void CancelRoomDraw()
    {
        RoomDrawPoints.Clear();
        GhostRenderer.Clear();
    }

    public void UpdateRoomGhost(Point2D cursorPos)
    {
        if (RoomDrawPoints.Count == 0) return;
        // Show ghost line from last point to cursor
        var lastPt = RoomDrawPoints[^1];
        GhostRenderer.GhostWall = new Wall
        {
            Name = "room-ghost", Start = lastPt, End = cursorPos,
            Thickness = 0.05, Height = 0.01, BottomZ = ActiveFloor.Level,
            Rgb = [100, 150, 255]
        };
    }

    public Room? FinishRoomDraw()
    {
        if (RoomDrawPoints.Count < 3) return null;
        var points = RoomDrawPoints.ToList();
        RoomDrawPoints.Clear();
        GhostRenderer.Clear();

        var room = new Room
        {
            Name = $"Pomeschenie-{ActiveFloor.Rooms.Count + 1}",
            Outline = new Polygon2D { Points = points },
            BottomZ = ActiveFloor.Level,
            TopZ = ActiveFloor.Level + ActiveFloor.Height,
            Mode = RoomCreationMode.Manual
        };

        History.Execute(new AddRoomCommand(ActiveFloor, room));
        RoomsDirty = true;
        HierarchyChanged?.Invoke();
        return room;
    }

    // --- Selection ---

    private void ClearAllSelection()
    {
        SelectedWalls.Clear(); SelectedDoors.Clear();
        SelectedSlabs.Clear(); SelectedStairs.Clear(); SelectedRooms.Clear();
    }

    public void SelectWall(Wall? wall, bool addToSelection = false)
    {
        if (!addToSelection) ClearAllSelection();
        if (wall != null) SelectedWalls.Add(wall);
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public void SelectDoor(Door? door, bool addToSelection = false)
    {
        if (!addToSelection) ClearAllSelection();
        if (door != null) SelectedDoors.Add(door);
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public void SelectSlab(Slab? slab, bool addToSelection = false)
    {
        if (!addToSelection) ClearAllSelection();
        if (slab != null) SelectedSlabs.Add(slab);
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public void SelectStair(StairFlight? stair, bool addToSelection = false)
    {
        if (!addToSelection) ClearAllSelection();
        if (stair != null) SelectedStairs.Add(stair);
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public void SelectRoom(Room? room, bool addToSelection = false)
    {
        if (!addToSelection) ClearAllSelection();
        if (room != null) SelectedRooms.Add(room);
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        int total = SelectedWalls.Count + SelectedDoors.Count + SelectedSlabs.Count + SelectedStairs.Count + SelectedRooms.Count;
        if (total == 0) return;
        ClearAllSelection();
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    private int OtherSelectedCount =>
        SelectedWalls.Count + SelectedDoors.Count + SelectedSlabs.Count + SelectedStairs.Count + SelectedRooms.Count;

    public Wall? SingleSelectedWall => SelectedWalls.Count == 1 && OtherSelectedCount - 1 == 0 ? SelectedWalls.First() : null;
    public Door? SingleSelectedDoor => SelectedDoors.Count == 1 && OtherSelectedCount - 1 == 0 ? SelectedDoors.First() : null;
    public Slab? SingleSelectedSlab => SelectedSlabs.Count == 1 && OtherSelectedCount - 1 == 0 ? SelectedSlabs.First() : null;
    public StairFlight? SingleSelectedStair => SelectedStairs.Count == 1 && OtherSelectedCount - 1 == 0 ? SelectedStairs.First() : null;
    public Room? SingleSelectedRoom => SelectedRooms.Count == 1 && OtherSelectedCount - 1 == 0 ? SelectedRooms.First() : null;
    public bool HasAnySelection => OtherSelectedCount > 0;

    // --- Delete ---

    public void DeleteSelected()
    {
        if (!HasAnySelection) return;

        // Delete rooms
        if (SelectedRooms.Count > 0)
        {
            foreach (var room in SelectedRooms)
                ActiveFloor.Rooms.Remove(room);
            RoomsDirty = true;
            HierarchyChanged?.Invoke();
        }

        var cmd = new DeleteCommand(ActiveFloor, SelectedWalls.ToList(), SelectedDoors.ToList(), SelectedSlabs.ToList(), SelectedStairs.ToList());
        History.Execute(cmd);
        ClearAllSelection();
        WallsDirty = true; DoorsDirty = true; SlabsDirty = true; StairsDirty = true;
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    // --- Move ---

    public void BeginMove(Point2D cursorPos)
    {
        if (SingleSelectedWall is { } wall)
        {
            ActiveTransform = TransformMode.Move;
            TransformStart = cursorPos;
            TransformOriginalStart = wall.Start;
            TransformOriginalEnd = wall.End;
        }
        else if (SingleSelectedDoor is { } door)
        {
            ActiveTransform = TransformMode.Move;
            TransformStart = cursorPos;
            TransformOriginalDoorPos = door.Position;
        }
        else if (SingleSelectedSlab is { } slab)
        {
            ActiveTransform = TransformMode.Move;
            TransformStart = cursorPos;
            TransformOriginalSlabPoints = slab.Outline.Points.Select(p => p).ToList();
        }
        else if (SingleSelectedStair is { } stair)
        {
            ActiveTransform = TransformMode.Move;
            TransformStart = cursorPos;
            TransformOriginalStairStart = stair.Start;
            TransformOriginalStairEnd = stair.End;
        }
        else if (SingleSelectedRoom is { } room)
        {
            ActiveTransform = TransformMode.Move;
            TransformStart = cursorPos;
            TransformOriginalRoomPoints = room.Outline.Points.Select(p => p).ToList();
        }
    }

    public void UpdateMove(Point2D cursorPos)
    {
        if (ActiveTransform != TransformMode.Move) return;
        var delta = cursorPos - TransformStart;

        if (SingleSelectedWall is { } wall)
        {
            wall.Start = TransformOriginalStart + delta;
            wall.End = TransformOriginalEnd + delta;
            WallsDirty = true; SelectionDirty = true;
            GhostRenderer.GhostWall = new Wall { Start = wall.Start, End = wall.End, Thickness = wall.Thickness, Height = wall.Height, BottomZ = wall.BottomZ, Rgb = [100, 200, 100] };
        }
        else if (SingleSelectedDoor is { } door)
        {
            door.Position = TransformOriginalDoorPos + delta;
            DoorsDirty = true; SelectionDirty = true;
        }
        else if (SingleSelectedSlab is { } slab && TransformOriginalSlabPoints.Count >= 4)
        {
            slab.Outline.Points = TransformOriginalSlabPoints.Select(p => p + delta).ToList();
            SlabsDirty = true; SelectionDirty = true;
        }
        else if (SingleSelectedStair is { } stair)
        {
            stair.Start = TransformOriginalStairStart + delta;
            stair.End = TransformOriginalStairEnd + delta;
            StairsDirty = true; SelectionDirty = true;
        }
        else if (SingleSelectedRoom is { } room)
        {
            room.Outline.Points = TransformOriginalRoomPoints.Select(p => p + delta).ToList();
            RoomsDirty = true; SelectionDirty = true;
        }
    }

    public void ConfirmMove()
    {
        if (SingleSelectedWall is { } wall && ActiveTransform == TransformMode.Move)
        {
            var os = TransformOriginalStart; var oe = TransformOriginalEnd;
            History.Execute(new ChangePropertyCommand(
                () => { }, () => { wall.Start = os; wall.End = oe; WallsDirty = true; SelectionDirty = true; }));
            GhostRenderer.Clear(); ActiveTransform = TransformMode.None; WallsDirty = true;
        }
        else if (SingleSelectedDoor is { } door && ActiveTransform == TransformMode.Move)
        {
            var op = TransformOriginalDoorPos; var np = door.Position;
            History.Execute(new ChangePropertyCommand(
                () => { }, () => { door.Position = op; DoorsDirty = true; SelectionDirty = true; }));
            ActiveTransform = TransformMode.None; DoorsDirty = true;
        }
        else if (SingleSelectedSlab is { } slab && ActiveTransform == TransformMode.Move)
        {
            var op = TransformOriginalSlabPoints; var np = slab.Outline.Points.Select(p => p).ToList();
            History.Execute(new ChangePropertyCommand(
                () => { }, () => { slab.Outline.Points = op; SlabsDirty = true; SelectionDirty = true; }));
            ActiveTransform = TransformMode.None; SlabsDirty = true;
        }
        else if (SingleSelectedStair is { } stair && ActiveTransform == TransformMode.Move)
        {
            var os = TransformOriginalStairStart; var oe = TransformOriginalStairEnd;
            History.Execute(new ChangePropertyCommand(
                () => { }, () => { stair.Start = os; stair.End = oe; StairsDirty = true; SelectionDirty = true; }));
            ActiveTransform = TransformMode.None; StairsDirty = true;
        }
        else if (SingleSelectedRoom is { } room && ActiveTransform == TransformMode.Move)
        {
            var op = TransformOriginalRoomPoints; var np = room.Outline.Points.Select(p => p).ToList();
            History.Execute(new ChangePropertyCommand(
                () => { }, () => { room.Outline.Points = op; RoomsDirty = true; SelectionDirty = true; }));
            ActiveTransform = TransformMode.None; RoomsDirty = true;
        }
    }

    public void CancelMove()
    {
        if (SingleSelectedWall is { } wall)
        { wall.Start = TransformOriginalStart; wall.End = TransformOriginalEnd; GhostRenderer.Clear(); WallsDirty = true; }
        if (SingleSelectedDoor is { } door)
        { door.Position = TransformOriginalDoorPos; DoorsDirty = true; }
        if (SingleSelectedSlab is { } slab && TransformOriginalSlabPoints.Count >= 4)
        { slab.Outline.Points = TransformOriginalSlabPoints; SlabsDirty = true; }
        if (SingleSelectedStair is { } stair)
        { stair.Start = TransformOriginalStairStart; stair.End = TransformOriginalStairEnd; StairsDirty = true; }
        if (SingleSelectedRoom is { } room && TransformOriginalRoomPoints.Count >= 3)
        { room.Outline.Points = TransformOriginalRoomPoints; RoomsDirty = true; }

        ActiveTransform = TransformMode.None; SelectionDirty = true;
    }

    // --- Rotate ---

    public void BeginRotate(Point2D cursorPos)
    {
        if (SingleSelectedWall is not { } wall) return;
        ActiveTransform = TransformMode.Rotate;
        TransformOriginalStart = wall.Start; TransformOriginalEnd = wall.End;
        var center = new Point2D((wall.Start.X + wall.End.X) / 2, (wall.Start.Y + wall.End.Y) / 2);
        TransformOriginalAngle = Math.Atan2(cursorPos.Y - center.Y, cursorPos.X - center.X);
    }

    public void UpdateRotate(Point2D cursorPos)
    {
        if (SingleSelectedWall is not { } wall || ActiveTransform != TransformMode.Rotate) return;
        var center = new Point2D((TransformOriginalStart.X + TransformOriginalEnd.X) / 2, (TransformOriginalStart.Y + TransformOriginalEnd.Y) / 2);
        double angleDelta = Math.Atan2(cursorPos.Y - center.Y, cursorPos.X - center.X) - TransformOriginalAngle;
        wall.Start = RotatePoint(TransformOriginalStart, center, angleDelta);
        wall.End = RotatePoint(TransformOriginalEnd, center, angleDelta);
        WallsDirty = true; SelectionDirty = true;
    }

    public void ConfirmRotate()
    {
        if (SingleSelectedWall is not { } wall || ActiveTransform != TransformMode.Rotate) return;
        History.Execute(new ChangePropertyCommand(
            () => { }, () => { wall.Start = TransformOriginalStart; wall.End = TransformOriginalEnd; WallsDirty = true; SelectionDirty = true; }));
        ActiveTransform = TransformMode.None; WallsDirty = true;
    }

    public void CancelRotate()
    {
        if (SingleSelectedWall is { } wall) { wall.Start = TransformOriginalStart; wall.End = TransformOriginalEnd; WallsDirty = true; SelectionDirty = true; }
        ActiveTransform = TransformMode.None;
    }

    // --- Mirror ---

    public void MirrorHorizontal()
    {
        if (SingleSelectedWall is { } wall)
        {
            var os = wall.Start; var oe = wall.End;
            double cx = (os.X + oe.X) / 2;
            History.Execute(new ChangePropertyCommand(
                () => { wall.Start = new Point2D(2 * cx - os.X, os.Y); wall.End = new Point2D(2 * cx - oe.X, oe.Y); },
                () => { wall.Start = os; wall.End = oe; WallsDirty = true; SelectionDirty = true; }));
            WallsDirty = true; SelectionDirty = true;
        }
        else if (SingleSelectedSlab is { } slab && slab.Outline.Points.Count >= 4)
        {
            var op = slab.Outline.Points.Select(p => p).ToList();
            double cx = (slab.Outline.MinX + slab.Outline.MaxX) / 2;
            History.Execute(new ChangePropertyCommand(
                () => { slab.Outline.Points = op.Select(p => new Point2D(2 * cx - p.X, p.Y)).ToList(); SlabsDirty = true; },
                () => { slab.Outline.Points = op; SlabsDirty = true; SelectionDirty = true; }));
            SlabsDirty = true; SelectionDirty = true;
        }
    }

    public void MirrorVertical()
    {
        if (SingleSelectedWall is { } wall)
        {
            var os = wall.Start; var oe = wall.End;
            double cy = (os.Y + oe.Y) / 2;
            History.Execute(new ChangePropertyCommand(
                () => { wall.Start = new Point2D(os.X, 2 * cy - os.Y); wall.End = new Point2D(oe.X, 2 * cy - oe.Y); },
                () => { wall.Start = os; wall.End = oe; WallsDirty = true; SelectionDirty = true; }));
            WallsDirty = true; SelectionDirty = true;
        }
        else if (SingleSelectedSlab is { } slab && slab.Outline.Points.Count >= 4)
        {
            var op = slab.Outline.Points.Select(p => p).ToList();
            double cy = (slab.Outline.MinY + slab.Outline.MaxY) / 2;
            History.Execute(new ChangePropertyCommand(
                () => { slab.Outline.Points = op.Select(p => new Point2D(p.X, 2 * cy - p.Y)).ToList(); SlabsDirty = true; },
                () => { slab.Outline.Points = op; SlabsDirty = true; SelectionDirty = true; }));
            SlabsDirty = true; SelectionDirty = true;
        }
    }

    // --- Tool switching ---

    public void SetTool(ToolMode mode)
    {
        if (ActiveTransform != TransformMode.None)
        {
            if (ActiveTransform == TransformMode.Move) CancelMove();
            else if (ActiveTransform == TransformMode.Rotate) CancelRotate();
        }
        // Cancel room drawing if switching away from room tool
        if (CurrentTool == ToolMode.Room && mode != ToolMode.Room)
            CancelRoomDraw();
        GhostRenderer.Clear();
        CurrentTool = mode;
        ToolChanged?.Invoke();
    }

    // --- Undo/Redo ---

    public void Undo()
    {
        History.Undo();
        MarkAllDirty();
        SelectionChanged?.Invoke();
        HierarchyChanged?.Invoke();
    }

    public void Redo()
    {
        History.Redo();
        MarkAllDirty();
        SelectionChanged?.Invoke();
        HierarchyChanged?.Invoke();
    }

    // --- Helpers ---

    private static Point2D RotatePoint(Point2D point, Point2D center, double angle)
    {
        double dx = point.X - center.X, dy = point.Y - center.Y;
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return new Point2D(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
    }

    public Wall? FindWallByName(string name)
    {
        foreach (var floor in Building.Floors)
        {
            var wall = floor.Walls.FirstOrDefault(w => w.Name == name);
            if (wall != null) return wall;
        }
        return null;
    }

    public IEnumerable<Wall> ActiveWalls => ActiveFloor.Walls;

    /// Find which floor a wall belongs to
    public Floor? FindFloorForWall(Wall wall)
    {
        foreach (var floor in Building.Floors)
            if (floor.Walls.Contains(wall)) return floor;
        return null;
    }

    /// Find which floor a room belongs to
    public Floor? FindFloorForRoom(Room room)
    {
        foreach (var floor in Building.Floors)
            if (floor.Rooms.Contains(room)) return floor;
        return null;
    }
}
