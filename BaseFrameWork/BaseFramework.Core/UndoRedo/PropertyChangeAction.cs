namespace BaseFramework.Core.UndoRedo;

public sealed record PropertyChangeAction(ObservableObject Target, string PropertyName, object? OldValue, object? NewValue) : IUndoableAction
{
    public void Undo() => Target.ApplyExternalValue(PropertyName, OldValue);

    public void Redo() => Target.ApplyExternalValue(PropertyName, NewValue);
}
