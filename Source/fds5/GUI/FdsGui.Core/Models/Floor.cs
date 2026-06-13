namespace FdsGui.Core.Models;

public class Floor
{
    public string Name { get; set; } = "Этаж 1";
    public double Height { get; set; } = 3.0;
    public double Level { get; set; } = 0.0;
    public List<Wall> Walls { get; set; } = new();
    public List<Door> Doors { get; set; } = new();
    public List<Slab> Slabs { get; set; } = new();
    public List<StairFlight> Stairs { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<Background> Backgrounds { get; set; } = new();
}
