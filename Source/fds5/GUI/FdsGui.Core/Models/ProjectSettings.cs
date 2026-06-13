namespace FdsGui.Core.Models;

public class ProjectSettings
{
    public double MeshCellSize { get; set; } = 0.1;
    public double SimulationTime { get; set; } = 600.0;
    public double OutputInterval { get; set; } = 5.0;
    public double AmbientTemperature { get; set; } = 20.0;
    public string DefaultMaterial { get; set; } = "CONCRETE";
    public string FdsExecutablePath { get; set; } = "";
    public string SmokeViewPath { get; set; } = "";
    public string MpiExecPath { get; set; } = "";
    public int MpiProcesses { get; set; } = 1;
}
