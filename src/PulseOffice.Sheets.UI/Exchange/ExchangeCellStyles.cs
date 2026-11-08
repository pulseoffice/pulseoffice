using PulseOffice.Exchange;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI.Exchange;

public interface IExchangeCellStyles
{
    ExchangeCell Describe(ExchangeCell cell, CellStyle style);

    CellStyle? Style(ExchangeCell cell);
}

public sealed class ExchangeCellStyles : IExchangeCellStyles
{
    public ExchangeCell Describe(ExchangeCell cell, CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(style);
        var borders = new ExchangeBorders { Left = Line(style.Borders.Left), Top = Line(style.Borders.Top), Right = Line(style.Borders.Right), Bottom = Line(style.Borders.Bottom) };
        return cell with
        {
            Bold = style.Font.Bold,
            Italic = style.Font.Italic,
            Underline = style.Font.Underline || style.Font.DoubleUnderline,
            Strikethrough = style.Font.Strikethrough,
            FontFamily = style.Font.Family != CellFont.Default.Family ? style.Font.Family : null,
            FontSizePoints = style.Font.Size != CellFont.Default.Size ? style.Font.Size : null,
            ColorHex = style.Font.ColorHex is { } color && color != CellFont.Default.ColorHex ? color : null,
            BackgroundHex = style.BackgroundHex,
            Horizontal = style.Horizontal switch
            {
                CellHorizontalAlignment.Left or CellHorizontalAlignment.Fill => ExchangeHorizontalAlignment.Left,
                CellHorizontalAlignment.Center => ExchangeHorizontalAlignment.Center,
                CellHorizontalAlignment.Right => ExchangeHorizontalAlignment.Right,
                CellHorizontalAlignment.Justify => ExchangeHorizontalAlignment.Justify,
                _ => ExchangeHorizontalAlignment.General,
            },
            Vertical = style.Vertical switch
            {
                CellVerticalAlignment.Top => ExchangeVerticalAlignment.Top,
                CellVerticalAlignment.Middle => ExchangeVerticalAlignment.Middle,
                _ => ExchangeVerticalAlignment.Bottom,
            },
            Wrap = style.WrapText,
            Borders = borders.IsEmpty ? null : borders,
        };
    }

    public CellStyle? Style(ExchangeCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        if (!cell.Bold && !cell.Italic && !cell.Underline && !cell.Strikethrough && cell.ColorHex is null && cell.BackgroundHex is null
            && cell.FontFamily is null && cell.FontSizePoints is null && cell.Horizontal == ExchangeHorizontalAlignment.General
            && cell.Vertical is ExchangeVerticalAlignment.Default or ExchangeVerticalAlignment.Bottom && !cell.Wrap && cell.Borders is not { IsEmpty: false }) return null;
        var font = CellFont.Default with
        {
            Bold = cell.Bold,
            Italic = cell.Italic,
            Underline = cell.Underline,
            Strikethrough = cell.Strikethrough,
            Family = cell.FontFamily is { Length: > 0 } family ? family : CellFont.Default.Family,
            Size = cell.FontSizePoints is { } size && double.IsFinite(size) && size > 0 ? size : CellFont.Default.Size,
            ColorHex = cell.ColorHex ?? CellFont.Default.ColorHex,
        };
        var shaped = CellStyle.Default with
        {
            Font = font,
            BackgroundHex = cell.BackgroundHex,
            Horizontal = cell.Horizontal switch
            {
                ExchangeHorizontalAlignment.Left => CellHorizontalAlignment.Left,
                ExchangeHorizontalAlignment.Center => CellHorizontalAlignment.Center,
                ExchangeHorizontalAlignment.Right => CellHorizontalAlignment.Right,
                ExchangeHorizontalAlignment.Justify => CellHorizontalAlignment.Justify,
                _ => CellHorizontalAlignment.General,
            },
            Vertical = cell.Vertical switch
            {
                ExchangeVerticalAlignment.Top => CellVerticalAlignment.Top,
                ExchangeVerticalAlignment.Middle => CellVerticalAlignment.Middle,
                _ => CellVerticalAlignment.Bottom,
            },
            WrapText = cell.Wrap,
            Borders = cell.Borders is { IsEmpty: false } borders ? new CellBorders(Edge(borders.Left), Edge(borders.Top), Edge(borders.Right), Edge(borders.Bottom)) : CellBorders.None,
        };
        return shaped.IsDefault ? null : shaped;
    }

    private ExchangeBorderLine? Line(BorderEdge edge) =>
        edge.IsVisible
            ? new ExchangeBorderLine(edge.Width.ToPoints(), edge.ColorHex)
            {
                Style = edge.Style switch
                {
                    BorderStyle.Dashed => ExchangeLineStyle.Dashed,
                    BorderStyle.Dotted => ExchangeLineStyle.Dotted,
                    BorderStyle.Double => ExchangeLineStyle.Double,
                    _ => ExchangeLineStyle.Solid,
                },
            }
            : null;

    private BorderEdge Edge(ExchangeBorderLine? line)
    {
        if (line is null || !double.IsFinite(line.WidthPoints) || line.WidthPoints <= 0) return BorderEdge.None;
        var style = line.Style switch
        {
            ExchangeLineStyle.Dashed => BorderStyle.Dashed,
            ExchangeLineStyle.Dotted => BorderStyle.Dotted,
            ExchangeLineStyle.Double => BorderStyle.Double,
            _ => line.WidthPoints switch { >= 2.25 => BorderStyle.Thick, >= 1.5 => BorderStyle.Medium, _ => BorderStyle.Thin },
        };
        return new BorderEdge(style, line.ColorHex, Twip.FromPoints(line.WidthPoints));
    }
}

public interface IExchangeGridSnapshots
{
    ClipboardSnapshot FromGrid(CellRangeContent grid, Workbook workbook);
}

public sealed class ExchangeGridSnapshots(IStyleTransplant transplant, IExchangeCellStyles styles) : IExchangeGridSnapshots
{
    public ClipboardSnapshot FromGrid(CellRangeContent grid, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(workbook);
        var cells = new List<(int, int, Cell)>();
        for (var row = 0; row < grid.RowCount; row++)
            for (var column = 0; column < grid.ColumnCount; column++)
            {
                var source = grid.At(row, column);
                var shaped = styles.Style(source) is { } style ? transplant.WithFormat(style, source.NumberFormat, workbook.NumberFormats)
                    : source.NumberFormat is { Length: > 0 } ? transplant.WithFormat(CellStyle.Default, source.NumberFormat, workbook.NumberFormats)
                    : null;
                if (source.Kind == ExchangeCellKind.Empty && source.Formula is null && shaped is null) continue;
                var cell = new Cell { Value = Value(source), Formula = source.Formula };
                if (shaped is not null) cell = cell with { StyleId = workbook.Styles.Intern(shaped), ExplicitStyle = true };
                cells.Add((row, column, cell));
            }
        var merges = grid.Merges
            .Where(merge => merge.RowSpan > 0 && merge.ColumnSpan > 0 && (merge.RowSpan > 1 || merge.ColumnSpan > 1))
            .Select(merge => new CellRange(new CellAddress(merge.Row, merge.Column), new CellAddress(merge.Row + merge.RowSpan - 1, merge.Column + merge.ColumnSpan - 1)))
            .ToList();
        return new ClipboardSnapshot(grid.RowCount, grid.ColumnCount, cells) { Merges = merges };
    }

    private CellValue Value(ExchangeCell cell) => cell.Kind switch
    {
        ExchangeCellKind.Number => CellValue.Number(cell.Number),
        ExchangeCellKind.Boolean => CellValue.Boolean(cell.Number != 0),
        ExchangeCellKind.Text or ExchangeCellKind.Error => CellValue.Text(cell.Text),
        _ => CellValue.Empty,
    };
}
