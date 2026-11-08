using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Blocks;

public enum CellVerticalAlign
{
    Top,
    Middle,
    Bottom,
}

public enum CellTextDirection
{
    Horizontal,
    TopToBottom,
    BottomToTop,
}

public sealed record CellPadding(double Top, double Bottom, double Left, double Right);

public enum TableAlignment
{
    Left,
    Center,
    Right,
}

public enum CellValueKind
{
    Number,
    Percent,
    Currency,
    Date,
}

public sealed record CellNumberFormat(CellValueKind Kind, int Decimals = 2, bool Grouping = true)
{
    public string? Symbol { get; init; }

    public bool LongDate { get; init; }
}

public sealed class TableCell(BlockContainer content)
{
    public bool Protected { get; set; }

    public CellNumberFormat? NumberFormat { get; set; }

    public BlockContainer Content { get; } = content;

    public int ColumnSpan { get; set; } = 1;

    public int RowSpan { get; set; } = 1;

    public bool Covered { get; set; }

    public string? StyleName { get; set; }

    public uint? Background { get; set; }

    public CellVerticalAlign VerticalAlign { get; set; }

    public BoxBorders? Borders { get; set; }

    public CellTextDirection TextDirection { get; set; }

    public CellPadding? Padding { get; set; }

    public string? Formula { get; set; }

    public TableCell Clone() => new(Content.Clone())
    {
        Protected = Protected,
        NumberFormat = NumberFormat,
        Borders = Borders,
        Formula = Formula,
        ColumnSpan = ColumnSpan,
        RowSpan = RowSpan,
        Covered = Covered,
        StyleName = StyleName,
        Background = Background,
        VerticalAlign = VerticalAlign,
        TextDirection = TextDirection,
        Padding = Padding,
    };
}

public sealed class TableRow(IEnumerable<TableCell> cells)
{
    public List<TableCell> Cells { get; } = [.. cells];

    public Twip? Height { get; set; }

    public bool MinimumHeight { get; set; } = true;

    public bool AllowBreakAcrossPages { get; set; } = true;

    public string? ChangeId { get; set; }

    public TableRow Clone() => new(Cells.Select(cell => cell.Clone()))
    {
        Height = Height,
        MinimumHeight = MinimumHeight,
        AllowBreakAcrossPages = AllowBreakAcrossPages,
        ChangeId = ChangeId,
    };
}

public sealed class TableBlock(string id, IEnumerable<TableRow> rows, IEnumerable<Twip> columns) : Block(id)
{
    public List<TableRow> Rows { get; } = [.. rows];

    public List<Twip> Columns { get; } = [.. columns];

    public string? Name { get; set; }

    public string? StyleName { get; set; }

    public int HeaderRows { get; set; }

    public bool RepeatHeaderRows { get; set; } = true;

    public Styles.TableProperties Direct { get; set; } = Styles.TableProperties.None;

    public string? PageStyleName { get; set; }

    public bool PageBreakBefore { get; set; }

    public TableAlignment Alignment { get; set; }

    public Twip Indent { get; set; }

    public int ColumnCount => Columns.Count;

    public TableCell Cell(int row, int column) => Rows[row].Cells[column];

    public override T Accept<T>(IBlockVisitor<T> visitor) => visitor.VisitTable(this);

    public override Block Clone() => new TableBlock(Id, Rows.Select(row => row.Clone()), Columns)
    {
        Name = Name,
        StyleName = StyleName,
        HeaderRows = HeaderRows,
        RepeatHeaderRows = RepeatHeaderRows,
        Direct = Direct,
        PageStyleName = PageStyleName,
        PageBreakBefore = PageBreakBefore,
        Alignment = Alignment,
        Indent = Indent,
    };
}
