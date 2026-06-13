using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

public class SlabRenderer
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

    /// Generate 8 vertices for a horizontal slab (rectangular prism).
    public static float[] GenerateSlabVertices(Slab slab, float alpha = 1.0f)
    {
        var pts = slab.Outline.Points;
        if (pts.Count < 4) return [];

        double z1 = slab.BottomZ;
        double z2 = slab.BottomZ + slab.Thickness;

        float r = 200f / 255f;
        float g = 200f / 255f;
        float b = 200f / 255f;
        float a = alpha;

        // Use first 4 points of the rectangle
        var p0 = pts[0]; var p1 = pts[1]; var p2 = pts[2]; var p3 = pts[3];

        return
        [
            // Bottom (0-3)
            (float)p0.X, (float)p0.Y, (float)z1, r, g, b, a,
            (float)p1.X, (float)p1.Y, (float)z1, r, g, b, a,
            (float)p2.X, (float)p2.Y, (float)z1, r, g, b, a,
            (float)p3.X, (float)p3.Y, (float)z1, r, g, b, a,
            // Top (4-7)
            (float)p0.X, (float)p0.Y, (float)z2, r, g, b, a,
            (float)p1.X, (float)p1.Y, (float)z2, r, g, b, a,
            (float)p2.X, (float)p2.Y, (float)z2, r, g, b, a,
            (float)p3.X, (float)p3.Y, (float)z2, r, g, b, a,
        ];
    }

    public static ushort[] GetBoxIndices() => WallRenderer.GetBoxIndices();

    public static (float[] vertices, ushort[] indices) GenerateBatchedGeometry(IList<Slab> slabs)
    {
        var valid = slabs.Where(s => s.Outline.Points.Count >= 4).ToList();
        int count = valid.Count;
        if (count == 0) return ([], []);

        var vertices = new float[count * 8 * 7];
        var indices = new ushort[count * 36];
        var baseIndices = GetBoxIndices();

        for (int i = 0; i < count; i++)
        {
            var slabVerts = GenerateSlabVertices(valid[i]);
            Array.Copy(slabVerts, 0, vertices, i * 56, 56);

            ushort offset = (ushort)(i * 8);
            for (int j = 0; j < 36; j++)
                indices[i * 36 + j] = (ushort)(baseIndices[j] + offset);
        }

        return (vertices, indices);
    }

    public static float[] GenerateOutlineVertices(Slab slab, float expand = 0.03f)
    {
        var verts = GenerateSlabVertices(slab);
        if (verts.Length == 0) return [];
        var outline = new float[verts.Length];

        double cx = 0, cy = 0, cz = 0;
        for (int i = 0; i < 8; i++)
        {
            cx += verts[i * 7]; cy += verts[i * 7 + 1]; cz += verts[i * 7 + 2];
        }
        cx /= 8; cy /= 8; cz /= 8;

        for (int i = 0; i < 8; i++)
        {
            float vx = verts[i * 7], vy = verts[i * 7 + 1], vz = verts[i * 7 + 2];
            float dx = vx - (float)cx, dy = vy - (float)cy, dz = vz - (float)cz;
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len > 1e-6f) { dx /= len; dy /= len; dz /= len; }

            outline[i * 7] = vx + dx * expand;
            outline[i * 7 + 1] = vy + dy * expand;
            outline[i * 7 + 2] = vz + dz * expand;
            outline[i * 7 + 3] = 1.0f;
            outline[i * 7 + 4] = 0.9f;
            outline[i * 7 + 5] = 0.0f;
            outline[i * 7 + 6] = 1.0f;
        }

        return outline;
    }
}
