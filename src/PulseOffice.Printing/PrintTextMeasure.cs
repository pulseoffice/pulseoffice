using PulseOffice.Foundation.Units;

namespace PulseOffice.Printing;

public interface IPrintTextMeasure
{
    Twip Measure(string text, PrintFont font);
}

public sealed class CanvasTextMeasure(IPageCanvas canvas) : IPrintTextMeasure
{
    public Twip Measure(string text, PrintFont font) => canvas.MeasureText(text, font);
}

public sealed class AverageAdvanceTextMeasure : IPrintTextMeasure
{
    private const double AverageAdvance = 0.55;

    public Twip Measure(string text, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        return Twip.FromPoints(text.Length * font.SizePoints * AverageAdvance);
    }
}
