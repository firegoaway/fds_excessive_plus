namespace FdsGui.Core.Models;

public class StairFlight
{
    public string Name { get; set; } = "";
    public Point2D Start { get; set; } = new(0, 0);
    public Point2D End { get; set; } = new(3, 0);
    public double Height { get; set; } = 3.0;
    public double BottomZ { get; set; } = 0.0;
    public double Width { get; set; } = 1.2;
    public Direction Direction { get; set; } = Direction.Up;
    public double StepHeight { get; set; } = 0.15;

    public int StepCount => (int)Math.Ceiling(Height / StepHeight);

    /// Generates XB arrays for each step (as OBST in FDS)
    public List<double[]> GenerateSteps()
    {
        var steps = new List<double[]>();
        var dir = (End - Start).Normalized();
        var normal = new Point2D(-dir.Y, dir.X);
        double totalLength = Start.DistanceTo(End);
        double stepLength = totalLength / StepCount;
        double stepRise = Height / StepCount;

        for (int i = 0; i < StepCount; i++)
        {
            var p1 = Start + dir * (stepLength * i);
            var p2 = Start + dir * (stepLength * (i + 1));
            var halfW = normal * (Width / 2.0);

            double z1 = BottomZ + stepRise * i;
            double z2 = BottomZ + stepRise * (i + 1);

            double minX = Math.Min(p1.X - halfW.X, p2.X - halfW.X);
            double maxX = Math.Max(p1.X + halfW.X, p2.X + halfW.X);
            double minY = Math.Min(p1.Y - halfW.Y, p2.Y - halfW.Y);
            double maxY = Math.Max(p1.Y + halfW.Y, p2.Y + halfW.Y);

            steps.Add([minX, maxX, minY, maxY, z1, z2]);
        }

        return steps;
    }
}
