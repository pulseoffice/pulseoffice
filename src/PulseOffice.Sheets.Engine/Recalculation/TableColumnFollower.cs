using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed class TableColumnFollower(ITableReferenceResolution resolution)
{
    private readonly Dictionary<string, IReadOnlyList<string>> _known = new(StringComparer.OrdinalIgnoreCase);

    public void Reset(Workbook workbook)
    {
        _known.Clear();
        foreach (var table in workbook.DatabaseRanges.All)
            if (Columns(workbook, table) is { } columns) _known[table.Name] = columns;
    }

    public IReadOnlyList<TableRename> Follow(Workbook workbook, IReadOnlyCollection<SheetCell> changed)
    {
        if (workbook.DatabaseRanges.Count == 0 || changed.Count == 0) return [];
        var renames = new List<TableRename>();
        foreach (var table in workbook.DatabaseRanges.All)
        {
            if (!table.HasHeader || !changed.Any(cell => cell.SheetIndex == table.SheetIndex && cell.Address.Row == table.Range.Start.Row
                && cell.Address.Column >= table.Range.Start.Column && cell.Address.Column <= table.Range.End.Column)) continue;
            if (Columns(workbook, table) is not { } current) continue;
            if (_known.TryGetValue(table.Name, out var previous) && previous.Count == current.Count)
                for (var index = 0; index < current.Count; index++)
                    if (!string.Equals(previous[index], current[index], StringComparison.Ordinal))
                        renames.Add(new TableRename(table.Name, null, previous[index], current[index]));
            _known[table.Name] = current;
        }
        return renames;
    }

    private IReadOnlyList<string>? Columns(Workbook workbook, DatabaseRange table) =>
        table.SheetIndex >= 0 && table.SheetIndex < workbook.Sheets.Count ? resolution.Columns(workbook.Sheets[table.SheetIndex], table) : null;
}
