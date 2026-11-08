using PulseOffice.Foundation.Modules;

namespace PulseOffice.Document.Embedding;

public enum DataValueKind { Text, Number, Date, Boolean }

public sealed record DataQuerySource
{
    public required string ConnectionId { get; init; }
    public required string Sql { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public int MaxRows { get; init; } = 5_000;
}

public sealed record DataColumn(string Name, DataValueKind Kind)
{
    public double WidthWeight { get; init; } = 1;
}

public sealed record DataSnapshot
{
    public required IReadOnlyList<DataColumn> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; }

    public DateTimeOffset TakenAt { get; init; } = DateTimeOffset.Now;
    public bool Truncated { get; init; }

    public static DataSnapshot Empty { get; } = new() { Columns = [], Rows = [] };
}

public sealed record TableMerge(int Row, int Column, int RowSpan, int ColumnSpan)
{
    public bool Covers(int row, int column) =>
        row >= Row && row < Row + RowSpan && column >= Column && column < Column + ColumnSpan
        && !(row == Row && column == Column);

    public bool IsOrigin(int row, int column) => row == Row && column == Column;
}

public enum CellVerticalAlign { Top, Center, Bottom }

public sealed record TableBorderLine(string ColorHex, double WidthPoints);

public enum CellHorizontalAlign { Left, Center, Right }

public sealed record TableCellLook
{
    public string? FillHex { get; init; }

    public TableBorderLine? Left { get; init; }

    public TableBorderLine? Right { get; init; }

    public TableBorderLine? Top { get; init; }

    public TableBorderLine? Bottom { get; init; }

    public string? TextHex { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public CellHorizontalAlign? Horizontal { get; init; }

    public static TableCellLook Plain { get; } = new();
}

public sealed record TableCellFormat(int Row, int Column, CellVerticalAlign VerticalAlign)
{
    public const int HeaderRow = -1;

    public TableCellLook? Look { get; init; }
}

public sealed record DataTableStyle
{
    public bool HeaderRow { get; init; } = true;
    public bool BandedRows { get; init; } = true;
    public bool Grid { get; init; } = true;
    public double FontSize { get; init; } = 11;
    public string? AccentHex { get; init; }

    public bool OwnLook { get; init; }
}

public sealed record TableLayout
{
    public IReadOnlyList<double> ColumnFractions { get; init; } = [];

    public IReadOnlyList<double> RowHeights { get; init; } = [];

    public static TableLayout Automatic { get; } = new();

    public double? RowHeight(int row) =>
        row >= 0 && row < RowHeights.Count && RowHeights[row] > 0 ? RowHeights[row] : null;
}

public sealed record DataTableObject : EmbeddedObject
{
    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Table;

    public required DataQuerySource Query { get; init; }

    public DataSnapshot Snapshot { get; init; } = DataSnapshot.Empty;

    public DataTableStyle Style { get; init; } = new();

    public IReadOnlyList<TableMerge> Merges { get; init; } = [];

    public IReadOnlyList<TableCellFormat> CellFormats { get; init; } = [];

    public TableLayout Layout { get; init; } = TableLayout.Automatic;

    public string? LastError { get; init; }

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];
}
