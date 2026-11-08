using System.Globalization;
using System.Net;
using System.Text;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Exchange;

public interface ICellGridConversions
{
    string Html(CellRangeContent grid);

    string Tabular(CellRangeContent grid);

    DataTableObject Table(CellRangeContent grid, string id);

    CellRangeContent Grid(DataTableObject table, CultureInfo culture);
}

public sealed class CellGridConversions : ICellGridConversions
{
    private const double ColumnWidthCm = 2.6;
    private const double RowHeightCm = 0.7;
    private const string CellPadding = "padding:1.5pt 2.5pt";

    public string Html(CellRangeContent grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var covered = grid.CoveredCells();
        var origins = grid.Merges.ToDictionary(merge => (merge.Row, merge.Column));
        var html = new StringBuilder("<html><head><meta charset=\"utf-8\"></head><body>\n<table style=\"border-collapse:collapse\">\n");
        for (var row = 0; row < grid.RowCount; row++)
        {
            html.Append("<tr>");
            for (var column = 0; column < grid.ColumnCount; column++)
            {
                if (covered[row * grid.ColumnCount + column]) continue;
                var cell = grid.At(row, column);
                html.Append("<td");
                if (origins.TryGetValue((row, column), out var merge))
                {
                    if (merge.ColumnSpan > 1) html.Append(CultureInfo.InvariantCulture, $" colspan=\"{merge.ColumnSpan}\"");
                    if (merge.RowSpan > 1) html.Append(CultureInfo.InvariantCulture, $" rowspan=\"{merge.RowSpan}\"");
                }
                html.Append(" style=\"").Append(Inline(cell)).Append('"');
                html.Append('>').Append(WebUtility.HtmlEncode(cell.Text).ReplaceLineEndings("<br>")).Append("</td>");
            }
            html.Append("</tr>\n");
        }
        return html.Append("</table>\n</body></html>\n").ToString();
    }

    public string Tabular(CellRangeContent grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var text = new StringBuilder();
        for (var row = 0; row < grid.RowCount; row++)
        {
            for (var column = 0; column < grid.ColumnCount; column++)
            {
                if (column > 0) text.Append('\t');
                text.Append(Quoted(grid.At(row, column).Text));
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    public DataTableObject Table(CellRangeContent grid, string id)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var columns = Math.Max(1, grid.ColumnCount);
        var rows = Math.Max(1, grid.RowCount);
        var kinds = Enumerable.Range(0, columns).Select(column => Kind(grid, column)).ToArray();
        var formatted = grid.Rows.Any(row => row.Any(Styled));
        return new DataTableObject
        {
            Id = id,
            Name = grid.SheetName,
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot
            {
                Columns = [.. Enumerable.Range(0, columns).Select(column => new DataColumn(Header(grid, column), kinds[column]))],
                Rows = [.. Enumerable.Range(1, rows - 1).Select(row => (IReadOnlyList<object?>)[.. Enumerable.Range(0, columns).Select(column => Value(grid.At(row, column), kinds[column]))])],
            },
            Style = new DataTableStyle { HeaderRow = true, BandedRows = false, Grid = !formatted, OwnLook = formatted },
            Merges = [.. grid.Merges.Select(merge => new TableMerge(merge.Row + TableCellFormat.HeaderRow, merge.Column, merge.RowSpan, merge.ColumnSpan))],
            CellFormats = formatted ? Formats(grid) : [],
            NaturalSize = new DocumentSize(Extent(ColumnWidthCm * columns), Extent(RowHeightCm * rows)),
        };
    }

    public CellRangeContent Grid(DataTableObject table, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(culture);
        var columns = table.Snapshot.Columns;
        var formats = new Dictionary<(int Row, int Column), TableCellFormat>();
        foreach (var format in table.CellFormats) formats[(format.Row, format.Column)] = format;
        var rows = new List<IReadOnlyList<ExchangeCell>>(table.Snapshot.Rows.Count + 1);
        rows.Add([.. columns.Select((column, index) => Described(new ExchangeCell { Kind = column.Name.Length == 0 ? ExchangeCellKind.Empty : ExchangeCellKind.Text, Text = column.Name }, formats.GetValueOrDefault((TableCellFormat.HeaderRow, index))))]);
        for (var row = 0; row < table.Snapshot.Rows.Count; row++)
        {
            var values = table.Snapshot.Rows[row];
            rows.Add([.. columns.Select((column, index) => Described(Cell(index < values.Count ? values[index] : null, column.Kind, culture), formats.GetValueOrDefault((row, index))))]);
        }
        var height = rows.Count;
        return new CellRangeContent
        {
            SheetName = table.Name ?? string.Empty,
            RowCount = height,
            ColumnCount = columns.Count,
            Rows = rows,
            Merges = [.. table.Merges
                .Select(merge => new ExchangeMerge(merge.Row - TableCellFormat.HeaderRow, merge.Column, merge.RowSpan, merge.ColumnSpan))
                .Where(merge => merge.Row >= 0 && merge.Column >= 0 && merge.RowSpan >= 1 && merge.ColumnSpan >= 1
                    && (long)merge.Row + merge.RowSpan <= height && (long)merge.Column + merge.ColumnSpan <= columns.Count)],
        };
    }

    private ExchangeCell Cell(object? value, DataValueKind kind, CultureInfo culture) => value switch
    {
        null or DBNull => ExchangeCell.Empty,
        DateTime moment => new ExchangeCell
        {
            Kind = ExchangeCellKind.Text,
            Text = kind == DataValueKind.Date && moment.TimeOfDay == TimeSpan.Zero ? moment.ToString("d", culture) : moment.ToString("g", culture),
        },
        bool flag => new ExchangeCell { Kind = ExchangeCellKind.Boolean, Number = flag ? 1 : 0, Text = flag.ToString(culture) },
        double or float or decimal or int or long or short or byte when kind == DataValueKind.Number => new ExchangeCell
        {
            Kind = ExchangeCellKind.Number,
            Number = Convert.ToDouble(value, CultureInfo.InvariantCulture),
            Text = ((IFormattable)value).ToString("0.########", culture),
        },
        _ => new ExchangeCell { Kind = ExchangeCellKind.Text, Text = Convert.ToString(value, culture) ?? string.Empty },
    };

    private ExchangeCell Described(ExchangeCell cell, TableCellFormat? format)
    {
        if (format?.Look is not { } look) return cell;
        var borders = new ExchangeBorders { Left = Line(look.Left), Top = Line(look.Top), Right = Line(look.Right), Bottom = Line(look.Bottom) };
        return cell with
        {
            Bold = look.Bold,
            Italic = look.Italic,
            Underline = look.Underline,
            Strikethrough = look.Strikethrough,
            FontFamily = look.FontFamily is { Length: > 0 } family ? family : cell.FontFamily,
            ColorHex = HexColour(look.TextHex),
            BackgroundHex = HexColour(look.FillHex),
            FontSizePoints = look.FontSize,
            Horizontal = look.Horizontal switch
            {
                CellHorizontalAlign.Left => ExchangeHorizontalAlignment.Left,
                CellHorizontalAlign.Center => ExchangeHorizontalAlignment.Center,
                CellHorizontalAlign.Right => ExchangeHorizontalAlignment.Right,
                _ => ExchangeHorizontalAlignment.General,
            },
            Vertical = format.VerticalAlign switch
            {
                CellVerticalAlign.Top => ExchangeVerticalAlignment.Top,
                CellVerticalAlign.Bottom => ExchangeVerticalAlignment.Bottom,
                _ => ExchangeVerticalAlignment.Middle,
            },
            Borders = borders.IsEmpty ? null : borders,
        };
    }

    private ExchangeBorderLine? Line(TableBorderLine? line) =>
        line is { } edge && double.IsFinite(edge.WidthPoints) && edge.WidthPoints > 0 && HexColour(edge.ColorHex) is { } colour
            ? new ExchangeBorderLine(edge.WidthPoints, colour)
            : null;

    private bool Styled(ExchangeCell cell) =>
        cell.Bold || cell.Italic || cell.Underline || cell.Strikethrough || cell.FontFamily is { Length: > 0 }
        || cell.ColorHex is not null || cell.BackgroundHex is not null || cell.FontSizePoints is not null
        || cell.Horizontal != ExchangeHorizontalAlignment.General || cell.Vertical != ExchangeVerticalAlignment.Default
        || cell.Borders is { IsEmpty: false };

    private List<TableCellFormat> Formats(CellRangeContent grid)
    {
        var formats = new List<TableCellFormat>(grid.RowCount * grid.ColumnCount);
        for (var row = 0; row < grid.RowCount; row++)
            for (var column = 0; column < grid.ColumnCount; column++)
            {
                var cell = grid.At(row, column);
                formats.Add(new TableCellFormat(row + TableCellFormat.HeaderRow, column, Vertical(cell.Vertical)) { Look = Look(cell) });
            }
        return formats;
    }

    private TableCellLook Look(ExchangeCell cell) => new()
    {
        FillHex = HexColour(cell.BackgroundHex),
        TextHex = HexColour(cell.ColorHex),
        Bold = cell.Bold,
        Italic = cell.Italic,
        Underline = cell.Underline,
        Strikethrough = cell.Strikethrough,
        FontFamily = FamilyName(cell.FontFamily) is { Length: > 0 } family ? family : null,
        FontSize = cell.FontSizePoints is { } size && double.IsFinite(size) && size > 0 ? size : null,
        Horizontal = cell.Horizontal switch
        {
            ExchangeHorizontalAlignment.Left or ExchangeHorizontalAlignment.Justify => CellHorizontalAlign.Left,
            ExchangeHorizontalAlignment.Center => CellHorizontalAlign.Center,
            ExchangeHorizontalAlignment.Right => CellHorizontalAlign.Right,
            _ => cell.Kind == ExchangeCellKind.Number ? CellHorizontalAlign.Right : null,
        },
        Top = Line(cell.Borders?.Top),
        Bottom = Line(cell.Borders?.Bottom),
        Left = Line(cell.Borders?.Left),
        Right = Line(cell.Borders?.Right),
    };

    private CellVerticalAlign Vertical(ExchangeVerticalAlignment alignment) => alignment switch
    {
        ExchangeVerticalAlignment.Top => CellVerticalAlign.Top,
        ExchangeVerticalAlignment.Bottom => CellVerticalAlign.Bottom,
        _ => CellVerticalAlign.Center,
    };

    private TableBorderLine? Line(ExchangeBorderLine? line) =>
        line is { } edge && double.IsFinite(edge.WidthPoints) && edge.WidthPoints > 0 && HexColour(edge.ColorHex) is { } colour
            ? new TableBorderLine(colour, edge.WidthPoints)
            : null;

    private Twip Extent(double centimeters) =>
        new((int)Math.Min(ExchangeContentGuard.MaximumExtent, Math.Round(centimeters * Twip.PerCentimeter)));

    private string Header(CellRangeContent grid, int column) => grid.At(0, column).Text;

    private DataValueKind Kind(CellRangeContent grid, int column)
    {
        var body = Enumerable.Range(1, Math.Max(0, grid.RowCount - 1)).Select(row => grid.At(row, column)).Where(cell => cell.Kind != ExchangeCellKind.Empty).ToList();
        return body.Count > 0 && body.All(cell => cell.Kind == ExchangeCellKind.Number) ? DataValueKind.Number : DataValueKind.Text;
    }

    private object? Value(ExchangeCell cell, DataValueKind kind) => cell.Kind switch
    {
        ExchangeCellKind.Empty => null,
        ExchangeCellKind.Number when kind == DataValueKind.Number => cell.Number,
        _ => cell.Text,
    };

    private string Inline(ExchangeCell cell)
    {
        var rules = new List<string> { CellPadding };
        if (cell.Bold) rules.Add("font-weight:bold");
        if (cell.Italic) rules.Add("font-style:italic");
        var decorations = new List<string>();
        if (cell.Underline) decorations.Add("underline");
        if (cell.Strikethrough) decorations.Add("line-through");
        if (decorations.Count > 0) rules.Add("text-decoration:" + string.Join(' ', decorations));
        if (Aligned(cell) is { } align) rules.Add("text-align:" + align);
        if (cell.Vertical != ExchangeVerticalAlignment.Default) rules.Add("vertical-align:" + cell.Vertical.ToString().ToLowerInvariant());
        if (FamilyName(cell.FontFamily) is { Length: > 0 } family) rules.Add("font-family:'" + family + "'");
        if (cell.FontSizePoints is { } size && double.IsFinite(size) && size > 0) rules.Add("font-size:" + Points(size));
        if (HexColour(cell.ColorHex) is { } color) rules.Add("color:" + color);
        if (HexColour(cell.BackgroundHex) is { } background) rules.Add("background:" + background);
        if (cell.Borders is { } borders)
        {
            if (BorderCss(borders.Top) is { } top) rules.Add("border-top:" + top);
            if (BorderCss(borders.Right) is { } right) rules.Add("border-right:" + right);
            if (BorderCss(borders.Bottom) is { } bottom) rules.Add("border-bottom:" + bottom);
            if (BorderCss(borders.Left) is { } left) rules.Add("border-left:" + left);
        }
        return string.Join(';', rules);
    }

    private string? Aligned(ExchangeCell cell) => cell.Horizontal switch
    {
        ExchangeHorizontalAlignment.Left => "left",
        ExchangeHorizontalAlignment.Center => "center",
        ExchangeHorizontalAlignment.Right => "right",
        ExchangeHorizontalAlignment.Justify => "justify",
        _ when cell.Kind == ExchangeCellKind.Number => "right",
        _ => null,
    };

    private string? BorderCss(ExchangeBorderLine? line) =>
        line is { } edge && double.IsFinite(edge.WidthPoints) && edge.WidthPoints > 0 && HexColour(edge.ColorHex) is { } colour
            ? Points(edge.WidthPoints) + " " + edge.Style.ToString().ToLowerInvariant() + " " + colour
            : null;

    private string Points(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "pt";

    private string? FamilyName(string? family) =>
        family is null ? null : new string([.. family.Where(character => !char.IsControl(character) && character is not ('\'' or '"' or '<' or '>' or ';' or '&' or '\\'))]).Trim();

    private string? HexColour(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && hex.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") is false ? hex : null;

    private string Quoted(string value) =>
        value.IndexOfAny(['\t', '\n', '\r']) >= 0 || value.StartsWith('"')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}
