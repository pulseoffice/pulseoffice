using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Fontwork;

namespace PulseOffice.UI.Controls.Shapes;

public interface IFontworkRenderer
{
    void Render(DrawingContext context, FontworkObject fontwork, Rect bounds);
}

public sealed class FontworkRenderer(IShapeBrushes brushes) : IFontworkRenderer
{
    private const double LargestSide = 16000;

    private readonly FontworkPath _path = new();

    public FontworkRenderer() : this(new ShapeBrushes()) { }

    public void Render(DrawingContext context, FontworkObject fontwork, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fontwork);
        if (!(bounds.Width > 0 && bounds.Height > 0) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) || string.IsNullOrEmpty(fontwork.Text)) return;

        var reduction = LargestSide / Math.Max(bounds.Width, bounds.Height);
        if (reduction >= 1)
        {
            Paint(context, fontwork, bounds);
            return;
        }

        using (context.PushTransform(Matrix.CreateScale(1 / reduction, 1 / reduction)))
            Paint(context, fontwork, new Rect(bounds.X * reduction, bounds.Y * reduction, bounds.Width * reduction, bounds.Height * reduction));
    }

    private void Paint(DrawingContext context, FontworkObject fontwork, Rect bounds)
    {
        var typeface = new Typeface($"{fontwork.FontFamily}, Inter, Helvetica, Arial",
            fontwork.Italic ? FontStyle.Italic : FontStyle.Normal, fontwork.Bold ? FontWeight.Bold : FontWeight.Normal);
        var size = Math.Max(4, bounds.Height * Scale(fontwork.Warp));
        FormattedText Glyph(string character, double em) => new(character, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, em, Brushes.Black);

        var elements = TextElements(fontwork.Text);
        var natural = elements.Sum(character => Glyph(character, size).WidthIncludingTrailingWhitespace);
        if (natural > 0)
            size = Math.Max(4, Math.Min(size * _path.Length(fontwork.Warp, bounds.Height / bounds.Width) * bounds.Width * 0.9 / natural, bounds.Height * Ceiling(fontwork.Warp)));

        var inset = fontwork.Warp == FontworkWarp.Plain ? 0 : Math.Min(size * 0.8, Math.Min(bounds.Width, bounds.Height) * 0.4);
        var area = bounds.Deflate(new Thickness(inset, inset, inset, 0));
        if (area.Width <= 0 || area.Height <= 0) area = bounds;
        if (natural > 0 && area.Width < bounds.Width)
        {
            var fitted = elements.Sum(character => Glyph(character, size).WidthIncludingTrailingWhitespace);
            var room = _path.Length(fontwork.Warp, area.Height / area.Width) * area.Width * 0.9;
            if (fitted > room) size = Math.Max(4, size * room / fitted);
        }

        var glyphs = elements.Select(character => Glyph(character, size)).ToList();
        var advances = glyphs.Select(glyph => glyph.WidthIncludingTrailingWhitespace / area.Width).ToList();
        var placements = _path.Place(fontwork.Warp, advances, area.Height / area.Width);
        bounds = area;

        var fill = brushes.Fill(fontwork.Style.Fill, bounds) ?? new SolidColorBrush(Color.FromRgb(37, 99, 235));
        var pen = fontwork.Style.Line.IsVisible ? brushes.Stroke(fontwork.Style.Line) : null;
        var shadow = fontwork.Style.Shadow.Enabled ? new SolidColorBrush(brushes.Colour(fontwork.Style.Shadow.ColorHex, Colors.Black), fontwork.Style.Shadow.Opacity) : null;

        for (var index = 0; index < glyphs.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(elements[index])) continue;
            var glyph = glyphs[index];
            var placement = placements[index];
            if (glyph.BuildGeometry(new Point(-glyph.WidthIncludingTrailingWhitespace / 2, -glyph.Baseline)) is not { } outline) continue;

            var transform = Matrix.CreateRotation(placement.AngleRadians)
                * Matrix.CreateTranslation(bounds.X + placement.X * bounds.Width, bounds.Y + placement.Y * bounds.Height);
            if (shadow is not null)
                using (context.PushTransform(transform * Matrix.CreateTranslation(fontwork.Style.Shadow.OffsetX.ToPoints(), fontwork.Style.Shadow.OffsetY.ToPoints())))
                    context.DrawGeometry(shadow, null, outline);
            using (context.PushTransform(transform))
                context.DrawGeometry(fill, pen, outline);
        }
    }

    private List<string> TextElements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) elements.Add(enumerator.GetTextElement());
        return elements;
    }

    private double Ceiling(FontworkWarp warp) => warp switch
    {
        FontworkWarp.Plain => 0.85,
        FontworkWarp.Circle => 0.2,
        FontworkWarp.ArchUp or FontworkWarp.ArchDown => 0.36,
        _ => 0.45,
    };

    private double Scale(FontworkWarp warp) => warp switch
    {
        FontworkWarp.Plain => 0.7,
        FontworkWarp.Circle => 0.16,
        FontworkWarp.ArchUp or FontworkWarp.ArchDown => 0.3,
        _ => 0.38,
    };
}
