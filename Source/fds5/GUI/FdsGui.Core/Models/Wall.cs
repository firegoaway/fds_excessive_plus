namespace FdsGui.Core.Models;

public class Wall
{
    public string Name { get; set; } = "";
    public Point2D Start { get; set; } = new(0, 0);
    public Point2D End { get; set; } = new(1, 0);
    public double Thickness { get; set; } = 0.2;
    public double Height { get; set; } = 3.0;
    public double BottomZ { get; set; } = 0.0;
    public string Material { get; set; } = "CONCRETE";
    public int[] Rgb { get; set; } = [180, 180, 180];

    public double Length => Start.DistanceTo(End);

    public Point2D Direction => (End - Start).Normalized();

    public Point2D Normal => new(-Direction.Y, Direction.X);

    /// XB for FDS: x1, x2, y1, y2, z1, z2
    public double[] ToXb()
    {
        var n = Normal * (Thickness / 2.0);
        var c1 = Start + n;
        var c2 = End + n;
        var c3 = Start - n;
        var c4 = End - n;

        double minX = Math.Min(Math.Min(c1.X, c2.X), Math.Min(c3.X, c4.X));
        double maxX = Math.Max(Math.Max(c1.X, c2.X), Math.Max(c3.X, c4.X));
        double minY = Math.Min(Math.Min(c1.Y, c2.Y), Math.Min(c3.Y, c4.Y));
        double maxY = Math.Max(Math.Max(c1.Y, c2.Y), Math.Max(c3.Y, c4.Y));

        return [minX, maxX, minY, maxY, BottomZ, BottomZ + Height];
    }
}
