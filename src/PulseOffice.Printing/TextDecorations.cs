using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public readonly record struct PrintTextSpan(
    double X, double Baseline, double Width, PrintFont Font, PrintColor Color, double RotationDegrees = 0);

public interface ITextDecorationPainter
{
    void Paint(IPageCanvas canvas, PrintTextSpan span);
}

public sealed class TextDecorationPainter : ITextDecorationPainter
{
    private const double UnderlineDrop = 0.12;
    private const double DoubleUnderlineFirst = 0.08;
    private const double DoubleUnderlineSecond = 0.2;
    private const double StrikeRise = 0.28;
    private const double Thickness = 0.06;
    private const double ThinnestPoints = 0.4;

    public void Paint(IPageCanvas canvas, PrintTextSpan span)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(span.Font);
        if (!span.Font.IsDecorated) return;
        var size = span.Font.SizePoints;
        if (!(size > 0) || !double.IsFinite(size) || !(span.Width > 0) || !double.IsFinite(span.Width)) return;
        if (!double.IsFinite(span.X) || !double.IsFinite(span.Baseline)) return;

        var thickness = Math.Max(ThinnestPoints, size * Thickness);
        switch (span.Font.Underline)
        {
            case PrintUnderline.Single:
                Line(canvas, span, size * UnderlineDrop, thickness);
                break;
            case PrintUnderline.Double:
                Line(canvas, span, size * DoubleUnderlineFirst, thickness * 0.75);
                Line(canvas, span, size * DoubleUnderlineSecond, thickness * 0.75);
                break;
        }

        if (span.Font.Strikethrough) Line(canvas, span, -size * StrikeRise, thickness);
    }

    private void Line(IPageCanvas canvas, PrintTextSpan span, double below, double thickness)
    {
        var radians = double.IsFinite(span.RotationDegrees) ? span.RotationDegrees * Math.PI / 180 : 0;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        canvas.DrawLine(Along(span, 0, below, cos, sin), Along(span, span.Width, below, cos, sin), span.Color, Twip.FromPoints(thickness));
    }

    private DocumentPoint Along(PrintTextSpan span, double distance, double below, double cos, double sin) =>
        new(Twip.FromPoints(span.X + distance * cos + below * sin), Twip.FromPoints(span.Baseline - distance * sin + below * cos));
}
