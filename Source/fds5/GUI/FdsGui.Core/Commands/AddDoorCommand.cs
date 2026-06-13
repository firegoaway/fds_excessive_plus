using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class AddDoorCommand : ICommand
{
    private readonly Floor _floor;
    private readonly Door _door;

    public AddDoorCommand(Floor floor, Door door)
    {
        _floor = floor;
        _door = door;
    }

    public Door Door => _door;

    public void Execute() => _floor.Doors.Add(_door);
    public void Undo() => _floor.Doors.Remove(_door);
}
