using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

public class StairRenderer
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

    /// Generate all step boxes for a stair flight as a single batched geometry.
    public static (float[] vertices, ushort[] indices) GenerateStairGeometry(StairFlight stair, float alpha = 1.0f)
    {
        var steps = stair.GenerateSteps();
        int stepCount = steps.Count;
        if (stepCount == 0) return ([], []);

        var vertices = new float[stepCount * 8 * 7];
        var indices = new ushort[stepCount * 36];
        var baseIndices = WallRenderer.GetBoxIndices();

        float r = 100f / 255f;
        float g = 180f / 255f;
        float b = 100f / 255f;
        float a = alpha;

        for (int i = 0; i < stepCount; i++)
        {
            var xb = steps[i]; // x1, x2, y1, y2, z1, z2
            double x1 = xb[0], x2 = xb[1], y1 = xb[2], y2 = xb[3], z1 = xb[4], z2 = xb[5];

            int vi = i * 56;
            // Bottom (0-3)
            vertices[vi + 0] = (float)x1; vertices[vi + 1] = (float)y1; vertices[vi + 2] = (float)z1;
            vertices[vi + 3] = r; vertices[vi + 4] = g; vertices[vi + 5] = b; vertices[vi + 6] = a;

            vertices[vi + 7] = (float)x2; vertices[vi + 8] = (float)y1; vertices[vi + 9] = (float)z1;
            vertices[vi + 10] = r; vertices[vi + 11] = g; vertices[vi + 12] = b; vertices[vi + 13] = a;

            vertices[vi + 14] = (float)x2; vertices[vi + 15] = (float)y2; vertices[vi + 16] = (float)z1;
            vertices[vi + 17] = r; vertices[vi + 18] = g; vertices[vi + 19] = b; vertices[vi + 20] = a;

            vertices[vi + 21] = (float)x1; vertices[vi + 22] = (float)y2; vertices[vi + 23] = (float)z1;
            vertices[vi + 24] = r; vertices[vi + 25] = g; vertices[vi + 26] = b; vertices[vi + 27] = a;

            // Top (4-7)
            vertices[vi + 28] = (float)x1; vertices[vi + 29] = (float)y1; vertices[vi + 30] = (float)z2;
            vertices[vi + 31] = r; vertices[vi + 32] = g; vertices[vi + 33] = b; vertices[vi + 34] = a;

            vertices[vi + 35] = (float)x2; vertices[vi + 36] = (float)y1; vertices[vi + 37] = (float)z2;
            vertices[vi + 38] = r; vertices[vi + 39] = g; vertices[vi + 40] = b; vertices[vi + 41] = a;

            vertices[vi + 42] = (float)x2; vertices[vi + 43] = (float)y2; vertices[vi + 44] = (float)z2;
            vertices[vi + 45] = r; vertices[vi + 46] = g; vertices[vi + 47] = b; vertices[vi + 48] = a;

            vertices[vi + 49] = (float)x1; vertices[vi + 50] = (float)y2; vertices[vi + 51] = (float)z2;
            vertices[vi + 52] = r; vertices[vi + 53] = g; vertices[vi + 54] = b; vertices[vi + 55] = a;

            ushort offset = (ushort)(i * 8);
            for (int j = 0; j < 36; j++)
                indices[i * 36 + j] = (ushort)(baseIndices[j] + offset);
        }

        return (vertices, indices);
    }

    /// Batched geometry for multiple stair flights.
    public static (float[] vertices, ushort[] indices) GenerateBatchedGeometry(IList<StairFlight> stairs)
    {
        if (stairs.Count == 0) return ([], []);

        var allVerts = new List<float>();
        var allIndices = new List<ushort>();
        int vertexOffset = 0;

        foreach (var stair in stairs)
        {
            var (verts, idxs) = GenerateStairGeometry(stair);
            if (verts.Length == 0) continue;

            allVerts.AddRange(verts);
            foreach (var idx in idxs)
                allIndices.Add((ushort)(idx + vertexOffset));

            vertexOffset += verts.Length / 7;
        }

        return (allVerts.ToArray(), allIndices.ToArray());
    }

    public static float[] GenerateOutlineVertices(StairFlight stair, float expand = 0.03f)
    {
        // Generate outline from bounding box of entire stair
        var steps = stair.GenerateSteps();
        if (steps.Count == 0) return [];

        double minX = steps.Min(s => s[0]);
        double maxX = steps.Max(s => s[1]);
        double minY = steps.Min(s => s[2]);
        double maxY = steps.Max(s => s[3]);
        double z1 = steps[0][4];
        double z2 = steps[^1][5];

        // Create a bounding box outline
        float r = 1.0f, g = 0.9f, b = 0.0f, a = 1.0f;
        return
        [
            (float)minX, (float)minY, (float)z1, r, g, b, a,
            (float)maxX, (float)minY, (float)z1, r, g, b, a,
            (float)maxX, (float)maxY, (float)z1, r, g, b, a,
            (float)minX, (float)maxY, (float)z1, r, g, b, a,
            (float)minX, (float)minY, (float)z2, r, g, b, a,
            (float)maxX, (float)minY, (float)z2, r, g, b, a,
            (float)maxX, (float)maxY, (float)z2, r, g, b, a,
            (float)minX, (float)maxY, (float)z2, r, g, b, a,
        ];
    }
}
