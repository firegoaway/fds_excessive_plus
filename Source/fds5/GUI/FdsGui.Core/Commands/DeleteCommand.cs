using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class DeleteCommand : ICommand
{
    private readonly Floor _floor;
    private readonly List<Wall> _walls;
    private readonly List<int> _indices;

    public DeleteCommand(Floor floor, List<Wall> walls)
    {
        _floor = floor;
        _walls = walls;
        _indices = walls.Select(w => _floor.Walls.IndexOf(w)).ToList();
    }

    public void Execute()
    {
        foreach (var wall in _walls)
            _floor.Walls.Remove(wall);
    }

    public void Undo()
    {
        for (int i = 0; i < _walls.Count; i++)
        {
            int idx = Math.Min(_indices[i], _floor.Walls.Count);
            _floor.Walls.Insert(idx, _walls[i]);
        }
    }
}
