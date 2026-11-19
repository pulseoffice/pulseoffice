using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public sealed record CellAppearance(BoxBorders Borders, uint? Background, uint? Ink, bool Bold, bool OwnBorders, bool OwnBackground);

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
        var header = row < table.HeaderRows;
        return new CellAppearance(
            cell.Borders ?? StyleBorders(table, style, row, column),
            cell.Background ?? StyleBackground(table, style, row, column),
            header ? style.HeaderColor : null,
            header ? style.HeaderBold == true : style.HeaderBold == true && Emphasized(table, row, column),
            cell.Borders is not null,
            cell.Background is not null);
    }

    public BoxBorders StyleBorders(TableBlock table, TableProperties style, int row, int column)
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
        if (row < table.HeaderRows && style.HeaderBackground is { } header) return header;
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
