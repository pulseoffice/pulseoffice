namespace PulseOffice.Document.Tables;

public readonly record struct TableCellRange(int FirstRow, int FirstColumn, int LastRow, int LastColumn)
{
    public int Top => Math.Min(FirstRow, LastRow);

    public int Bottom => Math.Max(FirstRow, LastRow);

    public int Left => Math.Min(FirstColumn, LastColumn);

    public int Right => Math.Max(FirstColumn, LastColumn);

    public int RowCount => Bottom - Top + 1;

    public int ColumnCount => Right - Left + 1;

    public bool IsSingleCell => RowCount == 1 && ColumnCount == 1;

    public bool Contains(int row, int column) =>
        row >= Top && row <= Bottom && column >= Left && column <= Right;

    public TableCellRange Normalized => new(Top, Left, Bottom, Right);

    public (int First, int Last)? DataRows(int rowCount) => Clamp(FirstRow, LastRow, rowCount);

    public (int First, int Last)? Columns(int columnCount) => Clamp(FirstColumn, LastColumn, columnCount);

    public TableCellRange? ClampedTo(int rowCount, int columnCount) =>
        DataRows(rowCount) is { } rows && Columns(columnCount) is { } columns
            ? new TableCellRange(rows.First, columns.First, rows.Last, columns.Last)
            : null;

    private (int First, int Last)? Clamp(int a, int b, int count)
    {
        var first = Math.Max(Math.Min(a, b), 0);
        var last = Math.Min(Math.Max(a, b), count - 1);
        return last >= first ? (first, last) : null;
    }
}
