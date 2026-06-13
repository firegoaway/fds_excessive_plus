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

/// <summary>
/// Central editor state shared between viewport, property panel, and toolbar.
/// </summary>
public class EditorState
{
    // Domain model
    public Building Building { get; set; } = new();
    public Floor ActiveFloor => Building.Floors.Count > 0
        ? Building.Floors[0]
        : CreateDefaultFloor();

    // Command history
    public CommandHistory History { get; } = new();

    // Tool state
    public ToolMode CurrentTool { get; set; } = ToolMode.Select;

    // Selection
    public HashSet<Wall> SelectedWalls { get; } = new();

    // Snap
    public SnapEngine SnapEngine { get; } = new();

    // Transform state
    public TransformMode ActiveTransform { get; set; } = TransformMode.None;
    public Point2D TransformStart { get; set; } = new(0, 0);
    public Point2D TransformOriginalStart { get; set; } = new(0, 0);
    public Point2D TransformOriginalEnd { get; set; } = new(0, 0);
    public double TransformOriginalAngle { get; set; }

    // Renderers
    public WallRenderer WallRenderer { get; } = new();
    public GhostRenderer GhostRenderer { get; } = new();

    // Dirty flags
    public bool WallsDirty { get; set; } = true;
    public bool SelectionDirty { get; set; } = true;

    // Events
    public event Action? SelectionChanged;
    public event Action? ToolChanged;

    private Floor CreateDefaultFloor()
    {
        var floor = new Floor { Name = "Etaz 1", Height = 3.0, Level = 0.0 };
        Building.Floors.Add(floor);
        return floor;
    }

    // --- Wall tool ---

    public void BeginWallDraw(Point2D start)
    {
        var ghost = new Wall
        {
            Name = "ghost",
            Start = start,
            End = start,
            Thickness = 0.2,
            Height = ActiveFloor.Height,
            BottomZ = ActiveFloor.Level,
            Rgb = [180, 180, 180]
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
            Start = start,
            End = end,
            Thickness = 0.2,
            Height = ActiveFloor.Height,
            BottomZ = ActiveFloor.Level,
            Material = Building.Settings.DefaultMaterial,
            Rgb = [180, 180, 180]
        };

        var cmd = new AddWallCommand(ActiveFloor, wall);
        History.Execute(cmd);
        WallsDirty = true;

        return wall;
    }

    // --- Selection ---

    public void SelectWall(Wall? wall, bool addToSelection = false)
    {
        if (!addToSelection) SelectedWalls.Clear();
        if (wall != null) SelectedWalls.Add(wall);
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        if (SelectedWalls.Count == 0) return;
        SelectedWalls.Clear();
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public Wall? SingleSelectedWall => SelectedWalls.Count == 1 ? SelectedWalls.First() : null;

    // --- Delete ---

    public void DeleteSelected()
    {
        if (SelectedWalls.Count == 0) return;
        var walls = SelectedWalls.ToList();
        var cmd = new DeleteCommand(ActiveFloor, walls);
        History.Execute(cmd);
        SelectedWalls.Clear();
        WallsDirty = true;
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    // --- Move ---

    public void BeginMove(Point2D cursorPos)
    {
        var wall = SingleSelectedWall;
        if (wall == null) return;

        ActiveTransform = TransformMode.Move;
        TransformStart = cursorPos;
        TransformOriginalStart = wall.Start;
        TransformOriginalEnd = wall.End;
    }

    public void UpdateMove(Point2D cursorPos)
    {
        var wall = SingleSelectedWall;
        if (wall == null || ActiveTransform != TransformMode.Move) return;

        var delta = cursorPos - TransformStart;
        wall.Start = TransformOriginalStart + delta;
        wall.End = TransformOriginalEnd + delta;
        WallsDirty = true;
        SelectionDirty = true;

        // Update ghost for preview
        GhostRenderer.GhostWall = new Wall
        {
            Start = wall.Start,
            End = wall.End,
            Thickness = wall.Thickness,
            Height = wall.Height,
            BottomZ = wall.BottomZ,
            Rgb = [100, 200, 100]
        };
    }

    public void ConfirmMove()
    {
        var wall = SingleSelectedWall;
        if (wall == null || ActiveTransform != TransformMode.Move) return;

        var cmd = new MoveObjectCommand(wall, TransformOriginalStart, TransformOriginalEnd, wall.Start, wall.End);
        // Already moved, so just record for undo (don't re-execute)
        History.Execute(new ChangePropertyCommand(
            () => { wall.Start = wall.Start; wall.End = wall.End; },
            () => { wall.Start = TransformOriginalStart; wall.End = TransformOriginalEnd; WallsDirty = true; SelectionDirty = true; }
        ));
        GhostRenderer.Clear();
        ActiveTransform = TransformMode.None;
        WallsDirty = true;
    }

    public void CancelMove()
    {
        var wall = SingleSelectedWall;
        if (wall == null) { ActiveTransform = TransformMode.None; GhostRenderer.Clear(); return; }

        wall.Start = TransformOriginalStart;
        wall.End = TransformOriginalEnd;
        GhostRenderer.Clear();
        ActiveTransform = TransformMode.None;
        WallsDirty = true;
        SelectionDirty = true;
    }

    // --- Rotate ---

    public void BeginRotate(Point2D cursorPos)
    {
        var wall = SingleSelectedWall;
        if (wall == null) return;

        ActiveTransform = TransformMode.Rotate;
        TransformOriginalStart = wall.Start;
        TransformOriginalEnd = wall.End;

        var center = new Point2D((wall.Start.X + wall.End.X) / 2, (wall.Start.Y + wall.End.Y) / 2);
        TransformOriginalAngle = Math.Atan2(cursorPos.Y - center.Y, cursorPos.X - center.X);
    }

    public void UpdateRotate(Point2D cursorPos)
    {
        var wall = SingleSelectedWall;
        if (wall == null || ActiveTransform != TransformMode.Rotate) return;

        var center = new Point2D(
            (TransformOriginalStart.X + TransformOriginalEnd.X) / 2,
            (TransformOriginalStart.Y + TransformOriginalEnd.Y) / 2);

        double currentAngle = Math.Atan2(cursorPos.Y - center.Y, cursorPos.X - center.X);
        double angleDelta = currentAngle - TransformOriginalAngle;

        wall.Start = RotatePoint(TransformOriginalStart, center, angleDelta);
        wall.End = RotatePoint(TransformOriginalEnd, center, angleDelta);
        WallsDirty = true;
        SelectionDirty = true;
    }

    public void ConfirmRotate()
    {
        var wall = SingleSelectedWall;
        if (wall == null || ActiveTransform != TransformMode.Rotate) return;

        History.Execute(new ChangePropertyCommand(
            () => { wall.Start = wall.Start; wall.End = wall.End; },
            () => { wall.Start = TransformOriginalStart; wall.End = TransformOriginalEnd; WallsDirty = true; SelectionDirty = true; }
        ));
        ActiveTransform = TransformMode.None;
        WallsDirty = true;
    }

    public void CancelRotate()
    {
        var wall = SingleSelectedWall;
        if (wall == null) { ActiveTransform = TransformMode.None; return; }

        wall.Start = TransformOriginalStart;
        wall.End = TransformOriginalEnd;
        ActiveTransform = TransformMode.None;
        WallsDirty = true;
        SelectionDirty = true;
    }

    // --- Mirror ---

    public void MirrorHorizontal()
    {
        var wall = SingleSelectedWall;
        if (wall == null) return;

        var oldStart = wall.Start;
        var oldEnd = wall.End;
        double cx = (oldStart.X + oldEnd.X) / 2;

        History.Execute(new ChangePropertyCommand(
            () =>
            {
                wall.Start = new Point2D(2 * cx - oldStart.X, oldStart.Y);
                wall.End = new Point2D(2 * cx - oldEnd.X, oldEnd.Y);
            },
            () => { wall.Start = oldStart; wall.End = oldEnd; WallsDirty = true; SelectionDirty = true; }
        ));
        WallsDirty = true;
        SelectionDirty = true;
    }

    public void MirrorVertical()
    {
        var wall = SingleSelectedWall;
        if (wall == null) return;

        var oldStart = wall.Start;
        var oldEnd = wall.End;
        double cy = (oldStart.Y + oldEnd.Y) / 2;

        History.Execute(new ChangePropertyCommand(
            () =>
            {
                wall.Start = new Point2D(oldStart.X, 2 * cy - oldStart.Y);
                wall.End = new Point2D(oldEnd.X, 2 * cy - oldEnd.Y);
            },
            () => { wall.Start = oldStart; wall.End = oldEnd; WallsDirty = true; SelectionDirty = true; }
        ));
        WallsDirty = true;
        SelectionDirty = true;
    }

    // --- Tool switching ---

    public void SetTool(ToolMode mode)
    {
        // Cancel any active transform
        if (ActiveTransform != TransformMode.None)
        {
            if (ActiveTransform == TransformMode.Move) CancelMove();
            else if (ActiveTransform == TransformMode.Rotate) CancelRotate();
        }

        GhostRenderer.Clear();
        CurrentTool = mode;
        ToolChanged?.Invoke();
    }

    // --- Undo/Redo ---

    public void Undo()
    {
        History.Undo();
        WallsDirty = true;
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    public void Redo()
    {
        History.Redo();
        WallsDirty = true;
        SelectionDirty = true;
        SelectionChanged?.Invoke();
    }

    // --- Helpers ---

    private static Point2D RotatePoint(Point2D point, Point2D center, double angle)
    {
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new Point2D(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos);
    }

    /// Get all walls from the active floor (for snap/picking)
    public IEnumerable<Wall> ActiveWalls => ActiveFloor.Walls;
}
