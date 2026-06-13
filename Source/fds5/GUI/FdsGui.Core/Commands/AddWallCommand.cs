using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class AddWallCommand : ICommand
{
    private readonly Floor _floor;
    private readonly Wall _wall;

    public AddWallCommand(Floor floor, Wall wall)
    {
        _floor = floor;
        _wall = wall;
    }

    public Wall Wall => _wall;

    public void Execute() => _floor.Walls.Add(_wall);
    public void Undo() => _floor.Walls.Remove(_wall);
}
