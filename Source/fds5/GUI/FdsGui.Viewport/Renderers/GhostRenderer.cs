using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

/// <summary>
/// Renders semi-transparent preview geometry for walls being drawn or transformed.
/// </summary>
public class GhostRenderer
{
    public Wall? GhostWall { get; set; }

    public bool HasGhost => GhostWall != null;

    public float[]? GenerateGhostVertices()
    {
        if (GhostWall == null) return null;
        return WallRenderer.GenerateWallVertices(GhostWall, alpha: 0.4f);
    }

    public ushort[] GetGhostIndices() => WallRenderer.GetBoxIndices();

    public void Clear() => GhostWall = null;
}
