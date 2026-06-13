using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

public class DoorRenderer
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

    /// Generate 8 vertices for a door box placed on a wall.
    public static float[] GenerateDoorVertices(Door door, Wall wall, float alpha = 1.0f)
    {
        var dir = wall.Direction;
        var normal = wall.Normal;
        var halfW = dir * (door.Width / 2.0);
        var halfT = normal * (wall.Thickness / 2.0);

        var center = door.Position;
        var dStart = center - halfW;
        var dEnd = center + halfW;

        double z1 = door.BottomZ;
        double z2 = door.BottomZ + door.Height;

        // 4 bottom corners
        var b0 = dStart - halfT;
        var b1 = dStart + halfT;
        var b2 = dEnd + halfT;
        var b3 = dEnd - halfT;

        float r = 139f / 255f;
        float g = 90f / 255f;
        float b = 43f / 255f;
        float a = alpha;

        return
        [
            (float)b0.X, (float)b0.Y, (float)z1, r, g, b, a,
            (float)b1.X, (float)b1.Y, (float)z1, r, g, b, a,
            (float)b2.X, (float)b2.Y, (float)z1, r, g, b, a,
            (float)b3.X, (float)b3.Y, (float)z1, r, g, b, a,
            (float)b0.X, (float)b0.Y, (float)z2, r, g, b, a,
            (float)b1.X, (float)b1.Y, (float)z2, r, g, b, a,
            (float)b2.X, (float)b2.Y, (float)z2, r, g, b, a,
            (float)b3.X, (float)b3.Y, (float)z2, r, g, b, a,
        ];
    }

    public static ushort[] GetBoxIndices() => WallRenderer.GetBoxIndices();

    /// Generate batched geometry for all doors (each door needs its parent wall).
    public static (float[] vertices, ushort[] indices) GenerateBatchedGeometry(
        IList<Door> doors, Func<string, Wall?> findWall)
    {
        var validDoors = doors.Where(d => findWall(d.ParentWallId) != null).ToList();
        int count = validDoors.Count;
        if (count == 0) return ([], []);

        var vertices = new float[count * 8 * 7];
        var indices = new ushort[count * 36];
        var baseIndices = GetBoxIndices();

        for (int i = 0; i < count; i++)
        {
            var wall = findWall(validDoors[i].ParentWallId)!;
            var doorVerts = GenerateDoorVertices(validDoors[i], wall);
            Array.Copy(doorVerts, 0, vertices, i * 56, 56);

            ushort offset = (ushort)(i * 8);
            for (int j = 0; j < 36; j++)
                indices[i * 36 + j] = (ushort)(baseIndices[j] + offset);
        }

        return (vertices, indices);
    }

    /// Generate outline vertices for selection.
    public static float[] GenerateOutlineVertices(Door door, Wall wall, float expand = 0.03f)
    {
        var verts = GenerateDoorVertices(door, wall);
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
