using System.Globalization;
using PulseOffice.Exchange;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Foundation.Colors;
using PulseOffice.Sheets.Formatting;
using BorderLineStyle = PulseOffice.Text.Rich.BorderLineStyle;

namespace PulseOffice.Shell;

public interface IHtmlTableGrids
{
    CellRangeContent? Grid(ExchangePackage package);
}

public sealed class HtmlTableGrids(IHtmlTableReader tables, ISpreadsheetLocale locale) : IHtmlTableGrids
{
    public CellRangeContent? Grid(ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Content is not null || package.TextOf(ExchangeFlavor.Html) is not { } html) return null;
        if (tables.FirstTable(html) is not { Rows.Count: > 0 } table || table.ColumnCount == 0) return null;
        var columns = table.ColumnCount;
        var shift = table.Caption is { Length: > 0 } ? 1 : 0;
        var rows = new List<IReadOnlyList<ExchangeCell>>(table.Rows.Count + shift);
        var merges = new List<ExchangeMerge>();
        if (shift == 1)
        {
            rows.Add([new ExchangeCell { Kind = ExchangeCellKind.Text, Text = table.Caption!, Horizontal = ExchangeHorizontalAlignment.Center }, .. Enumerable.Repeat(ExchangeCell.Empty, columns - 1)]);
            if (columns > 1) merges.Add(new ExchangeMerge(0, 0, 1, columns));
        }
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var source = table.Rows[row];
            rows.Add([.. Enumerable.Range(0, columns).Select(column => Cell(column < source.Count ? source[column] : null))]);
            for (var column = 0; column < source.Count; column++)
                if (source[column] is { } spanning && (spanning.ColumnSpan > 1 || spanning.RowSpan > 1))
                    merges.Add(new ExchangeMerge(row + shift, column,
                        Math.Clamp(spanning.RowSpan, 1, table.Rows.Count - row),
                        Math.Clamp(spanning.ColumnSpan, 1, columns - column)));
        }
        return new CellRangeContent
        {
            SheetName = string.Empty,
            RowCount = rows.Count,
            ColumnCount = columns,
            Rows = rows,
            Merges = merges,
        };
    }

    private ExchangeCell Cell(HtmlTableCell? cell)
    {
        if (cell is null) return ExchangeCell.Empty;
        var parsed = 0d;
        var number = cell.Text.Length > 0 && double.TryParse(cell.Text.Trim(), NumberStyles.Number, locale.Culture, out parsed);
        return new ExchangeCell
        {
            Kind = cell.Text.Length == 0 ? ExchangeCellKind.Empty : number ? ExchangeCellKind.Number : ExchangeCellKind.Text,
            Number = number ? parsed : 0,
            Text = cell.Text,
            Bold = cell.Bold,
            Italic = cell.Italic,
            Underline = cell.Underline,
            Horizontal = cell.Alignment switch
            {
                HtmlTextAlignment.Left => ExchangeHorizontalAlignment.Left,
                HtmlTextAlignment.Center => ExchangeHorizontalAlignment.Center,
                HtmlTextAlignment.Right => ExchangeHorizontalAlignment.Right,
                HtmlTextAlignment.Justify => ExchangeHorizontalAlignment.Justify,
                _ => ExchangeHorizontalAlignment.General,
            },
            Vertical = cell.VerticalAlignment switch
            {
                HtmlVerticalAlignment.Top => ExchangeVerticalAlignment.Top,
                HtmlVerticalAlignment.Middle => ExchangeVerticalAlignment.Middle,
                HtmlVerticalAlignment.Bottom => ExchangeVerticalAlignment.Bottom,
                _ => ExchangeVerticalAlignment.Default,
            },
            ColorHex = cell.ColorHex,
            BackgroundHex = cell.BackgroundHex,
            Borders = cell.Borders is { IsEmpty: false } edges
                ? new ExchangeBorders { Left = Line(edges.Left), Top = Line(edges.Top), Right = Line(edges.Right), Bottom = Line(edges.Bottom) }
                : null,
        };
    }

    private ExchangeBorderLine? Line(HtmlBorderLine? line) =>
        line is null ? null : new ExchangeBorderLine(line.WidthPoints, new ArgbColor(line.Color).ToHashedHex())
        {
            Style = line.Style switch
            {
                BorderLineStyle.Dashed => ExchangeLineStyle.Dashed,
                BorderLineStyle.Dotted => ExchangeLineStyle.Dotted,
                BorderLineStyle.Double => ExchangeLineStyle.Double,
                _ => ExchangeLineStyle.Solid,
            },
        };
}
