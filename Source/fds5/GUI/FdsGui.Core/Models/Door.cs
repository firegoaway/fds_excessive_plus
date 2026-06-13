namespace FdsGui.Core.Models;

public class Door
{
    public string Name { get; set; } = "";
    public Point2D Position { get; set; } = new(0, 0);
    public double Width { get; set; } = 0.9;
    public double Height { get; set; } = 2.1;
    public double BottomZ { get; set; } = 0.0;
    public string ParentWallId { get; set; } = "";
    public double Rotation { get; set; } = 0.0;
}
