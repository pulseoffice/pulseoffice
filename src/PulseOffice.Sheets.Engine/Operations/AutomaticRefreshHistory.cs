using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Operations;

public readonly record struct AutomaticRefreshKey(int SheetIndex, CellAddress Anchor);

public interface IAutomaticRefreshHistory
{
    bool Record(UndoStack history, CalculationEngine engine, Workbook workbook, AutomaticRefreshKey key, string description, Func<bool> refresh);
}

public sealed class AutomaticRefreshHistory(IWorkbookCheckpoints checkpoints) : IAutomaticRefreshHistory
{
    public bool Record(UndoStack history, CalculationEngine engine, Workbook workbook, AutomaticRefreshKey key, string description, Func<bool> refresh)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(refresh);

        var before = checkpoints.Capture(workbook);
        if (!refresh()) return false;

        var after = checkpoints.Capture(workbook);
        history.Push(new RefreshStep(description, before, after, checkpoint => checkpoints.Restore(engine, workbook, checkpoint)));
        return true;
    }

    private sealed class RefreshStep(
        string description,
        WorkbookCheckpoint before,
        WorkbookCheckpoint after,
        Action<WorkbookCheckpoint> restore) : IUndoableAction
    {
        public string Description { get; } = description;

        public void Redo() => restore(after);

        public void Undo() => restore(before);
    }
}
