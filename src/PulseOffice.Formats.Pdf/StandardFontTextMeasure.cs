using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public sealed class StandardFontTextMeasure(IPdfFontMetrics metrics) : IPrintTextMeasure
{
    public Twip Measure(string text, PrintFont font)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        return Twip.FromPoints(metrics.WidthOf(text, metrics.Resolve(font.Family, font.Bold, font.Italic), font.SizePoints));
    }
}
