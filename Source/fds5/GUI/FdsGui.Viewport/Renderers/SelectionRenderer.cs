using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

/// <summary>
/// Renders selection outlines and handles for selected walls.
/// </summary>
public class SelectionRenderer
{
    public static float[]? GenerateOutlineVertices(Wall wall)
    {
        return WallRenderer.GenerateOutlineVertices(wall, expand: 0.03f);
    }

    public static ushort[] GetOutlineEdgeIndices() => WallRenderer.GetBoxEdgeIndices();

    /// Generate handle vertices (small squares at wall endpoints)
    public static float[] GenerateHandleVertices(Wall wall, float handleSize = 0.1f)
    {
        float hs = handleSize / 2f;
        float z = (float)(wall.BottomZ + wall.Height / 2);

        float hr = 0.0f, hg = 0.7f, hb = 1.0f, ha = 1.0f;

        float sx = (float)wall.Start.X, sy = (float)wall.Start.Y;
        float ex = (float)wall.End.X, ey = (float)wall.End.Y;

        return
        [
            // Start handle (4 vertices)
            sx - hs, sy - hs, z, hr, hg, hb, ha,
            sx + hs, sy - hs, z, hr, hg, hb, ha,
            sx + hs, sy + hs, z, hr, hg, hb, ha,
            sx - hs, sy + hs, z, hr, hg, hb, ha,
            // End handle (4 vertices)
            ex - hs, ey - hs, z, hr, hg, hb, ha,
            ex + hs, ey - hs, z, hr, hg, hb, ha,
            ex + hs, ey + hs, z, hr, hg, hb, ha,
            ex - hs, ey + hs, z, hr, hg, hb, ha,
        ];
    }

    /// Indices for two filled quads (handles)
    public static ushort[] GetHandleIndices() =>
    [
        0, 1, 2,  0, 2, 3,  // Start handle
        4, 5, 6,  4, 6, 7,  // End handle
    ];
}
