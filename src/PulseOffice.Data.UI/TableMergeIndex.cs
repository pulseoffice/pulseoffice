using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public readonly record struct TableCellRef(int Row, int Column)
{
    public bool IsHeader => Row == TableCellFormat.HeaderRow;
}

public sealed class TableMergeIndex
{
    private readonly Dictionary<TableCellRef, TableMerge> _origins = [];
    private readonly Dictionary<TableCellRef, TableMerge> _owners = [];
    private readonly Dictionary<TableCellRef, CellVerticalAlign> _alignments = [];

    public TableMergeIndex(IEnumerable<TableMerge> merges, IEnumerable<TableCellFormat> formats, int rowCount, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(merges);
        ArgumentNullException.ThrowIfNull(formats);

        foreach (var merge in merges)
        {
            if (merge.Column < 0 || merge.Column >= columnCount) continue;
            if (merge.Row != TableCellFormat.HeaderRow && (merge.Row < 0 || merge.Row >= rowCount)) continue;

            var clamped = Clamp(merge, rowCount, columnCount);
            if (clamped.RowSpan == 1 && clamped.ColumnSpan == 1) continue;
            if (!_origins.TryAdd(new TableCellRef(clamped.Row, clamped.Column), clamped)) continue;

            for (var row = clamped.Row; row < clamped.Row + clamped.RowSpan; row++)
                for (var column = clamped.Column; column < clamped.Column + clamped.ColumnSpan; column++)
                    _owners.TryAdd(new TableCellRef(row, column), clamped);
        }

        foreach (var format in formats)
            _alignments[new TableCellRef(format.Row, format.Column)] = format.VerticalAlign;
    }

    public TableMerge? Origin(TableCellRef cell) => _origins.GetValueOrDefault(cell);

    public TableMerge? Owner(TableCellRef cell) => _owners.GetValueOrDefault(cell);

    public bool IsCovered(TableCellRef cell) =>
        _owners.TryGetValue(cell, out var owner) && !owner.IsOrigin(cell.Row, cell.Column);

    public CellVerticalAlign VerticalAlign(TableCellRef cell) =>
        _alignments.GetValueOrDefault(cell, CellVerticalAlign.Top);

    private static TableMerge Clamp(TableMerge merge, int rowCount, int columnCount) => merge with
    {
        RowSpan = merge.Row == TableCellFormat.HeaderRow ? 1 : Math.Clamp(merge.RowSpan, 1, rowCount - merge.Row),
        ColumnSpan = Math.Clamp(merge.ColumnSpan, 1, columnCount - merge.Column),
    };
}
