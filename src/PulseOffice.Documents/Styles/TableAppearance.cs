using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public sealed record CellAppearance(BoxBorders Borders, uint? Background, uint? Ink, bool Bold, bool OwnBorders, bool OwnBackground)
{
    public bool Italic { get; init; }

    public string? Font { get; init; }

    public double? FontSize { get; init; }

    public bool StylesText => Ink is not null || Bold || Italic || Font is not null || FontSize is not null;
}

public interface ITableAppearance
{
    TableProperties Style(StyleSheet sheet, TableBlock table);

    CellAppearance Cell(TableBlock table, TableProperties style, int row, int column);

    BoxBorders StyleBorders(TableBlock table, TableProperties style, int row, int column);

    uint? StyleBackground(TableBlock table, TableProperties style, int row, int column);

    CellAppearance?[][] Cells(TableBlock table, TableProperties style);
}

public sealed class TableAppearance(IStyleResolver resolver, ICellBorderConflicts conflicts) : ITableAppearance
{
    public TableAppearance(IStyleResolver resolver) : this(resolver, new CellBorderConflicts())
    {
    }

    public CellAppearance?[][] Cells(TableBlock table, TableProperties style)
    {
        var looks = table.Rows.Select((row, r) => row.Cells.Select((cell, column) => cell.Covered ? null : Cell(table, style, r, column)).ToArray()).ToArray();
        conflicts.Resolve(table, looks);
        return looks;
    }

    public TableProperties Style(StyleSheet sheet, TableBlock table) => table.Direct.Over(resolver.ResolveTableStyle(sheet, table.StyleName));

    public CellAppearance Cell(TableBlock table, TableProperties style, int row, int column)
    {
        var cell = table.Cell(row, column);
        var regions = Regions(table, style, row, column);
        var text = regions.Aggregate(new TableRegionFormat(), (under, over) => TableRegionFormat.Merge(over, under)!);
        return new CellAppearance(
            cell.Borders ?? StyleBorders(table, style, row, column),
            cell.Background ?? Visible(StyleBackground(table, style, row, column)),
            Visible(text.Ink),
            text.Bold ?? (style.HeaderBold == true && Emphasized(table, row, column)),
            cell.Borders is not null,
            cell.Background is not null)
        {
            Italic = text.Italic == true,
            Font = string.IsNullOrEmpty(text.Font) ? null : text.Font,
            FontSize = text.FontSize is > 0 ? text.FontSize : null,
        };
    }

    private IReadOnlyList<TableRegionFormat> Regions(TableBlock table, TableProperties style, int row, int column)
    {
        var first = table.Banding.FirstColumn && column == 0;
        var last = table.Banding.LastColumn && column == table.ColumnCount - 1;
        var header = row < table.HeaderRows;
        var total = table.Banding.LastRow && row == table.Rows.Count - 1 && row >= table.HeaderRows;
        var body = row - Math.Max(0, table.HeaderRows);
        var regions = new List<TableRegionFormat>();
        void Add(bool applies, TableRegionFormat? format)
        {
            if (applies && format is not null) regions.Add(format);
        }
        Add(!header && !total && FirstBanded(table, style, body, column), style.FirstBand);
        Add(!header && !total && SecondBanded(table, style, body, column), style.SecondBand);
        Add(first, style.FirstColumn);
        Add(last, style.LastColumn);
        Add(header, TableRegionFormat.Merge(new TableRegionFormat { Ink = style.HeaderColor, Bold = style.HeaderBold }, style.HeaderRow));
        Add(total, style.TotalRow);
        Add(header && first, style.NorthWestCell);
        Add(header && last, style.NorthEastCell);
        Add(total && first, style.SouthWestCell);
        Add(total && last, style.SouthEastCell);
        return regions;
    }

    private uint? Visible(uint? colour) => colour is { } value && value >> 24 == 0 ? null : colour;

    public BoxBorders StyleBorders(TableBlock table, TableProperties style, int row, int column)
    {
        var lines = BaseBorders(table, style, row, column);
        var regional = Regions(table, style, row, column).Aggregate((BoxBorders?)null, (under, over) => TableRegionFormat.MergeBorders(over.Borders, under));
        return regional is null ? lines : lines with
        {
            Top = Drawn(regional.Top ?? lines.Top),
            Bottom = Drawn(regional.Bottom ?? lines.Bottom),
            Left = Drawn(regional.Left ?? lines.Left),
            Right = Drawn(regional.Right ?? lines.Right),
        };
    }

    private BorderLine? Drawn(BorderLine? line) => line is { Width: > 0 } ? line : null;

    private BoxBorders BaseBorders(TableBlock table, TableProperties style, int row, int column)
    {
        if (style.BorderWidth is not { } width || width <= 0) return new BoxBorders();
        var line = new BorderLine(width, style.BorderColor ?? 0xFF000000);
        if (style.InnerLines != false) return BoxBorders.Uniform(line, 0);
        var cell = table.Cell(row, column);
        var last = row + Math.Max(1, cell.RowSpan) - 1;
        var headerEnd = Math.Max(0, table.HeaderRows) - 1;
        var total = table.Banding.LastRow && row == table.Rows.Count - 1 && row >= table.HeaderRows;
        return new BoxBorders
        {
            Top = row == 0 || total ? line : null,
            Bottom = last == table.Rows.Count - 1 || last == headerEnd ? line : null,
        };
    }

    public uint? StyleBackground(TableBlock table, TableProperties style, int row, int column)
    {
        var header = row < table.HeaderRows;
        var first = table.Banding.FirstColumn && column == 0;
        var last = table.Banding.LastColumn && column == table.ColumnCount - 1;
        var total = table.Banding.LastRow && row == table.Rows.Count - 1 && !header;
        uint? fill = null;
        if (header && style.HeaderBackground is { } headerFill) fill = headerFill;
        if (total && style.TotalRow?.Background is { } totalFill) fill = totalFill;
        if (fill is null && first && style.FirstColumn?.Background is { } firstFill) fill = firstFill;
        if (fill is null && last && style.LastColumn?.Background is { } lastFill) fill = lastFill;
        var corner = (header, total, first, last) switch
        {
            (true, _, true, _) => style.NorthWestCell,
            (true, _, _, true) => style.NorthEastCell,
            (_, true, true, _) => style.SouthWestCell,
            (_, true, _, true) => style.SouthEastCell,
            _ => null,
        };
        if (corner?.Background is { } cornerFill) fill = cornerFill;
        return fill ?? BodyBackground(table, style, row, column);
    }

    private bool SecondBanded(TableBlock table, TableProperties style, int body, int column)
    {
        var axes = style.BandAxes ?? TableBandAxes.Both;
        return body >= 0 && (axes.HasFlag(TableBandAxes.Rows) && table.Banding.BandsRow(body) || axes.HasFlag(TableBandAxes.Columns) && table.Banding.BandsColumn(column));
    }

    private bool FirstBanded(TableBlock table, TableProperties style, int body, int column)
    {
        var axes = style.FirstBandAxes ?? TableBandAxes.Both;
        return body >= 0 && !SecondBanded(table, style, body, column)
            && (axes.HasFlag(TableBandAxes.Rows) && table.Banding.BandedRows && !table.Banding.BandsRow(body)
                || axes.HasFlag(TableBandAxes.Columns) && table.Banding.BandedColumns && !table.Banding.BandsColumn(column));
    }

    private uint? BodyBackground(TableBlock table, TableProperties style, int row, int column)
    {
        var body = row - Math.Max(0, table.HeaderRows);
        var bandAxes = style.BandAxes ?? TableBandAxes.Both;
        if (style.BandBackground is { } band
            && (bandAxes.HasFlag(TableBandAxes.Rows) && table.Banding.BandsRow(body) || bandAxes.HasFlag(TableBandAxes.Columns) && body >= 0 && table.Banding.BandsColumn(column))) return band;
        var firstAxes = style.FirstBandAxes ?? TableBandAxes.Both;
        if (style.FirstBandBackground is { } first && body >= 0
            && (firstAxes.HasFlag(TableBandAxes.Rows) && table.Banding.BandedRows && !table.Banding.BandsRow(body)
                || firstAxes.HasFlag(TableBandAxes.Columns) && table.Banding.BandedColumns && !table.Banding.BandsColumn(column))) return first;
        return style.Background;
    }

    private bool Emphasized(TableBlock table, int row, int column) =>
        row >= table.HeaderRows && table.Banding.Emphasizes(row, column, table.Rows.Count, table.ColumnCount);
}
