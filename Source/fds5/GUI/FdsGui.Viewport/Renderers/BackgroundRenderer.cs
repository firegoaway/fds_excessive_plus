using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

public class BackgroundRenderer
{
    private int _vao;
    private int _vbo;
    private int _ebo;
    private int _indexCount;
    private bool _initialized;

    public bool IsInitialized => _initialized;
    public int Vao => _vao;
    public int Vbo => _vbo;
    public int Ebo => _ebo;
    public int IndexCount => _indexCount;
    public bool Dirty { get; private set; } = true;

    public void MarkDirty() => Dirty = true;
    public void ClearDirty() => Dirty = false;
    public void SetUninitialized() => _initialized = false;

    public void SetGlResources(int vao, int vbo, int ebo, int indexCount)
    {
        _vao = vao;
        _vbo = vbo;
        _ebo = ebo;
        _indexCount = indexCount;
        Dirty = false;
        _initialized = true;
    }

    /// Generate 4 vertices for a flat quad representing a background.
    /// Vertex format: xyz + rgba (7 floats per vertex).
    public static float[] GenerateBackgroundVertices(Background bg, double floorLevel, float alpha = 1.0f)
    {
        double z = floorLevel - 0.01;
        float r = 0.70f, g = 0.85f, b = 0.70f;
        float a = (float)bg.Opacity * alpha;

        double x1 = bg.Origin.X, y1 = bg.Origin.Y;
        double x2 = bg.Origin.X + bg.RealWidth, y2 = bg.Origin.Y + bg.RealHeight;

        // Apply rotation around origin if non-zero
        if (Math.Abs(bg.Rotation) > 0.001)
        {
            double rad = bg.Rotation * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            double cx = bg.Origin.X, cy = bg.Origin.Y;

            (double x, double y) Rotate(double px, double py)
            {
                double dx = px - cx, dy = py - cy;
                return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
            }

            var (rx1, ry1) = Rotate(x1, y1);
            var (rx2, ry2) = Rotate(x2, y1);
            var (rx3, ry3) = Rotate(x2, y2);
            var (rx4, ry4) = Rotate(x1, y2);

            return
            [
                (float)rx1, (float)ry1, (float)z, r, g, b, a,
                (float)rx2, (float)ry2, (float)z, r, g, b, a,
                (float)rx3, (float)ry3, (float)z, r, g, b, a,
                (float)rx4, (float)ry4, (float)z, r, g, b, a,
            ];
        }

        return
        [
            (float)x1, (float)y1, (float)z, r, g, b, a,
            (float)x2, (float)y1, (float)z, r, g, b, a,
            (float)x2, (float)y2, (float)z, r, g, b, a,
            (float)x1, (float)y2, (float)z, r, g, b, a,
        ];
    }

    /// Quad indices (2 triangles).
    public static ushort[] GetQuadIndices() => [0, 1, 2, 0, 2, 3];

    /// Generate outline (wireframe) vertices for selection highlight.
    public static float[] GenerateOutlineVertices(Background bg, double floorLevel)
    {
        var verts = GenerateBackgroundVertices(bg, floorLevel);
        if (verts.Length == 0) return [];

        var outline = new float[4 * 7]; // 4 vertices for outline quad
        for (int i = 0; i < 4; i++)
        {
            outline[i * 7] = verts[i * 7];
            outline[i * 7 + 1] = verts[i * 7 + 1];
            outline[i * 7 + 2] = verts[i * 7 + 2];
            outline[i * 7 + 3] = 1.0f;  // R
            outline[i * 7 + 4] = 0.9f;  // G
            outline[i * 7 + 5] = 0.0f;  // B
            outline[i * 7 + 6] = 1.0f;  // A
        }
        return outline;
    }

    /// Line indices for outline (4 edges).
    public static ushort[] GetOutlineLineIndices() => [0, 1, 1, 2, 2, 3, 3, 0];

    public static (float[] vertices, ushort[] indices) GenerateBatchedGeometry(
        IList<(Background bg, double floorLevel)> backgrounds)
    {
        int count = backgrounds.Count;
        if (count == 0) return ([], []);

        var vertices = new float[count * 4 * 7];
        var indices = new ushort[count * 6];
        var baseIndices = GetQuadIndices();

        for (int i = 0; i < count; i++)
        {
            var (bg, level) = backgrounds[i];
            var bgVerts = GenerateBackgroundVertices(bg, level);
            Array.Copy(bgVerts, 0, vertices, i * 28, 28);

            ushort offset = (ushort)(i * 4);
            for (int j = 0; j < 6; j++)
                indices[i * 6 + j] = (ushort)(baseIndices[j] + offset);
        }

        return (vertices, indices);
    }
}
