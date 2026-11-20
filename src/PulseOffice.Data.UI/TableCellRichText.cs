using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public interface ITableCellRichText
{
    IReadOnlyList<FormattedText> Lines(TableCellText text, TableCellLook look, double size, IBrush brush, double width, System.Globalization.CultureInfo culture);

    void Draw(DrawingContext context, IReadOnlyList<FormattedText> lines, Point origin);
}

public sealed class TableCellRichText(ITableCellTextStyles styles) : ITableCellRichText
{
    public IReadOnlyList<FormattedText> Lines(TableCellText text, TableCellLook look, double size, IBrush brush, double width, System.Globalization.CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(look);
        var lines = new List<FormattedText>();
        foreach (var paragraph in text.Paragraphs)
        {
            var content = paragraph.Text.Length == 0 ? " " : paragraph.Text;
            var formatted = new FormattedText(content, culture, FlowDirection.LeftToRight, styles.Typeface(look), size, brush)
            {
                MaxTextWidth = Math.Max(1, width),
                TextAlignment = paragraph.AlignedOver(look) switch
                {
                    CellHorizontalAlign.Center => TextAlignment.Center,
                    CellHorizontalAlign.Right => TextAlignment.Right,
                    CellHorizontalAlign.Justify => TextAlignment.Justify,
                    _ => TextAlignment.Left,
                },
            };
            var start = 0;
            foreach (var run in paragraph.Runs)
            {
                var length = run.Text.Length;
                if (length > 0 && !run.IsPlain)
                {
                    var own = run.Over(look);
                    var typeface = styles.Typeface(own);
                    formatted.SetFontFamily(typeface.FontFamily, start, length);
                    formatted.SetFontWeight(typeface.Weight, start, length);
                    formatted.SetFontStyle(typeface.Style, start, length);
                    if (own.FontSize is { } runSize && run.FontSize is not null) formatted.SetFontSize(runSize, start, length);
                    if (run.TextHex is { } hex && Color.TryParse(hex, out var ink)) formatted.SetForegroundBrush(new SolidColorBrush(ink), start, length);
                    if (styles.Decorations(own) is { } decorations) formatted.SetTextDecorations(decorations, start, length);
                }
                start += length;
            }
            lines.Add(formatted);
        }
        return lines;
    }

    public void Draw(DrawingContext context, IReadOnlyList<FormattedText> lines, Point origin)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lines);
        var y = origin.Y;
        foreach (var line in lines)
        {
            context.DrawText(line, new Point(origin.X, y));
            y += line.Height;
        }
    }
}
