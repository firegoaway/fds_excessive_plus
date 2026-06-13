namespace FdsGui.Core.Models;

public class Room
{
    public string Name { get; set; } = "";
    public Polygon2D Outline { get; set; } = new();
    public double BottomZ { get; set; } = 0.0;
    public double TopZ { get; set; } = 3.0;
    public RoomCreationMode Mode { get; set; } = RoomCreationMode.Manual;

    public double[] ToXb() => [
        Outline.MinX, Outline.MaxX,
        Outline.MinY, Outline.MaxY,
        BottomZ, TopZ
    ];
}
