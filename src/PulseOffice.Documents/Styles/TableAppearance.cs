using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public sealed record CellAppearance(BoxBorders Borders, uint? Background, uint? Ink, bool Bold, bool OwnBorders, bool OwnBackground);

public interface ITableAppearance
{
    TableProperties Style(StyleSheet sheet, TableBlock table);

    CellAppearance Cell(TableBlock table, TableProperties style, int row, int column);

    BoxBorders StyleBorders(TableBlock table, TableProperties style, int row, int column);

    uint? StyleBackground(TableBlock table, TableProperties style, int row);
}

public sealed class TableAppearance(IStyleResolver resolver) : ITableAppearance
{
    public TableProperties Style(StyleSheet sheet, TableBlock table) => table.Direct.Over(resolver.ResolveTableStyle(sheet, table.StyleName));

    public CellAppearance Cell(TableBlock table, TableProperties style, int row, int column)
    {
        var cell = table.Cell(row, column);
        var header = row < table.HeaderRows;
        return new CellAppearance(
            cell.Borders ?? StyleBorders(table, style, row, column),
            cell.Background ?? StyleBackground(table, style, row),
            header ? style.HeaderColor : null,
            header && style.HeaderBold == true,
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
        return new BoxBorders
        {
            Top = row == 0 ? line : null,
            Bottom = last == table.Rows.Count - 1 || last == headerEnd ? line : null,
        };
    }

    public uint? StyleBackground(TableBlock table, TableProperties style, int row)
    {
        if (row < table.HeaderRows && style.HeaderBackground is { } header) return header;
        var body = row - Math.Max(0, table.HeaderRows);
        if (body >= 0 && body % 2 == 1 && style.BandBackground is { } band) return band;
        return style.Background;
    }
}
