namespace PulseOffice.Document.Embedding;

public sealed class TableCellIndex
{
    private readonly Dictionary<(int Row, int Column), TableCellFormat> _formats = [];
    private readonly Dictionary<(int Row, int Column), TableMerge> _origins = [];
    private readonly Dictionary<(int Row, int Column), TableMerge> _covers = [];
    private readonly Dictionary<(int Row, int Column), TableCellText> _texts = [];

    public TableCellIndex(DataTableObject table)
    {
        foreach (var format in table.CellFormats) _formats.TryAdd((format.Row, format.Column), format);
        foreach (var text in table.CellTexts) _texts.TryAdd((text.Row, text.Column), text);
        var rows = table.Snapshot.Rows.Count;
        var columns = table.Snapshot.Columns.Count;
        foreach (var merge in table.Merges)
        {
            _origins.TryAdd((merge.Row, merge.Column), merge);
            var lastRow = (int)Math.Min((long)merge.Row + merge.RowSpan, rows);
            var lastColumn = (int)Math.Min((long)merge.Column + merge.ColumnSpan, columns);
            for (var row = Math.Max(merge.Row, TableCellFormat.HeaderRow); row < lastRow; row++)
                for (var column = Math.Max(merge.Column, 0); column < lastColumn; column++)
                    if (merge.Covers(row, column)) _covers.TryAdd((row, column), merge);
        }
    }

    public TableCellFormat? Format(int row, int column) => _formats.GetValueOrDefault((row, column));

    public TableCellText? Text(int row, int column, string? value) =>
        _texts.GetValueOrDefault((row, column)) is { } text && text.Matches(value) ? text : null;

    public TableMerge? Origin(int row, int column) => _origins.GetValueOrDefault((row, column));

    public TableMerge? Cover(int row, int column) => _covers.GetValueOrDefault((row, column));
}
