using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.Printing;

public sealed record SheetCellText(FormattedCell Formatted, CellStyle Style, DocumentRect Cell, DocumentRect Text, PrintColor Color);

public interface ISheetCellTextPainter
{
    PrintFont FontOf(CellStyle style);

    bool Spills(CellStyle style);

    void Paint(IPageCanvas canvas, SheetCellText cell);
}

public sealed class SheetCellTextPainter(IPrintTextBlock block, IPrintRotatedText rotated, ICellTextPlacement placement) : ISheetCellTextPainter
{
    public SheetCellTextPainter()
        : this(new PrintTextBlock(), new PrintRotatedText(), new CellTextPlacement())
    {
    }

    public PrintFont FontOf(CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return new PrintFont(style.Font.Family, style.Font.Size, style.Font.Bold, style.Font.Italic)
        {
            Underline = style.Font.DoubleUnderline ? PrintUnderline.Double
                : style.Font.Underline ? PrintUnderline.Single
                : PrintUnderline.None,
            Strikethrough = style.Font.Strikethrough,
        };
    }

    public bool Spills(CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return !style.WrapText && Turned(style) == 0;
    }

    public void Paint(IPageCanvas canvas, SheetCellText cell)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(cell);
        var text = cell.Formatted.Text;
        if (text.Length == 0) return;

        var font = FontOf(cell.Style);
        var alignment = Align(cell.Style, cell.Formatted);

        if (Turned(cell.Style) is var degrees and not 0)
        {
            PaintTurned(canvas, cell, font, alignment, degrees);
            return;
        }

        var multiline = text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal);
        var wraps = cell.Style.WrapText && !cell.Formatted.AlignRight
            && (multiline || canvas.MeasureText(text, font).Value > cell.Text.Width.Value);

        if (wraps || multiline)
        {
            block.Draw(canvas, new PrintTextBlockRequest(text, cell.Text, font, cell.Color, alignment, Baseline(cell.Style), wraps));
            return;
        }

        canvas.DrawText(text, cell.Text, font, cell.Color, alignment, Baseline(cell.Style));
    }

    private void PaintTurned(IPageCanvas canvas, SheetCellText cell, PrintFont font, TextAlignment alignment, int degrees)
    {
        var flat = cell.Formatted.Text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
        var width = canvas.MeasureText(flat, font).ToPoints();
        var height = block.LineHeightPoints(font);
        var box = new CellBox(cell.Cell.Left.ToPoints(), cell.Cell.Top.ToPoints(), cell.Cell.Width.ToPoints(), cell.Cell.Height.ToPoints());
        var padding = (cell.Text.Left - cell.Cell.Left).ToPoints();
        var (centreX, centreY) = placement.RotatedCentre(
            box, width, height, degrees, alignment == TextAlignment.Right, alignment == TextAlignment.Center, cell.Style.Vertical, padding);

        using var clip = canvas.Clip(cell.Cell);
        rotated.Draw(canvas, new PrintRotatedTextRequest(flat, centreX, centreY, font, cell.Color, degrees));
    }

    private int Turned(CellStyle style) => style.RotationDegrees % 360;

    private TextAlignment Align(CellStyle style, FormattedCell formatted) => style.Horizontal switch
    {
        CellHorizontalAlignment.Left or CellHorizontalAlignment.Justify or CellHorizontalAlignment.Fill => TextAlignment.Left,
        CellHorizontalAlignment.Center => TextAlignment.Center,
        CellHorizontalAlignment.Right => TextAlignment.Right,
        _ => formatted.AlignRight ? TextAlignment.Right : TextAlignment.Left,
    };

    private TextBaseline Baseline(CellStyle style) => style.Vertical switch
    {
        CellVerticalAlignment.Top => TextBaseline.Top,
        CellVerticalAlignment.Middle => TextBaseline.Middle,
        _ => TextBaseline.Bottom,
    };
}
