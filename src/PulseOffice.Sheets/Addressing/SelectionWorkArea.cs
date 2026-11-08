namespace PulseOffice.Sheets.Addressing;

public readonly record struct IndexSpan(int First, int Last)
{
    public int Length => Last - First + 1;
}

public sealed record SelectionWork(
    IReadOnlyList<CellRange> CellAreas,
    IReadOnlyList<IndexSpan> Columns,
    IReadOnlyList<IndexSpan> Rows)
{
    public bool SetsDefaults => Columns.Count > 0 || Rows.Count > 0;

    public long CellCount => CellAreas.Sum(area => area.CellCount);

    public IEnumerable<CellAddress> Cells => CellAreas.SelectMany(area => area.Cells());
}

public interface ISelectionWorkArea
{
    CellRange? Bound(CellRange selection, CellRange? used);

    SelectionWork Plan(CellRange selection, CellRange? used);
}

public sealed class SelectionWorkArea : ISelectionWorkArea
{
    public CellRange? Bound(CellRange selection, CellRange? used)
    {
        var wholeColumns = WholeColumns(selection);
        var wholeRows = WholeRows(selection);

        if (!wholeColumns && !wholeRows) return selection;
        if (used is not { } area) return null;

        var top = Math.Max(selection.Start.Row, area.Start.Row);
        var bottom = Math.Min(selection.End.Row, area.End.Row);
        var left = Math.Max(selection.Start.Column, area.Start.Column);
        var right = Math.Min(selection.End.Column, area.End.Column);

        return top > bottom || left > right
            ? null
            : new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
    }

    public SelectionWork Plan(CellRange selection, CellRange? used)
    {
        var areas = new List<CellRange>();
        if (Bound(selection, used) is { } area) areas.Add(area);

        IReadOnlyList<IndexSpan> columns = WholeColumns(selection) ? [new IndexSpan(selection.Start.Column, selection.End.Column)] : [];
        IReadOnlyList<IndexSpan> rows = WholeRows(selection) ? [new IndexSpan(selection.Start.Row, selection.End.Row)] : [];
        return new SelectionWork(areas, columns, rows);
    }

    private bool WholeColumns(CellRange selection) =>
        selection.Start.Row == 0 && selection.End.Row >= CellAddress.MaxRows - 1;

    private bool WholeRows(CellRange selection) =>
        selection.Start.Column == 0 && selection.End.Column >= CellAddress.MaxColumns - 1;
}
