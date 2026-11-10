using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public interface ITableEditStep
{
    void Undo(TableEditorSheet sheet);

    void Redo(TableEditorSheet sheet);
}

public sealed record TableSheetMemento(
    IReadOnlyList<string> Headers,
    IReadOnlyList<string[]> Rows,
    IReadOnlyList<TableMerge> Merges,
    IReadOnlyList<TableCellFormat> CellFormats);

public sealed class CellTextStep(IReadOnlyList<(TableCellRef Cell, string Before, string After)> changes) : ITableEditStep
{
    public void Undo(TableEditorSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.Write([.. changes.Select(change => (change.Cell, change.Before))]);
    }

    public void Redo(TableEditorSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.Write([.. changes.Select(change => (change.Cell, change.After))]);
    }
}

public sealed class StructureStep(TableSheetMemento before, TableSheetMemento after) : ITableEditStep
{
    public void Undo(TableEditorSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.Restore(before);
    }

    public void Redo(TableEditorSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheet.Restore(after);
    }
}

public sealed class TableEditHistory
{
    private const int Limit = 64;

    private readonly List<ITableEditStep> _undo = [];
    private readonly List<ITableEditStep> _redo = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Push(ITableEditStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _undo.Add(step);
        if (_undo.Count > Limit) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public void Undo(TableEditorSheet sheet)
    {
        if (_undo.Count == 0) return;
        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        step.Undo(sheet);
        _redo.Add(step);
    }

    public void Redo(TableEditorSheet sheet)
    {
        if (_redo.Count == 0) return;
        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        step.Redo(sheet);
        _undo.Add(step);
    }
}
