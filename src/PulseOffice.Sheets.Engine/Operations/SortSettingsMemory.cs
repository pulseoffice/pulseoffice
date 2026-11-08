using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface ISortSettingsMemory
{
    void Remember(Workbook workbook, int sheetIndex, CellRange range, IReadOnlyList<SortKey> keys, UndoStack? history, string label,
        bool hasHeader = false, SortOrientation orientation = SortOrientation.TopToBottom);

    SortSettings? Recall(Workbook workbook, int sheetIndex, CellRange range);
}

public sealed class SortSettingsMemory : ISortSettingsMemory
{
    public void Remember(Workbook workbook, int sheetIndex, CellRange range, IReadOnlyList<SortKey> keys, UndoStack? history, string label,
        bool hasHeader = false, SortOrientation orientation = SortOrientation.TopToBottom)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(keys);
        if (sheetIndex < 0 || sheetIndex >= workbook.Sheets.Count || keys.Count == 0) return;

        var settings = new SortSettings
        {
            Fields = [.. keys.Select(key => new SortField(key.ColumnOffset, key.Ascending) { CustomOrder = key.CustomOrder })],
            CaseSensitive = keys.Any(key => key.CaseSensitive),
            Orientation = orientation,
        };
        var sheet = workbook.Sheets[sheetIndex];

        if (!settings.LeftToRight && sheet.AutoFilter.Range is { } filtered && SameBlock(filtered, range))
        {
            var before = sheet.AutoFilter.Save();
            sheet.AutoFilter.Remember(Shifted(settings, range, filtered));
            var after = sheet.AutoFilter.Save();
            history?.Push(new DelegateAction(label, redo: () => sheet.AutoFilter.Restore(after), undo: () => sheet.AutoFilter.Restore(before)));
            return;
        }

        if (!settings.LeftToRight && Owner(workbook, sheetIndex, range) is { } owner)
        {
            var kept = owner with { Sort = Shifted(settings, range, owner.Range) };
            workbook.DatabaseRanges.Define(kept);
            history?.Push(new DelegateAction(label, redo: () => workbook.DatabaseRanges.Define(kept), undo: () => workbook.DatabaseRanges.Define(owner)));
            return;
        }

        var previous = sheet.RangeSort;
        var remembered = new SheetRangeSort(range, hasHeader, settings);
        sheet.RangeSort = remembered;
        history?.Push(new DelegateAction(label, redo: () => sheet.RangeSort = remembered, undo: () => sheet.RangeSort = previous));
    }

    public SortSettings? Recall(Workbook workbook, int sheetIndex, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (sheetIndex < 0 || sheetIndex >= workbook.Sheets.Count) return null;
        var sheet = workbook.Sheets[sheetIndex];
        if (sheet.AutoFilter.Range is { } filtered && SameBlock(filtered, range) && sheet.AutoFilter.Sort is { } automatic)
            return Within(automatic, filtered, range);
        if (Owner(workbook, sheetIndex, range) is { Sort: { } owned } owner)
            return Within(owned, owner.Range, range);
        return sheet.RangeSort is { } plain && (plain.Range == range || plain.Data == range || Unheaded(range, plain) == plain.Range)
            ? plain.Settings
            : null;
    }

    private DatabaseRange? Owner(Workbook workbook, int sheetIndex, CellRange range) =>
        workbook.DatabaseRanges.All.FirstOrDefault(database => database.SheetIndex == sheetIndex
            && (SameBlock(database.Range, range) || SameBlock(database.RangeWithoutTotals, range)));

    private CellRange? Unheaded(CellRange range, SheetRangeSort plain) => plain.Settings.LeftToRight
        ? range.ColumnCount > 1 ? new CellRange(new CellAddress(range.Start.Row, range.Start.Column + 1), range.End) : null
        : range.RowCount > 1 ? new CellRange(new CellAddress(range.Start.Row + 1, range.Start.Column), range.End) : null;

    private SortSettings? Within(SortSettings stored, CellRange owner, CellRange range) =>
        stored.Moved(field => owner.Start.Column + field - range.Start.Column is var offset && offset >= 0 && offset < range.ColumnCount ? offset : null);

    private bool SameBlock(CellRange owner, CellRange sorted) =>
        owner.Start.Column <= sorted.Start.Column && owner.End.Column >= sorted.End.Column
        && owner.Start.Row == sorted.Start.Row && owner.End.Row >= sorted.End.Row;

    private SortSettings? Shifted(SortSettings settings, CellRange sorted, CellRange owner) =>
        settings.Moved(field => field + sorted.Start.Column - owner.Start.Column);
}
