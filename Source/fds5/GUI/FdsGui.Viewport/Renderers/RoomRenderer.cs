using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

public class RoomRenderer
{
    /// Generate vertices for an extruded polygon (room volume).
    /// For N-point polygons, generates 2N vertices (N bottom + N top).
    public static float[] GenerateRoomVertices(Room room, float alpha = 0.15f)
    {
        var pts = room.Outline.Points;
        if (pts.Count < 3) return [];

        float r = 100f / 255f;
        float g = 150f / 255f;
        float b = 255f / 255f;
        float a = alpha;

        double z1 = room.BottomZ;
        double z2 = room.TopZ;
        int n = pts.Count;
        var vertices = new float[n * 2 * 7];

        // Bottom face (0..n-1)
        for (int i = 0; i < n; i++)
        {
            vertices[i * 7] = (float)pts[i].X;
            vertices[i * 7 + 1] = (float)pts[i].Y;
            vertices[i * 7 + 2] = (float)z1;
            vertices[i * 7 + 3] = r;
            vertices[i * 7 + 4] = g;
            vertices[i * 7 + 5] = b;
            vertices[i * 7 + 6] = a;
        }

        // Top face (n..2n-1)
        for (int i = 0; i < n; i++)
        {
            int idx = (n + i) * 7;
            vertices[idx] = (float)pts[i].X;
            vertices[idx + 1] = (float)pts[i].Y;
            vertices[idx + 2] = (float)z2;
            vertices[idx + 3] = r;
            vertices[idx + 4] = g;
            vertices[idx + 5] = b;
            vertices[idx + 6] = a;
        }

        return vertices;
    }

    /// Generate indices for an extruded polygon with N sides.
    /// Returns indices for: bottom fan, top fan, and N side quads.
    public static ushort[] GenerateIndices(int pointCount)
    {
        if (pointCount < 3) return [];

        int n = pointCount;
        var indices = new List<ushort>();

        // Bottom face (fan from vertex 0)
        for (int i = 1; i < n - 1; i++)
        {
            indices.Add(0);
            indices.Add((ushort)(i + 1));
            indices.Add((ushort)i);
        }

        // Top face (fan from vertex n)
        for (int i = 1; i < n - 1; i++)
        {
            indices.Add((ushort)n);
            indices.Add((ushort)(n + i));
            indices.Add((ushort)(n + i + 1));
        }

        // Side faces (quads)
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            ushort b0 = (ushort)i;
            ushort b1 = (ushort)next;
            ushort t0 = (ushort)(i + n);
            ushort t1 = (ushort)(next + n);

            indices.Add(b0);
            indices.Add(b1);
            indices.Add(t1);

            indices.Add(b0);
            indices.Add(t1);
            indices.Add(t0);
        }

        return indices.ToArray();
    }

    /// Generate batched geometry for multiple rooms.
    public static (float[] vertices, ushort[] indices) GenerateBatchedGeometry(IList<Room> rooms)
    {
        var valid = rooms.Where(r => r.Outline.Points.Count >= 3).ToList();
        if (valid.Count == 0) return ([], []);

        var allVertices = new List<float>();
        var allIndices = new List<ushort>();
        ushort vertexOffset = 0;

        foreach (var room in valid)
        {
            var verts = GenerateRoomVertices(room);
            var roomIndices = GenerateIndices(room.Outline.Points.Count);

            allVertices.AddRange(verts);

            foreach (var idx in roomIndices)
                allIndices.Add((ushort)(idx + vertexOffset));

            vertexOffset += (ushort)(room.Outline.Points.Count * 2);
        }

        return (allVertices.ToArray(), allIndices.ToArray());
    }

    /// Generate outline vertices for selection (wireframe, yellow).
    public static float[] GenerateOutlineVertices(Room room, float expand = 0.03f)
    {
        var pts = room.Outline.Points;
        if (pts.Count < 3) return [];

        int n = pts.Count;
        double z1 = room.BottomZ;
        double z2 = room.TopZ;
        var vertices = new float[n * 2 * 7];

        for (int i = 0; i < n; i++)
        {
            vertices[i * 7] = (float)pts[i].X;
            vertices[i * 7 + 1] = (float)pts[i].Y;
            vertices[i * 7 + 2] = (float)z1;
            vertices[i * 7 + 3] = 1.0f;
            vertices[i * 7 + 4] = 0.9f;
            vertices[i * 7 + 5] = 0.0f;
            vertices[i * 7 + 6] = 1.0f;
        }

        for (int i = 0; i < n; i++)
        {
            int idx = (n + i) * 7;
            vertices[idx] = (float)pts[i].X;
            vertices[idx + 1] = (float)pts[i].Y;
            vertices[idx + 2] = (float)z2;
            vertices[idx + 3] = 1.0f;
            vertices[idx + 4] = 0.9f;
            vertices[idx + 5] = 0.0f;
            vertices[idx + 6] = 1.0f;
        }

        return vertices;
    }

    /// Edge indices for wireframe outline of an N-point extruded polygon.
    public static ushort[] GenerateOutlineEdgeIndices(int pointCount)
    {
        if (pointCount < 3) return [];
        int n = pointCount;
        var indices = new List<ushort>();

        // Bottom edges
        for (int i = 0; i < n; i++)
        {
            indices.Add((ushort)i);
            indices.Add((ushort)((i + 1) % n));
        }

        // Top edges
        for (int i = 0; i < n; i++)
        {
            indices.Add((ushort)(n + i));
            indices.Add((ushort)(n + (i + 1) % n));
        }

        // Vertical edges
        for (int i = 0; i < n; i++)
        {
            indices.Add((ushort)i);
            indices.Add((ushort)(n + i));
        }

        return indices.ToArray();
    }
}
