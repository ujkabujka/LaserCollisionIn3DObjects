namespace BaseFramework.Core.UndoRedo;

public sealed class CompositeUndoableAction : IUndoableAction
{
    private readonly IReadOnlyList<IUndoableAction> _actions;

    public CompositeUndoableAction(IReadOnlyList<IUndoableAction> actions)
    {
        _actions = actions;
    }

    public void Undo()
    {
        for (var i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo();
        }
    }

    public void Redo()
    {
        foreach (var action in _actions)
        {
            action.Redo();
        }
    }
}
