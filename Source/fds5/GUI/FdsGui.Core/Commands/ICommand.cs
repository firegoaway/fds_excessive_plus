namespace FdsGui.Core.Commands;

public interface ICommand
{
    void Execute();
    void Undo();
}
