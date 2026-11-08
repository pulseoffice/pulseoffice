namespace PulseOffice.UI.Commands;

public interface IRibbonStripLayout
{
    IReadOnlyList<int> Lines(IReadOnlyList<double> widths, int maximumLines);
}

public sealed class RibbonStripLayout : IRibbonStripLayout
{
    public IReadOnlyList<int> Lines(IReadOnlyList<double> widths, int maximumLines)
    {
        ArgumentNullException.ThrowIfNull(widths);

        if (widths.Count == 0) return [];

        var lines = Math.Clamp(maximumLines, 1, widths.Count);
        var target = widths.Sum() / lines;

        var counts = new List<int>();
        var current = 0;
        var width = 0.0;

        for (var index = 0; index < widths.Count; index++)
        {
            var remainingItems = widths.Count - index;
            var remainingLines = lines - counts.Count;

            var mustBreak = current > 0 && remainingItems < remainingLines;
            var wouldOverflow = current > 0 && width + widths[index] > target + 0.5 && remainingLines > 1;

            if (mustBreak || wouldOverflow)
            {
                counts.Add(current);
                current = 0;
                width = 0;
            }

            current++;
            width += widths[index];
        }

        counts.Add(current);
        return counts;
    }
}
