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

public sealed record ColumnShare(double Weight, double Gap);

public sealed record ColumnLayout(int Count, double Gap, bool Balanced, bool Separator)
{
    public static readonly ColumnLayout Single = new(1, 0, false, false);

    private const double NarrowestColumn = 1;

    public IReadOnlyList<ColumnShare> Shares { get; init; } = [];

    public PulseOffice.Documents.Blocks.ColumnSeparatorLine SeparatorLine { get; init; } = PulseOffice.Documents.Blocks.ColumnSeparatorLine.Default;

    private bool Uneven => Count > 1 && Shares.Count == Count && Shares.Sum(share => Math.Max(0, share.Weight)) > 0;

    public double Spacing(double total) => Count <= 1 || !double.IsFinite(Gap) ? 0 : Math.Clamp(Gap, 0, Math.Max(0, (total - NarrowestColumn * Count) / (Count - 1)));

    public double Width(double total) => Uneven ? Width(total, 0) : EqualWidth(total);

    public double Width(double total, int index)
    {
        if (!Uneven) return EqualWidth(total);
        var gaps = Gaps(total);
        var room = Math.Max(0, total - gaps.Sum());
        var weights = Shares.Sum(share => Math.Max(0, share.Weight));
        return Math.Max(NarrowestColumn, room * Math.Max(0, Shares[Math.Clamp(index, 0, Count - 1)].Weight) / weights);
    }

    public double GapBefore(double total, int index)
    {
        if (index <= 0) return 0;
        return Uneven ? Gaps(total)[Math.Min(index, Count) - 1] : Spacing(total);
    }

    public double Left(double x, double total, int index)
    {
        if (!Uneven) return x + index * (EqualWidth(total) + Spacing(total));
        var left = x;
        for (var column = 0; column < Math.Min(index, Count); column++) left += Width(total, column) + GapBefore(total, column + 1);
        return left;
    }

    public bool Equals(ColumnLayout? other) =>
        other is not null && Count == other.Count && Gap.Equals(other.Gap) && Balanced == other.Balanced && Separator == other.Separator && SeparatorLine == other.SeparatorLine
        && Shares.SequenceEqual(other.Shares);

    public override int GetHashCode() => HashCode.Combine(Count, Gap, Balanced, Separator, Shares.Count);

    private double EqualWidth(double total) => Count <= 1 ? total : Math.Max(NarrowestColumn, (total - Spacing(total) * (Count - 1)) / Count);

    private double[] Gaps(double total)
    {
        var gaps = Shares.Take(Count - 1).Select(share => double.IsFinite(share.Gap) ? Math.Max(0, share.Gap) : 0).ToArray();
        var limit = Math.Max(0, total - NarrowestColumn * Count);
        var sum = gaps.Sum();
        return sum <= limit || sum <= 0 ? gaps : [.. gaps.Select(gap => gap * limit / sum)];
    }
}

public sealed class SectionStartItem(SectionBlock section, ColumnLayout columns) : FlowItem
{
    public SectionBlock Section { get; } = section;

    public ColumnLayout Columns { get; } = columns;

    public override FlowKey Key => new("section-start:" + Section.Id + (Section.StartsInNextColumn ? ":next-column" : string.Empty), [Columns], string.Empty);

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

    public IReadOnlyList<double>? AutoMinimum { get; init; }

    public IReadOnlyList<double>? AutoMaximum { get; init; }

    public override FlowKey Key => field ??= Compose();

    private FlowKey Compose()
    {
        var structure = string.Join(';', Columns.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))
            + "|" + string.Join(';', Rows.Select(row => $"{row.Height}:{row.Exact}:{row.Row.AllowBreakAcrossPages}:" + string.Join(',', row.Cells.Select(cell => $"{cell.Column}/{cell.ColumnSpan}/{cell.RowSpan}/{cell.Cell.Background}/{cell.Cell.VerticalAlign}/{cell.Cell.TextDirection}/{cell.Cell.Padding}/{cell.Appearance}"))))
            + "|" + Table.HeaderRows + Table.RepeatHeaderRows + "|" + Table.PageStyleName + Table.PageBreakBefore + "|" + Table.Alignment + Table.Indent.Value + "|" + Table.WidthPercent
            + "|" + string.Join(';', (AutoMinimum ?? []).Concat(AutoMaximum ?? []).Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
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

    public double Width(double available) => Columns.Count == 0 ? available : Fitted(available).Sum();

    private (double Available, IReadOnlyList<double> Widths)? _fitted;

    private IReadOnlyList<double> Fitted(double available)
    {
        if (_fitted is { } cached && cached.Available == available) return cached.Widths;
        var widths = Fit(available);
        _fitted = (available, widths);
        return widths;
    }

    private IReadOnlyList<double> Fit(double available)
    {
        if (AutoMinimum is { } minimum && AutoMaximum is { } maximum && minimum.Count == Columns.Count && maximum.Count == Columns.Count && available > 0)
            return AutoFit(minimum, maximum, available);
        var scale = Scale(available);
        return scale == 1 ? Columns : [.. Columns.Select(column => column * scale)];
    }

    private IReadOnlyList<double> AutoFit(IReadOnlyList<double> minimum, IReadOnlyList<double> maximum, double available)
    {
        var low = minimum.Sum();
        var high = maximum.Select((value, column) => Math.Max(value, minimum[column])).Sum();
        if (high <= available) return [.. maximum.Select((value, column) => Math.Max(value, minimum[column]))];
        if (low >= available || high <= low) return minimum;
        var share = (available - low) / (high - low);
        return [.. minimum.Select((value, column) => value + (Math.Max(maximum[column], value) - value) * share)];
    }

    private double Scale(double available)
    {
        var natural = Columns.Sum();
        return Table.WidthPercent is { } percent && percent > 0 && natural > 0 && available > 0 ? Math.Clamp(percent, 1, 1000) / 100 * available / natural : 1;
    }

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
        Columns.Count == 0 ? column * available / Math.Max(1, Table.ColumnCount) : Fitted(available).Take(column).Sum();

    public double ColumnWidth(int column, int span, double available)
    {
        if (Columns.Count == 0) return span * available / Math.Max(1, Table.ColumnCount);
        return Fitted(available).Skip(column).Take(span).Sum();
    }

    public override T Accept<T>(IFlowItemVisitor<T> visitor) => visitor.VisitTable(this);
}
