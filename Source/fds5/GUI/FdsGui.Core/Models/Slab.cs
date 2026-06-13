namespace FdsGui.Core.Models;

public class Slab
{
    public string Name { get; set; } = "";
    public Polygon2D Outline { get; set; } = new();
    public double Thickness { get; set; } = 0.22;
    public double BottomZ { get; set; } = 0.0;
}
