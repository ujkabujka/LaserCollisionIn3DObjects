namespace BaseFramework.Core.UndoRedo;

public interface IUndoableAction
{
    void Undo();
    void Redo();
}
