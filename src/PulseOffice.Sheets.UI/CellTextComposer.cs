using Avalonia.Media;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI;

public interface ICellTextComposer
{
    FormattedText Compose(string text, CellFont font, IBrush brush);

    bool Wrap(FormattedText text, double available, CellHorizontalAlignment alignment);

    TextExtent Extent(FormattedText text, int rotationDegrees);
}

public sealed class CellTextComposer(PulseOffice.UI.Controls.Text.ITextDecorationFactory? decorations = null) : ICellTextComposer
{
    private const double ScriptScale = 0.68;
    private const int TypefaceLimit = 256;

    private readonly Dictionary<(string Family, bool Italic, bool Bold), Typeface> _typefaces = [];
    private readonly PulseOffice.UI.Controls.Text.ITextDecorationFactory _decorations = decorations ?? new PulseOffice.UI.Controls.Text.TextDecorationFactory();

    public FormattedText Compose(string text, CellFont font, IBrush brush)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);

        var size = font.Size * CellBoxMetrics.PixelsPerPoint;
        if (font.Script is CellScript.Superscript or CellScript.Subscript) size *= ScriptScale;

        var formatted = new FormattedText(
            text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            TypefaceOf(font), size, brush);

        if (_decorations.Create(font.Underline, font.Strikethrough, font.Overline) is { } decorations)
            formatted.SetTextDecorations(decorations);

        return formatted;
    }

    private Typeface TypefaceOf(CellFont font)
    {
        var key = (font.Family, font.Italic, font.Bold);
        if (_typefaces.TryGetValue(key, out var typeface)) return typeface;

        if (_typefaces.Count >= TypefaceLimit) _typefaces.Clear();
        typeface = new Typeface(
            new FontFamily(font.Family),
            font.Italic ? FontStyle.Italic : FontStyle.Normal,
            font.Bold ? FontWeight.Bold : FontWeight.Normal);
        _typefaces[key] = typeface;
        return typeface;
    }

    public bool Wrap(FormattedText text, double available, CellHorizontalAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Width <= available) return false;

        text.MaxTextWidth = Math.Max(available, 1);
        text.TextAlignment = alignment switch
        {
            CellHorizontalAlignment.Center => TextAlignment.Center,
            CellHorizontalAlignment.Right => TextAlignment.Right,
            CellHorizontalAlignment.Justify => TextAlignment.Justify,
            _ => TextAlignment.Left,
        };
        return true;
    }

    public TextExtent Extent(FormattedText text, int rotationDegrees)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (rotationDegrees % 360 == 0) return new TextExtent(text.Width, text.Height);

        var radians = rotationDegrees * Math.PI / 180;
        var (sin, cos) = (Math.Abs(Math.Sin(radians)), Math.Abs(Math.Cos(radians)));
        return new TextExtent(text.Width * cos + text.Height * sin, text.Width * sin + text.Height * cos);
    }
}
