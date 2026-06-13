using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class DeleteCommand : ICommand
{
    private readonly Floor _floor;
    private readonly List<Wall> _walls;
    private readonly List<Door> _doors;
    private readonly List<Slab> _slabs;
    private readonly List<StairFlight> _stairs;
    private readonly List<int> _wallIndices;
    private readonly List<int> _doorIndices;
    private readonly List<int> _slabIndices;
    private readonly List<int> _stairIndices;

    public DeleteCommand(Floor floor, List<Wall> walls, List<Door>? doors = null,
        List<Slab>? slabs = null, List<StairFlight>? stairs = null)
    {
        _floor = floor;
        _walls = walls;
        _doors = doors ?? new();
        _slabs = slabs ?? new();
        _stairs = stairs ?? new();
        _wallIndices = walls.Select(w => _floor.Walls.IndexOf(w)).ToList();
        _doorIndices = _doors.Select(d => _floor.Doors.IndexOf(d)).ToList();
        _slabIndices = _slabs.Select(s => _floor.Slabs.IndexOf(s)).ToList();
        _stairIndices = _stairs.Select(s => _floor.Stairs.IndexOf(s)).ToList();
    }

    public void Execute()
    {
        foreach (var wall in _walls) _floor.Walls.Remove(wall);
        foreach (var door in _doors) _floor.Doors.Remove(door);
        foreach (var slab in _slabs) _floor.Slabs.Remove(slab);
        foreach (var stair in _stairs) _floor.Stairs.Remove(stair);
    }

    public void Undo()
    {
        for (int i = 0; i < _walls.Count; i++)
        {
            int idx = Math.Min(_wallIndices[i], _floor.Walls.Count);
            _floor.Walls.Insert(idx, _walls[i]);
        }
        for (int i = 0; i < _doors.Count; i++)
        {
            int idx = Math.Min(_doorIndices[i], _floor.Doors.Count);
            _floor.Doors.Insert(idx, _doors[i]);
        }
        for (int i = 0; i < _slabs.Count; i++)
        {
            int idx = Math.Min(_slabIndices[i], _floor.Slabs.Count);
            _floor.Slabs.Insert(idx, _slabs[i]);
        }
        for (int i = 0; i < _stairs.Count; i++)
        {
            int idx = Math.Min(_stairIndices[i], _floor.Stairs.Count);
            _floor.Stairs.Insert(idx, _stairs[i]);
        }
    }
}
