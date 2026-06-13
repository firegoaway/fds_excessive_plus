using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class AddFloorCommand : ICommand
{
    private readonly Building _building;
    private readonly Floor _floor;
    private int _insertIndex;

    public AddFloorCommand(Building building, Floor floor, int insertIndex = -1)
    {
        _building = building;
        _floor = floor;
        _insertIndex = insertIndex < 0 ? building.Floors.Count : insertIndex;
    }

    public Floor Floor => _floor;
    public int InsertIndex => _insertIndex;

    public void Execute()
    {
        _insertIndex = Math.Min(_insertIndex, _building.Floors.Count);
        _building.Floors.Insert(_insertIndex, _floor);
    }

    public void Undo()
    {
        _building.Floors.Remove(_floor);
    }
}
