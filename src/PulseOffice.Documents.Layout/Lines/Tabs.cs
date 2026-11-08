using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Lines;

public readonly record struct ResolvedTab(double Width, TabLeader Leader);

public interface ITabResolver
{
    ResolvedTab Resolve(ShapedParagraph paragraph, ParagraphFrame frame, int index, double x, double available);

    double Next(ParagraphFrame frame, double x);
}

public sealed class TabResolver : ITabResolver
{
    private const double Epsilon = 0.01;

    public ResolvedTab Resolve(ShapedParagraph paragraph, ParagraphFrame frame, int index, double x, double available)
    {
        var stop = Stops(frame).Where(tab => tab.Position > x + Epsilon).MinBy(tab => tab.Position)
            ?? new TabStop(DefaultPosition(frame, x));
        var position = stop.Alignment == TabAlignment.Left ? stop.Position : Math.Min(stop.Position, Math.Max(x, available));
        var width = stop.Alignment switch
        {
            TabAlignment.Right => position - x - Segment(paragraph, index, null),
            TabAlignment.Center => position - x - Segment(paragraph, index, null) / 2,
            TabAlignment.Decimal => position - x - Segment(paragraph, index, stop.DecimalCharacter),
            _ => position - x,
        };
        return new ResolvedTab(Math.Max(0, width), stop.Leader);
    }

    public double Next(ParagraphFrame frame, double x) =>
        Stops(frame).Where(tab => tab.Position > x + Epsilon).Select(tab => tab.Position).DefaultIfEmpty(DefaultPosition(frame, x)).Min();

    private IEnumerable<TabStop> Stops(ParagraphFrame frame) =>
        frame.TabOrigin == 0 ? frame.Tabs : frame.Tabs.Select(tab => tab with { Position = tab.Position + frame.TabOrigin });

    private double DefaultPosition(ParagraphFrame frame, double x)
    {
        var step = frame.TabInterval > 0 ? frame.TabInterval : 36;
        return (Math.Floor((x - frame.TabOrigin + Epsilon) / step) + 1) * step + frame.TabOrigin;
    }

    private double Segment(ShapedParagraph paragraph, int index, char? stopAt)
    {
        var width = 0.0;
        for (var i = index + 1; i < paragraph.Text.Length; i++)
        {
            var character = paragraph.Text[i];
            if (character is '\t' or '\n' or '\u2028') break;
            if (stopAt is { } decimalMark && character == decimalMark) break;
            width += paragraph.Advances[i];
        }
        return width;
    }
}
