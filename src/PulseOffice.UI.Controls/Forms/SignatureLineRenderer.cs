using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Forms;

namespace PulseOffice.UI.Controls.Forms;

public interface ISignatureLineRenderer
{
    void Render(DrawingContext context, SignatureLineObject line, Rect bounds, FormControlPalette palette, System.Globalization.CultureInfo culture);
}

public sealed class SignatureLineRenderer : ISignatureLineRenderer
{
    public void Render(
        DrawingContext context, SignatureLineObject line, Rect bounds, FormControlPalette palette, System.Globalization.CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(culture);

        if (bounds.Width <= 8 || bounds.Height <= 8) return;

        context.DrawRectangle(palette.Field, new Pen(palette.Border), bounds, 4, 4);

        var inner = bounds.Deflate(new Thickness(10, 8));
        var ruleY = inner.Y + (inner.Height * 0.52);

        context.DrawLine(
            new Pen(palette.Text, 1),
            new Point(inner.X, ruleY),
            new Point(inner.Right, ruleY));

        Text(context, culture, "×", palette.Text, new Rect(inner.X, ruleY - 18, 20, 18), 14);

        if (line.IsSigned)
            Text(
                context,
                culture,
                line.SignedBy!,
                palette.Accent,
                new Rect(inner.X + 20, ruleY - 20, inner.Width - 20, 18),
                13);

        var caption = string.Join(
            "   ",
            new[] { line.SignerName, line.SignerTitle }.Where(part => !string.IsNullOrWhiteSpace(part)));

        if (caption.Length > 0)
            Text(context, culture, caption, palette.Text, new Rect(inner.X, ruleY + 3, inner.Width, 16), 12);

        var footer = line.ShowDate && line.SignedOn is { } signedOn
            ? signedOn.ToString("d", culture)
            : line.Instructions;

        if (!string.IsNullOrWhiteSpace(footer))
            Text(context, culture, footer, palette.Muted, new Rect(inner.X, ruleY + 21, inner.Width, 14), 11);
    }

    private static void Text(DrawingContext context, System.Globalization.CultureInfo culture, string text, IBrush ink, Rect bounds, double size)
    {
        if (bounds.Width <= 2) return;

        var formatted = new FormattedText(
            text,
            culture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            size,
            ink)
        {
            MaxTextWidth = bounds.Width,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

        context.DrawText(formatted, bounds.TopLeft);
    }
}
