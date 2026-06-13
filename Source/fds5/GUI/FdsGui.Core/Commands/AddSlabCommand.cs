using FdsGui.Core.Models;

namespace FdsGui.Core.Commands;

public class AddSlabCommand : ICommand
{
    private readonly Floor _floor;
    private readonly Slab _slab;

    public AddSlabCommand(Floor floor, Slab slab)
    {
        _floor = floor;
        _slab = slab;
    }

    public Slab Slab => _slab;

    public void Execute() => _floor.Slabs.Add(_slab);
    public void Undo() => _floor.Slabs.Remove(_slab);
}
