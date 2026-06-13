namespace FdsGui.Core.Commands;

public class ChangePropertyCommand : ICommand
{
    private readonly Action _doAction;
    private readonly Action _undoAction;

    public ChangePropertyCommand(Action doAction, Action undoAction)
    {
        _doAction = doAction;
        _undoAction = undoAction;
    }

    public void Execute() => _doAction();
    public void Undo() => _undoAction();
}
