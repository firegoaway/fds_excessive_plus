namespace FdsGui.Core.Models;

public class Background
{
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public double RealWidth { get; set; } = 10.0;
    public double RealHeight { get; set; } = 8.0;
    public double Rotation { get; set; } = 0.0;
    public double CorrectionAngle { get; set; } = 0.0;
    public double Opacity { get; set; } = 0.5;
    public bool Visible { get; set; } = true;
    public Point2D Origin { get; set; } = new(0, 0);
}
