using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class CellValuePainter(GridViewport viewport, GridRenderServices services, GridDisplayOptions options)
{
    public void Paint(
        DrawingContext context, FormattedCell presented, CellStyle style, IBrush foreground, Rect bounds,
        Rect? spill = null, CellAddress? address = null)
    {
        var chosen = style.Font.ColorHex != CellFont.Default.ColorHex
            ? services.Paints.Brush(style.Font.ColorHex) ?? foreground
            : foreground;

        var brush = services.Paints.Brush(presented.ColorName) ?? chosen;

        var text = Layout(presented.Text, brush, style.Font);

        var padding = 3.0 + style.Indent * 8;

        var alignRight = style.Horizontal switch
        {
            CellHorizontalAlignment.Right => true,
            CellHorizontalAlignment.Left or CellHorizontalAlignment.Justify
                or CellHorizontalAlignment.Fill or CellHorizontalAlignment.Center => false,
            _ => presented.AlignRight,
        };

        var x = style.Horizontal == CellHorizontalAlignment.Center
            ? bounds.X + (bounds.Width - text.Width) / 2
            : alignRight
                ? bounds.Right - text.Width - padding
                : bounds.X + padding;

        var box = new CellBox(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var placement = services.TextPlacement;

        if (style.WrapText && style.RotationDegrees % 360 == 0 && !presented.AlignRight
            && text.Width > bounds.Width - padding * 2)
        {
            services.TextComposer.Wrap(text, bounds.Width - padding * 2, style.Horizontal);
            using (context.PushClip(bounds))
                context.DrawText(text, new Point(bounds.X + padding, placement.Top(box, text.Height, style.Vertical, 1)));
            return;
        }

        if (style.RotationDegrees % 360 != 0)
        {
            var (centreX, centreY) = placement.RotatedCentre(
                box, text.Width, text.Height, style.RotationDegrees, alignRight,
                style.Horizontal == CellHorizontalAlignment.Center, style.Vertical, padding);
            using (context.PushClip(bounds))
            using (context.PushTransform(
                       Matrix.CreateTranslation(-centreX, -centreY)
                       * Matrix.CreateRotation(-style.RotationDegrees * Math.PI / 180)
                       * Matrix.CreateTranslation(centreX, centreY)))
                context.DrawText(text, new Point(centreX - text.Width / 2, centreY - text.Height / 2));
            return;
        }

        if (alignRight && text.Width > bounds.Width - padding * 2)
        {
            var hashes = Layout(
                new string('#', Math.Max((int)(bounds.Width / (viewport.Metrics.FontSize * 0.6)), 1)),
                brush, style.Font);

            using (context.PushClip(bounds))
                context.DrawText(hashes, new Point(bounds.X + padding, placement.Top(box, hashes.Height, style.Vertical, 1)));
            return;
        }

        var clip = spill ?? bounds;

        if (clip.Width > bounds.Width && style.Horizontal == CellHorizontalAlignment.Center)
            x = bounds.X + (bounds.Width - text.Width) / 2;

        var top = placement.Top(box, text.Height, style.Vertical, 1);

        top += style.Font.Script switch
        {
            CellScript.Superscript => -text.Height * 0.22,
            CellScript.Subscript => text.Height * 0.22,

            _ => 0,
        };

        using (context.PushClip(clip))
        {
            if (style.Font.Shadow)
            {
                var shadow = Layout(presented.Text, services.Paints.Brush(Color.FromArgb(60, 0, 0, 0)), style.Font);
                context.DrawText(shadow, new Point(x + 1.2, top + 1.2));
            }

            context.DrawText(text, new Point(x, top));

            if (style.Font.DoubleUnderline)
            {
                var pen = new Pen(brush, 0.8);
                var baseline = top + text.Height - 1.5;

                context.DrawLine(pen, new Point(x, baseline), new Point(x + text.Width, baseline));
                context.DrawLine(pen, new Point(x, baseline + 2), new Point(x + text.Width, baseline + 2));
            }

            if (address is { } cell) DrawSpellingMarks(context, presented.Text, style, cell, x, top + text.Height);
        }
    }

    private void DrawSpellingMarks(
        DrawingContext context, string text, CellStyle style, CellAddress address, double left, double baseline)
    {
        if (options.Spelling is not { Enabled: true } spelling) return;
        if (viewport.Sheet is not { } sheet) return;

        var mistakes = spelling.MistakesIn(viewport.Engine.Workbook, sheet, address);
        if (mistakes.Count == 0) return;

        var pen = new Pen(Brushes.Crimson, 1.2);

        foreach (var mistake in mistakes)
        {
            if (mistake.Word.Start < 0 || mistake.Word.End > text.Length || mistake.Word.Start > mistake.Word.End) continue;

            var start = left + Measure(text[..mistake.Word.Start], style.Font);
            var end = left + Measure(text[..mistake.Word.End], style.Font);

            DrawWave(context, pen, start, end, baseline - 1);
        }
    }

    private double Measure(string text, CellFont font) =>
        text.Length == 0 ? 0 : Layout(text, Brushes.Black, font).WidthIncludingTrailingWhitespace;

    private void DrawWave(DrawingContext context, Pen pen, double from, double to, double y)
    {
        const double step = 2;

        var up = true;

        for (var x = from; x < to; x += step)
        {
            var next = Math.Min(x + step, to);

            context.DrawLine(pen, new Point(x, up ? y : y - 2), new Point(next, up ? y - 2 : y));
            up = !up;
        }
    }

    private FormattedText Layout(string text, IBrush brush, CellFont font) =>
        services.TextComposer.Compose(text, font, brush);
}
