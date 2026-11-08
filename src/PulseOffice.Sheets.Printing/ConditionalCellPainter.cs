using PulseOffice.Drawing.ConditionalIcons;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Printing;

public sealed class ConditionalCellPainter(IConditionalIconCatalog icons)
{
    private readonly PrintColor _white = PrintColor.FromHex("#FFFFFF");
    private readonly ShapeOutline _square = new OutlineBuilder().Rectangle(0, 0, 1, 1).Build();

    public ConditionalCellPainter() : this(new ConditionalIconCatalog()) { }

    public Twip IconReserve(DocumentRect bounds) => new(Math.Min(bounds.Height.Value, Twip.FromPoints(13).Value));

    public void PaintFill(IPageCanvas canvas, ConditionalAppearance appearance, DocumentRect bounds)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(appearance);

        if (Colour(appearance.ScaleColorHex) is { } scale) canvas.FillRectangle(bounds, scale);

        if (appearance.BarShape is not { } bar || Colour(bar.FillHex) is not { } fill) return;
        var left = bounds.Left + Share(bounds.Width, bar.Start);
        var width = Share(bounds.Width, bar.End) - Share(bounds.Width, bar.Start);
        var inset = Twip.FromPoints(1);
        if (width.Value > 0)
            canvas.DrawPath(new PrintPath(_square, new DocumentRect(left, bounds.Top + inset, width, bounds.Height - inset - inset))
            {
                Fill = fill,
                Gradient = bar.Gradient ? new PrintGradient(fill, _white, bar.Axis is { } at && bar.End <= at ? 180 : 0, false) : null,
                Stroke = Colour(bar.BorderHex),
                StrokeWidth = Twip.FromPoints(0.5),
            });

        if (bar.Axis is { } axis && Colour(bar.AxisColorHex) is { } axisColor)
        {
            var x = bounds.Left + Share(bounds.Width, axis);
            canvas.DrawLine(new DocumentPoint(x, bounds.Top), new DocumentPoint(x, bounds.Top + bounds.Height), axisColor, Twip.FromPoints(0.5));
        }
    }

    private Twip Share(Twip whole, double fraction) => new((int)Math.Round(whole.Value * Math.Clamp(fraction, 0, 1)));

    public void PaintIcon(IPageCanvas canvas, ConditionalAppearance appearance, DocumentRect bounds)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(appearance);
        if (appearance.IconIndex is not { } index || appearance.IconSetName is not { } name) return;

        var padding = Twip.FromPoints(1.5);
        var size = new Twip(Math.Max(1, IconReserve(bounds).Value - padding.Value * 2));
        var box = new DocumentRect(bounds.Left + padding, bounds.Top + new Twip((bounds.Height.Value - size.Value) / 2), size, size);

        foreach (var layer in icons.Icon(name, index).Layers)
            canvas.DrawPath(new PrintPath(layer.Outline, box)
            {
                Fill = Colour(layer.Fill),
                Stroke = layer.StrokeWidth > 0 ? Colour(layer.Stroke) : null,
                StrokeWidth = new Twip(Math.Max(1, (int)Math.Round(layer.StrokeWidth * size.Value))),
            });
    }

    private PrintColor? Colour(string? hex) =>
        hex is { Length: > 0 } && hex.TrimStart('#') is { } text && text.Length is 6 or 8 && text.All(Uri.IsHexDigit)
            ? PrintColor.FromHex(hex)
            : null;
}
