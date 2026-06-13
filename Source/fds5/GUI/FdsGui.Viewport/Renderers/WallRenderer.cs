using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

/// <summary>
/// Generates geometry for wall 3D boxes and manages batched OpenGL buffers.
/// </summary>
public class WallRenderer
{
    private int _vao;
    private int _vbo;
    private int _ebo;
    private int _indexCount;
    private bool _dirty = true;
    private bool _initialized;

    public bool IsInitialized => _initialized;

    public void MarkDirty() => _dirty = true;

    /// Generate 8 vertices for a wall box: float[7] = xyz + rgba
    public static float[] GenerateWallVertices(Wall wall, float alpha = 1.0f)
    {
        var dir = wall.Direction;
        var n = new Point2D(-dir.Y, dir.X);
        var halfN = n * (wall.Thickness / 2.0);

        var s = wall.Start;
        var e = wall.End;
        double z1 = wall.BottomZ;
        double z2 = wall.BottomZ + wall.Height;

        // 4 bottom corners
        var b0 = s - halfN;
        var b1 = s + halfN;
        var b2 = e + halfN;
        var b3 = e - halfN;

        float r = wall.Rgb[0] / 255f;
        float g = wall.Rgb[1] / 255f;
        float b = wall.Rgb[2] / 255f;
        float a = alpha;

        return
        [
            // Bottom face vertices (0-3)
            (float)b0.X, (float)b0.Y, (float)z1, r, g, b, a,
            (float)b1.X, (float)b1.Y, (float)z1, r, g, b, a,
            (float)b2.X, (float)b2.Y, (float)z1, r, g, b, a,
            (float)b3.X, (float)b3.Y, (float)z1, r, g, b, a,
            // Top face vertices (4-7)
            (float)b0.X, (float)b0.Y, (float)z2, r, g, b, a,
            (float)b1.X, (float)b1.Y, (float)z2, r, g, b, a,
            (float)b2.X, (float)b2.Y, (float)z2, r, g, b, a,
            (float)b3.X, (float)b3.Y, (float)z2, r, g, b, a,
        ];
    }

    /// Standard box indices: 36 indices (12 triangles)
    public static ushort[] GetBoxIndices() =>
    [
        // Bottom (0,1,2,3)
        0, 2, 1,  0, 3, 2,
        // Top (4,5,6,7)
        4, 5, 6,  4, 6, 7,
        // Front (0,1,5,4)
        0, 1, 5,  0, 5, 4,
        // Back (2,3,7,6)
        2, 3, 7,  2, 7, 6,
        // Left (0,3,7,4)
        0, 3, 7,  0, 7, 4,
        // Right (1,2,6,5)
        1, 2, 6,  1, 6, 5,
    ];

    /// Generate all wall vertices batched. Each wall gets 8 vertices offset by wallIndex * 8.
    public static (float[] vertices, ushort[] indices) GenerateBatchedGeometry(IList<Wall> walls)
    {
        int wallCount = walls.Count;
        var vertices = new float[wallCount * 8 * 7];
        var indices = new ushort[wallCount * 36];
        var baseIndices = GetBoxIndices();

        for (int i = 0; i < wallCount; i++)
        {
            var wallVerts = GenerateWallVertices(walls[i]);
            Array.Copy(wallVerts, 0, vertices, i * 56, 56);

            ushort offset = (ushort)(i * 8);
            for (int j = 0; j < 36; j++)
                indices[i * 36 + j] = (ushort)(baseIndices[j] + offset);
        }

        return (vertices, indices);
    }

    /// Generate selection outline vertices (wireframe edges, slightly expanded)
    public static float[] GenerateOutlineVertices(Wall wall, float expand = 0.02f)
    {
        var verts = GenerateWallVertices(wall);
        var outline = new float[verts.Length];

        // Compute center
        double cx = 0, cy = 0, cz = 0;
        for (int i = 0; i < 8; i++)
        {
            cx += verts[i * 7];
            cy += verts[i * 7 + 1];
            cz += verts[i * 7 + 2];
        }
        cx /= 8; cy /= 8; cz /= 8;

        // Expand each vertex outward from center
        for (int i = 0; i < 8; i++)
        {
            float vx = verts[i * 7];
            float vy = verts[i * 7 + 1];
            float vz = verts[i * 7 + 2];

            float dx = vx - (float)cx;
            float dy = vy - (float)cy;
            float dz = vz - (float)cz;
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len > 1e-6f)
            {
                dx /= len; dy /= len; dz /= len;
            }

            outline[i * 7] = vx + dx * expand;
            outline[i * 7 + 1] = vy + dy * expand;
            outline[i * 7 + 2] = vz + dz * expand;
            outline[i * 7 + 3] = 1.0f; // R (yellow)
            outline[i * 7 + 4] = 0.9f; // G
            outline[i * 7 + 5] = 0.0f; // B
            outline[i * 7 + 6] = 1.0f; // A
        }

        return outline;
    }

    /// 12 box edges as line pairs (24 indices for GL_LINES)
    public static ushort[] GetBoxEdgeIndices() =>
    [
        // Bottom edges
        0, 1,  1, 2,  2, 3,  3, 0,
        // Top edges
        4, 5,  5, 6,  6, 7,  7, 4,
        // Vertical edges
        0, 4,  1, 5,  2, 6,  3, 7,
    ];

    // GL resource management is handled by GlViewportControl
    public int Vao => _vao;
    public int Vbo => _vbo;
    public int Ebo => _ebo;
    public int IndexCount => _indexCount;
    public bool Dirty => _dirty;

    public void SetGlResources(int vao, int vbo, int ebo, int indexCount)
    {
        _vao = vao;
        _vbo = vbo;
        _ebo = ebo;
        _indexCount = indexCount;
        _dirty = false;
        _initialized = true;
    }

    public void ClearDirty() => _dirty = false;
    public void SetUninitialized() => _initialized = false;
}
