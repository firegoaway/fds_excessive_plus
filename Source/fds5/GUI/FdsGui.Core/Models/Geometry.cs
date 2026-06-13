namespace FdsGui.Core.Models;

public record Point2D(double X, double Y)
{
    public double DistanceTo(Point2D other) =>
        Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));

    public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2D operator *(Point2D p, double s) => new(p.X * s, p.Y * s);

    public Point2D Normalized()
    {
        var len = Math.Sqrt(X * X + Y * Y);
        return len < 1e-12 ? new Point2D(0, 0) : new Point2D(X / len, Y / len);
    }
}

public class Polygon2D
{
    public List<Point2D> Points { get; set; } = new();

    public double MinX => Points.Count > 0 ? Points.Min(p => p.X) : 0;
    public double MaxX => Points.Count > 0 ? Points.Max(p => p.X) : 0;
    public double MinY => Points.Count > 0 ? Points.Min(p => p.Y) : 0;
    public double MaxY => Points.Count > 0 ? Points.Max(p => p.Y) : 0;

    public bool ContainsPoint(Point2D point)
    {
        bool inside = false;
        int n = Points.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = Points[i];
            var pj = Points[j];
            if ((pi.Y > point.Y) != (pj.Y > point.Y) &&
                point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
