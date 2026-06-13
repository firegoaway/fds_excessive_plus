using OpenTK.Mathematics;

namespace FdsGui.Viewport;

/// <summary>
/// Renders a ground grid and XYZ axes using OpenGL.
/// </summary>
public class GridRenderer
{
    public float GridSize { get; set; } = 50.0f;
    public float GridStep { get; set; } = 0.5f;
    public float AxisLength { get; set; } = 5.0f;

    // Colors
    public Vector4 GridColor { get; set; } = new(0.4f, 0.4f, 0.4f, 0.5f);
    public Vector4 AxisXColor { get; set; } = new(1.0f, 0.0f, 0.0f, 1.0f);
    public Vector4 AxisYColor { get; set; } = new(0.0f, 0.8f, 0.0f, 1.0f);
    public Vector4 AxisZColor { get; set; } = new(0.0f, 0.4f, 1.0f, 1.0f);

    /// Generate grid line vertices (as line pairs for GL_LINES)
    public float[] GenerateGridVertices()
    {
        var verts = new List<float>();
        int halfCount = (int)(GridSize / GridStep);

        float r = GridColor.X, g = GridColor.Y, b = GridColor.Z, a = GridColor.W;

        for (int i = -halfCount; i <= halfCount; i++)
        {
            float pos = i * GridStep;

            // Lines along Y
            verts.AddRange([pos, -GridSize, 0, r, g, b, a]);
            verts.AddRange([pos, GridSize, 0, r, g, b, a]);

            // Lines along X
            verts.AddRange([-GridSize, pos, 0, r, g, b, a]);
            verts.AddRange([GridSize, pos, 0, r, g, b, a]);
        }

        return verts.ToArray();
    }

    public int GridVertexCount => GenerateGridVertices().Length / 7;

    /// Generate axis line vertices (X=red, Y=green, Z=blue)
    public float[] GenerateAxisVertices()
    {
        float len = AxisLength;
        return
        [
            // X axis (red)
            0, 0, 0, AxisXColor.X, AxisXColor.Y, AxisXColor.Z, AxisXColor.W,
            len, 0, 0, AxisXColor.X, AxisXColor.Y, AxisXColor.Z, AxisXColor.W,
            // Y axis (green)
            0, 0, 0, AxisYColor.X, AxisYColor.Y, AxisYColor.Z, AxisYColor.W,
            0, len, 0, AxisYColor.X, AxisYColor.Y, AxisYColor.Z, AxisYColor.W,
            // Z axis (blue)
            0, 0, 0, AxisZColor.X, AxisZColor.Y, AxisZColor.Z, AxisZColor.W,
            0, 0, len, AxisZColor.X, AxisZColor.Y, AxisZColor.Z, AxisZColor.W,
        ];
    }

    public const int AxisVertexCount = 6;
}
