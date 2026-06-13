using FdsGui.Core.Models;
using OpenTK.Mathematics;

namespace FdsGui.Viewport.Picking;

public class ObjectPicker
{
    public static Ray ScreenToRay(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection)
    {
        float ndcX = (float)(2.0 * screenX / viewportWidth - 1.0);
        float ndcY = (float)(1.0 - 2.0 * screenY / viewportHeight);

        Matrix4 invVP = (view * projection).Inverted();

        Vector4 nearPt = new Vector4(ndcX, ndcY, -1.0f, 1.0f) * invVP;
        Vector4 farPt = new Vector4(ndcX, ndcY, 1.0f, 1.0f) * invVP;

        if (MathF.Abs(nearPt.W) > 1e-6f) nearPt /= nearPt.W;
        if (MathF.Abs(farPt.W) > 1e-6f) farPt /= farPt.W;

        var origin = new Vector3(nearPt.X, nearPt.Y, nearPt.Z);
        var direction = (new Vector3(farPt.X, farPt.Y, farPt.Z) - origin).Normalized();

        return new Ray(origin, direction);
    }

    public static Point2D ScreenToGroundPlane(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection)
    {
        return ScreenToGroundPlane(screenX, screenY, viewportWidth, viewportHeight, view, projection, 0.0);
    }

    public static Point2D ScreenToGroundPlane(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, double groundZ)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        if (MathF.Abs(ray.Direction.Z) < 1e-6f)
            return new Point2D(ray.Origin.X, ray.Origin.Y);

        float t = (float)((groundZ - ray.Origin.Z) / ray.Direction.Z);
        return new Point2D(ray.Origin.X + ray.Direction.X * t, ray.Origin.Y + ray.Direction.Y * t);
    }

    public static Wall? PickWall(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, IEnumerable<Wall> walls)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        Wall? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var wall in walls)
        {
            var (min, max) = GetBoundingBox(wall.ToXb());
            float? dist = RayBoxIntersect(ray, min, max);
            if (dist.HasValue && dist.Value < nearestDist)
            {
                nearestDist = dist.Value;
                nearest = wall;
            }
        }

        return nearest;
    }

    public static Door? PickDoor(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, IEnumerable<Door> doors, Func<string, Wall?> findWall)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        Door? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var door in doors)
        {
            var wall = findWall(door.ParentWallId);
            if (wall == null) continue;

            var xb = GetDoorBoundingBox(door, wall);
            var (min, max) = GetBoundingBox(xb);
            float? dist = RayBoxIntersect(ray, min, max);
            if (dist.HasValue && dist.Value < nearestDist)
            {
                nearestDist = dist.Value;
                nearest = door;
            }
        }

        return nearest;
    }

    public static Slab? PickSlab(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, IEnumerable<Slab> slabs)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        Slab? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var slab in slabs)
        {
            if (slab.Outline.Points.Count < 4) continue;
            var xb = GetSlabBoundingBox(slab);
            var (min, max) = GetBoundingBox(xb);
            float? dist = RayBoxIntersect(ray, min, max);
            if (dist.HasValue && dist.Value < nearestDist)
            {
                nearestDist = dist.Value;
                nearest = slab;
            }
        }

        return nearest;
    }

    public static StairFlight? PickStair(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, IEnumerable<StairFlight> stairs)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        StairFlight? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var stair in stairs)
        {
            var xb = GetStairBoundingBox(stair);
            var (min, max) = GetBoundingBox(xb);
            float? dist = RayBoxIntersect(ray, min, max);
            if (dist.HasValue && dist.Value < nearestDist)
            {
                nearestDist = dist.Value;
                nearest = stair;
            }
        }

        return nearest;
    }

    public static Room? PickRoom(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, IEnumerable<Room> rooms)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        Room? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var room in rooms)
        {
            if (room.Outline.Points.Count < 3) continue;
            var xb = room.ToXb();
            var (min, max) = GetBoundingBox(xb);
            float? dist = RayBoxIntersect(ray, min, max);
            if (dist.HasValue && dist.Value < nearestDist)
            {
                nearestDist = dist.Value;
                nearest = room;
            }
        }

        return nearest;
    }

    public static Background? PickBackground(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, IEnumerable<(Background bg, double floorLevel)> backgrounds)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        Background? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var (bg, floorLevel) in backgrounds)
        {
            double z = floorLevel - 0.02;
            double x1 = bg.Origin.X, y1 = bg.Origin.Y;
            double x2 = bg.Origin.X + bg.RealWidth, y2 = bg.Origin.Y + bg.RealHeight;

            var min = new Vector3((float)Math.Min(x1, x2), (float)Math.Min(y1, y2), (float)z);
            var max = new Vector3((float)Math.Max(x1, x2), (float)Math.Max(y1, y2), (float)(z + 0.04));
            float? dist = RayBoxIntersect(ray, min, max);
            if (dist.HasValue && dist.Value < nearestDist)
            {
                nearestDist = dist.Value;
                nearest = bg;
            }
        }

        return nearest;
    }

    private static double[] GetDoorBoundingBox(Door door, Wall wall)
    {
        var dir = wall.Direction;
        var normal = wall.Normal;
        var halfW = dir * (door.Width / 2.0);
        var halfT = normal * (wall.Thickness / 2.0);

        var c = door.Position;
        var s = c - halfW;
        var e = c + halfW;

        var corners = new[] { s - halfT, s + halfT, e + halfT, e - halfT };
        double minX = corners.Min(p => p.X);
        double maxX = corners.Max(p => p.X);
        double minY = corners.Min(p => p.Y);
        double maxY = corners.Max(p => p.Y);

        return [minX, maxX, minY, maxY, door.BottomZ, door.BottomZ + door.Height];
    }

    private static double[] GetSlabBoundingBox(Slab slab)
    {
        return [slab.Outline.MinX, slab.Outline.MaxX,
                slab.Outline.MinY, slab.Outline.MaxY,
                slab.BottomZ, slab.BottomZ + slab.Thickness];
    }

    private static double[] GetStairBoundingBox(StairFlight stair)
    {
        var steps = stair.GenerateSteps();
        if (steps.Count == 0)
            return [0, 0, 0, 0, 0, 0];

        return [steps.Min(s => s[0]), steps.Max(s => s[1]),
                steps.Min(s => s[2]), steps.Max(s => s[3]),
                steps[0][4], steps[^1][5]];
    }

    private static (Vector3 min, Vector3 max) GetBoundingBox(double[] xb)
    {
        return (new Vector3((float)xb[0], (float)xb[2], (float)xb[4]),
                new Vector3((float)xb[1], (float)xb[3], (float)xb[5]));
    }

    private static float? RayBoxIntersect(Ray ray, Vector3 min, Vector3 max)
    {
        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;

        if (MathF.Abs(ray.Direction.X) > 1e-8f)
        {
            float t1 = (min.X - ray.Origin.X) / ray.Direction.X;
            float t2 = (max.X - ray.Origin.X) / ray.Direction.X;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
        }
        else if (ray.Origin.X < min.X || ray.Origin.X > max.X) return null;

        if (MathF.Abs(ray.Direction.Y) > 1e-8f)
        {
            float t1 = (min.Y - ray.Origin.Y) / ray.Direction.Y;
            float t2 = (max.Y - ray.Origin.Y) / ray.Direction.Y;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
        }
        else if (ray.Origin.Y < min.Y || ray.Origin.Y > max.Y) return null;

        if (MathF.Abs(ray.Direction.Z) > 1e-8f)
        {
            float t1 = (min.Z - ray.Origin.Z) / ray.Direction.Z;
            float t2 = (max.Z - ray.Origin.Z) / ray.Direction.Z;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
        }
        else if (ray.Origin.Z < min.Z || ray.Origin.Z > max.Z) return null;

        if (tMin > tMax || tMax < 0) return null;
        return tMin >= 0 ? tMin : tMax;
    }
}

public readonly struct Ray
{
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }

    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = direction;
    }
}
