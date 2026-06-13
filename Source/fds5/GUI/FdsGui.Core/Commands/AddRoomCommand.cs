using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class AddRoomCommand : ICommand
{
    private readonly Floor _floor;
    private readonly Room _room;

    public AddRoomCommand(Floor floor, Room room)
    {
        _floor = floor;
        _room = room;
    }

    public Room Room => _room;

    public void Execute() => _floor.Rooms.Add(_room);
    public void Undo() => _floor.Rooms.Remove(_room);
}
