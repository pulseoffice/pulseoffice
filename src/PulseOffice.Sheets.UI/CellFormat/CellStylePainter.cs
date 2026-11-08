using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Controls.Galleries;

namespace PulseOffice.Sheets.UI.CellFormat;

public sealed class CellStyleThumbnail(CellStyle style, string sample) : Control
{
    private static readonly IBrush Paper = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IPen Grid = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)), 1);

    public CellStyle Style { get; set; } = style;

    public string Sample { get; set; } = sample;

    public override void Render(DrawingContext context)
    {
        var box = new Rect(Bounds.Size).Deflate(1);
        if (box.Width <= 2 || box.Height <= 2) return;

        var fill = Style.BackgroundHex is { Length: > 0 } hex && Color.TryParse(hex, out var background)
            ? new SolidColorBrush(background)
            : Paper;
        context.DrawRectangle(fill, Grid, box);

        var ink = Color.TryParse(Style.Font.ColorHex, out var colour) ? colour : Colors.Black;
        var family = string.IsNullOrWhiteSpace(Style.Font.Family) ? FontFamily.Default : new FontFamily(Style.Font.Family);
        var text = new FormattedText(Sample, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(family, Style.Font.Italic ? FontStyle.Italic : FontStyle.Normal, Style.Font.Bold ? FontWeight.Bold : FontWeight.Normal),
            Math.Clamp(Style.Font.Size, 8, 16), new SolidColorBrush(ink));
        if (Decorations(Style.Font) is { Count: > 0 } decorations) text.SetTextDecorations(decorations);

        var x = Style.Horizontal switch
        {
            CellHorizontalAlignment.Right => box.Right - text.Width - 5,
            CellHorizontalAlignment.Center => box.X + (box.Width - text.Width) / 2,
            _ => box.X + 5,
        };
        using (context.PushClip(box))
            context.DrawText(text, new Point(x, box.Y + (box.Height - text.Height) / 2));

        if (Style.Borders.Bottom.Style != BorderStyle.None && Color.TryParse(Style.Borders.Bottom.ColorHex, out var line))
            context.DrawLine(new Pen(new SolidColorBrush(line), Math.Max(1, Style.Borders.Bottom.Width.ToPoints())), box.BottomLeft, box.BottomRight);
        if (Style.Borders.Top.Style != BorderStyle.None && Color.TryParse(Style.Borders.Top.ColorHex, out var top))
            context.DrawLine(new Pen(new SolidColorBrush(top), Math.Max(1, Style.Borders.Top.Width.ToPoints())), box.TopLeft, box.TopRight);
    }

    public static TextDecorationCollection Decorations(CellFont font)
    {
        var decorations = new TextDecorationCollection();
        if (font.Underline || font.DoubleUnderline) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        if (font.Strikethrough) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        if (font.Overline) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Overline });
        return decorations;
    }
}

public sealed class CellStylePainter(IReadOnlyDictionary<string, CellStyle> styles, string sample) : IThumbnailPainter<string>
{
    public Control Create(string item, Size size) =>
        new CellStyleThumbnail(styles.TryGetValue(item, out var style) ? style : CellStyle.Default, sample) { Height = size.Height };

    public void Repaint(Control thumbnail, string item)
    {
        if (thumbnail is CellStyleThumbnail cell && styles.TryGetValue(item, out var style)) cell.Style = style;
        thumbnail.InvalidateVisual();
    }
}
