namespace FdsGui.Core.Models;

public class Building
{
    public string Name { get; set; } = "Новое здание";
    public string FilePath { get; set; } = "";
    public ProjectSettings Settings { get; set; } = new();
    public List<Floor> Floors { get; set; } = new();
}
