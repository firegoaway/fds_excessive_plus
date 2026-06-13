using FdsGui.Core.Models;
using OpenTK.Mathematics;

namespace FdsGui.Viewport.Picking;

/// <summary>
/// Ray casting for picking walls in the 3D viewport.
/// </summary>
public class ObjectPicker
{
    /// Unproject screen coordinates to a world-space ray.
    public static Ray ScreenToRay(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection)
    {
        // NDC coordinates
        float ndcX = (float)(2.0 * screenX / viewportWidth - 1.0);
        float ndcY = (float)(1.0 - 2.0 * screenY / viewportHeight);

        // Unproject near and far points
        Matrix4 invVP = (view * projection).Inverted();

        Vector4 nearPt = new Vector4(ndcX, ndcY, -1.0f, 1.0f) * invVP;
        Vector4 farPt = new Vector4(ndcX, ndcY, 1.0f, 1.0f) * invVP;

        if (MathF.Abs(nearPt.W) > 1e-6f) nearPt /= nearPt.W;
        if (MathF.Abs(farPt.W) > 1e-6f) farPt /= farPt.W;

        var origin = new Vector3(nearPt.X, nearPt.Y, nearPt.Z);
        var direction = (new Vector3(farPt.X, farPt.Y, farPt.Z) - origin).Normalized();

        return new Ray(origin, direction);
    }

    /// Unproject screen coordinates to a point on the Z=0 plane.
    public static Point2D ScreenToGroundPlane(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        if (MathF.Abs(ray.Direction.Z) < 1e-6f)
            return new Point2D(ray.Origin.X, ray.Origin.Y);

        float t = -ray.Origin.Z / ray.Direction.Z;
        return new Point2D(ray.Origin.X + ray.Direction.X * t, ray.Origin.Y + ray.Direction.Y * t);
    }

    /// Pick the nearest wall under the cursor using ray-box intersection.
    public static Wall? PickWall(double screenX, double screenY, int viewportWidth, int viewportHeight,
        Matrix4 view, Matrix4 projection, IEnumerable<Wall> walls)
    {
        var ray = ScreenToRay(screenX, screenY, viewportWidth, viewportHeight, view, projection);

        Wall? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var wall in walls)
        {
            var (min, max) = GetWallBoundingBox(wall);
            float? dist = RayBoxIntersect(ray, min, max);
            if (dist.HasValue && dist.Value < nearestDist)
            {
                nearestDist = dist.Value;
                nearest = wall;
            }
        }

        return nearest;
    }

    private static (Vector3 min, Vector3 max) GetWallBoundingBox(Wall wall)
    {
        var xb = wall.ToXb();
        return (new Vector3((float)xb[0], (float)xb[2], (float)xb[4]),
                new Vector3((float)xb[1], (float)xb[3], (float)xb[5]));
    }

    /// Ray-AABB intersection (slab method).
    private static float? RayBoxIntersect(Ray ray, Vector3 min, Vector3 max)
    {
        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;

        // X slab
        if (MathF.Abs(ray.Direction.X) > 1e-8f)
        {
            float t1 = (min.X - ray.Origin.X) / ray.Direction.X;
            float t2 = (max.X - ray.Origin.X) / ray.Direction.X;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
        }
        else if (ray.Origin.X < min.X || ray.Origin.X > max.X) return null;

        // Y slab
        if (MathF.Abs(ray.Direction.Y) > 1e-8f)
        {
            float t1 = (min.Y - ray.Origin.Y) / ray.Direction.Y;
            float t2 = (max.Y - ray.Origin.Y) / ray.Direction.Y;
            tMin = MathF.Max(tMin, MathF.Min(t1, t2));
            tMax = MathF.Min(tMax, MathF.Max(t1, t2));
        }
        else if (ray.Origin.Y < min.Y || ray.Origin.Y > max.Y) return null;

        // Z slab
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
