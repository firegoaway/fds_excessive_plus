using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class AddStairCommand : ICommand
{
    private readonly Floor _floor;
    private readonly StairFlight _stair;

    public AddStairCommand(Floor floor, StairFlight stair)
    {
        _floor = floor;
        _stair = stair;
    }

    public StairFlight Stair => _stair;

    public void Execute() => _floor.Stairs.Add(_stair);
    public void Undo() => _floor.Stairs.Remove(_stair);
}
