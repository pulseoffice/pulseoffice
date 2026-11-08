using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Exchange;

public interface IDeckPortableTextBox
{
    ShapeObject? Portable(TextElement element);
}

public sealed class DeckPortableTextBox : IDeckPortableTextBox
{
    private const string DefaultFamily = "Helvetica";
    private const string DefaultInk = "#000000";

    public ShapeObject? Portable(TextElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Text.IsEmpty) return null;
        var first = element.Text.Paragraphs.FirstOrDefault(paragraph => paragraph.Text.Length > 0) ?? element.Text.Paragraphs[0];
        var run = first.Runs.FirstOrDefault(item => item.Text.Length > 0);
        var backdrop = element.Backdrop;
        return new ShapeObject
        {
            Id = element.Id,
            Shape = backdrop?.Shape ?? ShapeKind.Rectangle,
            Text = element.Text.PlainText,
            NaturalSize = new DocumentSize(element.Bounds.Width, element.Bounds.Height),
            Style = ShapeStyle.Default with
            {
                Fill = backdrop?.Fill ?? ShapeFill.None,
                Line = backdrop?.Line ?? ShapeLine.None,
                Text = ShapeTextStyle.Default with
                {
                    Family = run?.Font ?? DefaultFamily,
                    Size = run?.FontSize ?? first.FontSize,
                    Bold = run?.Bold ?? first.Bold,
                    Italic = run?.Italic ?? first.Italic,
                    Underline = run?.Underline ?? first.Underline,
                    ColorHex = (run?.Color ?? first.Color) is { } ink ? Hex(ink) : DefaultInk,
                    Horizontal = Horizontal(first.Align),
                    Vertical = TextVerticalPlacement.Top,
                    Inset = Twip.FromPoints(4),
                },
            },
        };
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();

    private TextHorizontalPlacement Horizontal(TextAlign align) => align switch
    {
        TextAlign.Center => TextHorizontalPlacement.Centre,
        TextAlign.Right => TextHorizontalPlacement.Right,
        TextAlign.Justify => TextHorizontalPlacement.Justify,
        _ => TextHorizontalPlacement.Left,
    };
}
