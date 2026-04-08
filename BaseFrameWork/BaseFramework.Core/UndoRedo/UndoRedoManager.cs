namespace BaseFramework.Core.UndoRedo;

public sealed class UndoRedoManager
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();
    private List<IUndoableAction>? _currentTransaction;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool InTransaction => _currentTransaction is not null;

    public void BeginTransaction()
    {
        if (_currentTransaction is not null)
        {
            throw new InvalidOperationException("Nested transaction is not supported.");
        }

        _currentTransaction = new List<IUndoableAction>();
    }

    public void CommitTransaction()
    {
        if (_currentTransaction is null)
        {
            return;
        }

        if (_currentTransaction.Count > 0)
        {
            _undo.Push(new CompositeUndoableAction(_currentTransaction.ToArray()));
            _redo.Clear();
        }

        _currentTransaction = null;
    }

    public void RollbackTransaction()
    {
        if (_currentTransaction is null)
        {
            return;
        }

        for (var i = _currentTransaction.Count - 1; i >= 0; i--)
        {
            _currentTransaction[i].Undo();
        }

        _currentTransaction = null;
    }

    public void Push(IUndoableAction action)
    {
        if (_currentTransaction is not null)
        {
            _currentTransaction.Add(action);
            return;
        }

        _undo.Push(action);
        _redo.Clear();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var action = _undo.Pop();
        action.Undo();
        _redo.Push(action);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var action = _redo.Pop();
        action.Redo();
        _undo.Push(action);
    }
}
