using FdsGui.Core.Models;

namespace FdsGui.Viewport.Snapping;

public class SnapResult
{
    public Point2D Position { get; set; } = new(0, 0);
    public bool IsSnapped { get; set; }
    public SnapMode SnapType { get; set; }
}

public class SnapEngine
{
    public bool IsEnabled { get; set; } = true;
    public double GridStep { get; set; } = 0.5;
    public double SnapRadius { get; set; } = 0.3;

    private static readonly double[] GridSteps = [0.1, 0.25, 0.5, 1.0];
    private int _gridStepIndex = 2;

    public void IncreaseGridStep()
    {
        if (_gridStepIndex < GridSteps.Length - 1)
        {
            _gridStepIndex++;
            GridStep = GridSteps[_gridStepIndex];
        }
    }

    public void DecreaseGridStep()
    {
        if (_gridStepIndex > 0)
        {
            _gridStepIndex--;
            GridStep = GridSteps[_gridStepIndex];
        }
    }

    public SnapResult Snap(Point2D cursor, IEnumerable<Wall> existingWalls)
    {
        if (!IsEnabled)
            return new SnapResult { Position = cursor, IsSnapped = false };

        var walls = existingWalls.ToList();

        // Priority: Endpoint > Midpoint > Intersection > Grid
        var endpointSnap = TrySnapEndpoint(cursor, walls);
        if (endpointSnap != null) return endpointSnap;

        var midpointSnap = TrySnapMidpoint(cursor, walls);
        if (midpointSnap != null) return midpointSnap;

        var intersectionSnap = TrySnapIntersection(cursor, walls);
        if (intersectionSnap != null) return intersectionSnap;

        // Grid snap
        return SnapToGrid(cursor);
    }

    private SnapResult? TrySnapEndpoint(Point2D cursor, List<Wall> walls)
    {
        foreach (var wall in walls)
        {
            if (cursor.DistanceTo(wall.Start) < SnapRadius)
                return new SnapResult { Position = wall.Start, IsSnapped = true, SnapType = SnapMode.Endpoint };
            if (cursor.DistanceTo(wall.End) < SnapRadius)
                return new SnapResult { Position = wall.End, IsSnapped = true, SnapType = SnapMode.Endpoint };
        }
        return null;
    }

    private SnapResult? TrySnapMidpoint(Point2D cursor, List<Wall> walls)
    {
        foreach (var wall in walls)
        {
            var mid = new Point2D((wall.Start.X + wall.End.X) / 2, (wall.Start.Y + wall.End.Y) / 2);
            if (cursor.DistanceTo(mid) < SnapRadius)
                return new SnapResult { Position = mid, IsSnapped = true, SnapType = SnapMode.Midpoint };
        }
        return null;
    }

    private SnapResult? TrySnapIntersection(Point2D cursor, List<Wall> walls)
    {
        for (int i = 0; i < walls.Count; i++)
        {
            for (int j = i + 1; j < walls.Count; j++)
            {
                var intersection = GetLineIntersection(walls[i], walls[j]);
                if (intersection != null && cursor.DistanceTo(intersection) < SnapRadius)
                    return new SnapResult { Position = intersection, IsSnapped = true, SnapType = SnapMode.Intersection };
            }
        }
        return null;
    }

    private SnapResult SnapToGrid(Point2D cursor)
    {
        double snappedX = Math.Round(cursor.X / GridStep) * GridStep;
        double snappedY = Math.Round(cursor.Y / GridStep) * GridStep;
        var snapped = new Point2D(snappedX, snappedY);
        bool isSnapped = cursor.DistanceTo(snapped) < SnapRadius;

        return new SnapResult
        {
            Position = isSnapped ? snapped : cursor,
            IsSnapped = isSnapped,
            SnapType = SnapMode.Grid
        };
    }

    private static Point2D? GetLineIntersection(Wall a, Wall b)
    {
        double x1 = a.Start.X, y1 = a.Start.Y, x2 = a.End.X, y2 = a.End.Y;
        double x3 = b.Start.X, y3 = b.Start.Y, x4 = b.End.X, y4 = b.End.Y;

        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10) return null;

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;

        double ix = x1 + t * (x2 - x1);
        double iy = y1 + t * (y2 - y1);
        return new Point2D(ix, iy);
    }
}
