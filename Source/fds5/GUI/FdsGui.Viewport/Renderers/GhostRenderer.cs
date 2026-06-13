using FdsGui.Core.Models;

namespace FdsGui.Viewport.Renderers;

public class GhostRenderer
{
    public Wall? GhostWall { get; set; }
    public Slab? GhostSlab { get; set; }
    public StairFlight? GhostStair { get; set; }

    public bool HasGhost => GhostWall != null || GhostSlab != null || GhostStair != null;

    public (float[]? vertices, ushort[]? indices) GenerateGhostGeometry()
    {
        if (GhostWall != null)
            return (WallRenderer.GenerateWallVertices(GhostWall, 0.4f), WallRenderer.GetBoxIndices());
        if (GhostSlab != null)
            return (SlabRenderer.GenerateSlabVertices(GhostSlab, 0.4f), SlabRenderer.GetBoxIndices());
        if (GhostStair != null)
        {
            var (v, i) = StairRenderer.GenerateStairGeometry(GhostStair, 0.4f);
            return (v.Length > 0 ? v : null, i.Length > 0 ? i : null);
        }
        return (null, null);
    }

    // Legacy methods for backward compat
    public float[]? GenerateGhostVertices()
    {
        var (v, _) = GenerateGhostGeometry();
        return v;
    }

    public ushort[] GetGhostIndices()
    {
        var (_, i) = GenerateGhostGeometry();
        return i ?? WallRenderer.GetBoxIndices();
    }

    public void Clear()
    {
        GhostWall = null;
        GhostSlab = null;
        GhostStair = null;
    }
}
