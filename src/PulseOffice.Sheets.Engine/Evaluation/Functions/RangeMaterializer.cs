using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class ValueGrid
{
    private readonly CellValue[,] _cells;
    private readonly int _storedRows;
    private readonly int _storedColumns;

    public ValueGrid(int rows, int columns)
        : this(rows, columns, rows, columns)
    {
    }

    public ValueGrid(int rows, int columns, int storedRows, int storedColumns)
    {
        RowCount = rows;
        ColumnCount = columns;
        _storedRows = Math.Min(storedRows, rows);
        _storedColumns = Math.Min(storedColumns, columns);
        _cells = new CellValue[_storedRows, _storedColumns];
    }

    public int RowCount { get; }
    public int ColumnCount { get; }
    public bool IsEmpty => RowCount == 0 || ColumnCount == 0;

    public int StoredRowCount => _storedRows;

    public CellValue this[int row, int column]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)row, (uint)RowCount, nameof(row));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)column, (uint)ColumnCount, nameof(column));
            return row < _storedRows && column < _storedColumns ? _cells[row, column] : CellValue.Empty;
        }
        internal set => _cells[row, column] = value;
    }

    public IEnumerable<CellValue> Row(int row)
    {
        for (var c = 0; c < ColumnCount; c++) yield return this[row, c];
    }

    public IEnumerable<CellValue> Column(int column)
    {
        for (var r = 0; r < RowCount; r++) yield return this[r, column];
    }

    public IEnumerable<CellValue> InReadingOrder()
    {
        for (var r = 0; r < RowCount; r++)
            for (var c = 0; c < ColumnCount; c++)
                yield return this[r, c];
    }
}

public interface IRangeMaterializer
{
    ValueGrid Materialize(EvalValue value, IEvaluationContext context);
}

public sealed class RangeMaterializer : IRangeMaterializer
{
    public ValueGrid Materialize(EvalValue value, IEvaluationContext context)
    {
        switch (value)
        {
            case RangeValue range:
                {
                    var content = context.ContentWithin(range.SheetName, range.Range);
                    var grid = new ValueGrid(range.Range.RowCount, range.Range.ColumnCount,
                        content?.RowCount ?? 0, content?.ColumnCount ?? 0);
                    var startRow = range.Range.Start.Row;
                    var startColumn = range.Range.Start.Column;

                    for (var r = 0; r < grid.StoredRowCount && content is not null; r++)
                        for (var c = 0; c < content.Value.ColumnCount; c++)
                            grid[r, c] = context.GetCellValue(
                                range.SheetName,
                                new Addressing.CellAddress(startRow + r, startColumn + c));
                    return grid;
                }

            case ArrayValue array:
                {
                    var columns = array.Rows.Count == 0 ? 0 : array.Rows.Max(row => row.Count);
                    var grid = new ValueGrid(array.Rows.Count, columns);

                    for (var r = 0; r < array.Rows.Count; r++)
                        for (var c = 0; c < columns; c++)

                            grid[r, c] = c < array.Rows[r].Count
                                ? array.Rows[r][c]
                                : CellValue.FromError(CellError.NotAvailable);
                    return grid;
                }

            case ReferenceListValue list:
                {
                    var cells = list.Areas.SelectMany(area => Materialize(area, context).InReadingOrder()).ToList();
                    var grid = new ValueGrid(cells.Count, 1);
                    for (var r = 0; r < cells.Count; r++) grid[r, 0] = cells[r];
                    return grid;
                }

            case ScalarValue scalar:
                {
                    var grid = new ValueGrid(1, 1);
                    grid[0, 0] = scalar.Value;
                    return grid;
                }

            default:
                return new ValueGrid(0, 0);
        }
    }
}
