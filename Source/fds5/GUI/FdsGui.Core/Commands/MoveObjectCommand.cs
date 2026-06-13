using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class MoveObjectCommand : ICommand
{
    private readonly Wall _wall;
    private readonly Point2D _oldStart;
    private readonly Point2D _oldEnd;
    private readonly Point2D _newStart;
    private readonly Point2D _newEnd;

    public MoveObjectCommand(Wall wall, Point2D oldStart, Point2D oldEnd, Point2D newStart, Point2D newEnd)
    {
        _wall = wall;
        _oldStart = oldStart;
        _oldEnd = oldEnd;
        _newStart = newStart;
        _newEnd = newEnd;
    }

    public void Execute()
    {
        _wall.Start = _newStart;
        _wall.End = _newEnd;
    }

    public void Undo()
    {
        _wall.Start = _oldStart;
        _wall.End = _oldEnd;
    }
}
