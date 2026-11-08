using System.Globalization;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Flow;

public abstract class FlowItem
{
    public abstract FlowKey Key { get; }

    public abstract T Accept<T>(IFlowItemVisitor<T> visitor);
}

public interface IFlowItemVisitor<out T>
{
    T VisitParagraph(ParagraphEntry paragraph);

    T VisitTable(TableItem table);

    T VisitSectionStart(SectionStartItem section);

    T VisitSectionEnd(SectionEndItem section);

    T VisitEndnotes(EndnotesItem endnotes);
}

public sealed record ColumnLayout(int Count, double Gap, bool Balanced, bool Separator)
{
    public static readonly ColumnLayout Single = new(1, 0, false, false);

    private const double NarrowestColumn = 1;

    public double Spacing(double total) => Count <= 1 || !double.IsFinite(Gap) ? 0 : Math.Clamp(Gap, 0, Math.Max(0, (total - NarrowestColumn * Count) / (Count - 1)));

    public double Width(double total) => Count <= 1 ? total : Math.Max(NarrowestColumn, (total - Spacing(total) * (Count - 1)) / Count);

    public double Left(double x, double total, int index) => x + index * (Width(total) + Spacing(total));
}

public sealed class SectionStartItem(SectionBlock section, ColumnLayout columns) : FlowItem
{
    public SectionBlock Section { get; } = section;

    public ColumnLayout Columns { get; } = columns;

    public override FlowKey Key => new("section-start:" + Section.Id, [Columns], string.Empty);

    public override T Accept<T>(IFlowItemVisitor<T> visitor) => visitor.VisitSectionStart(this);
}

public sealed class SectionEndItem(SectionBlock section) : FlowItem
{
    public SectionBlock Section { get; } = section;

    public override FlowKey Key => new("section-end:" + Section.Id, [], string.Empty);

    public override T Accept<T>(IFlowItemVisitor<T> visitor) => visitor.VisitSectionEnd(this);
}

public sealed class EndnotesItem(string? pageStyle) : FlowItem
{
    public string? PageStyle { get; } = pageStyle;

    public override FlowKey Key => new("endnotes", [PageStyle ?? string.Empty], string.Empty);

    public override T Accept<T>(IFlowItemVisitor<T> visitor) => visitor.VisitEndnotes(this);
}

public sealed record TableStyleValues(double Padding, double VerticalPadding, double BorderWidth, uint? BorderColor, uint? Background, uint? HeaderBackground, uint? BandBackground);

public sealed class TableCellItem(TableCell cell, int column, IReadOnlyList<FlowItem> items)
{
    public TableCell Cell { get; } = cell;

    public int Column { get; } = column;

    public int ColumnSpan => Math.Max(1, Cell.ColumnSpan);

    public int RowSpan => Math.Max(1, Cell.RowSpan);

    public IReadOnlyList<FlowItem> Items { get; } = items;

    public PulseOffice.Documents.Styles.CellAppearance? Appearance { get; init; }
}

public sealed class TableRowItem(TableRow row, IReadOnlyList<TableCellItem> cells)
{
    public TableRow Row { get; } = row;

    public IReadOnlyList<TableCellItem> Cells { get; } = cells;

    public double? Height => Row.Height is { } height ? Math.Max(0, height.ToPoints()) : null;

    public bool Exact => Row.Height is not null && !Row.MinimumHeight;
}

public sealed class TableItem(TableBlock table, IReadOnlyList<double> columns, IReadOnlyList<TableRowItem> rows, TableStyleValues style) : FlowItem
{
    public TableBlock Table { get; } = table;

    public IReadOnlyList<double> Columns { get; } = columns;

    public IReadOnlyList<TableRowItem> Rows { get; } = rows;

    public TableStyleValues Style { get; } = style;

    public int HeaderRows => Table.RepeatHeaderRows ? Math.Clamp(Table.HeaderRows, 0, Rows.Count) : 0;

    public StoryKind Story { get; init; } = StoryKind.Body;

    public override FlowKey Key => field ??= Compose();

    private FlowKey Compose()
    {
        var structure = string.Join(';', Columns.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))
            + "|" + string.Join(';', Rows.Select(row => $"{row.Height}:{row.Exact}:{row.Row.AllowBreakAcrossPages}:" + string.Join(',', row.Cells.Select(cell => $"{cell.Column}/{cell.ColumnSpan}/{cell.RowSpan}/{cell.Cell.Background}/{cell.Cell.VerticalAlign}/{cell.Cell.TextDirection}/{cell.Cell.Padding}/{cell.Appearance}"))))
            + "|" + Table.HeaderRows + Table.RepeatHeaderRows + "|" + Table.PageStyleName + Table.PageBreakBefore + "|" + Table.Alignment + Table.Indent.Value;
        var parts = new List<object> { structure, Style };
        foreach (var cell in Rows.SelectMany(row => row.Cells))
        {
            foreach (var key in cell.Items.Select(item => item.Key))
            {
                parts.Add(key.ParagraphId + "|" + key.Label);
                parts.AddRange(key.Parts);
            }
        }
        return new FlowKey("table:" + Table.Id, parts, string.Empty);
    }

    public double Width(double available) => Columns.Count == 0 ? available : Columns.Sum();

    public double Offset(double available)
    {
        var room = Math.Max(0, available - Width(available));
        return Table.Alignment switch
        {
            TableAlignment.Center => room / 2,
            TableAlignment.Right => room,
            _ => Math.Clamp(Table.Indent.ToPoints(), -Width(available), Math.Max(0, available)),
        };
    }

    public double ColumnLeft(int column, double available) =>
        Columns.Count == 0 ? column * available / Math.Max(1, Table.ColumnCount) : Columns.Take(column).Sum();

    public double ColumnWidth(int column, int span, double available)
    {
        if (Columns.Count == 0) return span * available / Math.Max(1, Table.ColumnCount);
        return Columns.Skip(column).Take(span).Sum();
    }

    public override T Accept<T>(IFlowItemVisitor<T> visitor) => visitor.VisitTable(this);
}
