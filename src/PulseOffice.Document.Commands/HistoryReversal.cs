namespace PulseOffice.Document.Commands;

public interface IHistoryReversal
{
    void Revert(UndoStack history, object? statePoint);
}

public sealed class UndoToStatePoint : IHistoryReversal
{
    public void Revert(UndoStack history, object? statePoint)
    {
        ArgumentNullException.ThrowIfNull(history);
        while (!ReferenceEquals(history.StatePoint, statePoint) && history.CanUndo) history.Undo();
    }
}

public sealed class RedoToStatePoint : IHistoryReversal
{
    public void Revert(UndoStack history, object? statePoint)
    {
        ArgumentNullException.ThrowIfNull(history);
        while (!ReferenceEquals(history.StatePoint, statePoint) && history.CanRedo) history.Redo();
    }
}

public interface IHistoryReversals
{
    IHistoryReversal For(string commandId);
}

public sealed class HistoryReversals(UndoToStatePoint undo, RedoToStatePoint redo) : IHistoryReversals
{
    public IHistoryReversal For(string commandId) => commandId == "shell.edit.undo" ? redo : undo;
}
