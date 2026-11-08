using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public enum PivotNodeKind { Item, Subtotal, GrandTotal }

public sealed record PivotNode(IReadOnlyList<string> Path, PivotNodeKind Kind)
{
    public string Label => Path.Count == 0 ? string.Empty : Path[^1];

    public int Depth => Math.Max(Path.Count - 1, 0);
}

public sealed record PivotColumn(PivotNode Node, PivotField Field, int FieldIndex);

public sealed class PivotTable
{
    private readonly CellValue[,] _cells;

    public PivotTable(
        IReadOnlyList<PivotNode> rows, IReadOnlyList<PivotColumn> columns, CellValue[,] cells)
    {
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        _cells = cells ?? throw new ArgumentNullException(nameof(cells));
    }

    public IReadOnlyList<PivotNode> Rows { get; }

    public IReadOnlyList<PivotColumn> Columns { get; }

    public CellValue Value(int row, int column) => _cells[row, column];

    public bool IsEmpty => Rows.Count == 0 || Columns.Count == 0;
}
