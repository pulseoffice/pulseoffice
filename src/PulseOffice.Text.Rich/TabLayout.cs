namespace PulseOffice.Text.Rich;

public readonly record struct TabPiece(int Start, int End, double X);

public interface ITabLayout
{
    IReadOnlyList<TabPiece> Place(string content, int start, int end, double origin, double lineStart, IReadOnlyList<TabStop>? stops, double interval, Func<int, int, double> width);
}

public sealed class TabLayout : ITabLayout
{
    public const double DefaultIntervalPoints = 72 * 1.25 / 2.54;

    public IReadOnlyList<TabPiece> Place(string content, int start, int end, double origin, double lineStart, IReadOnlyList<TabStop>? stops, double interval, Func<int, int, double> width)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(width);

        var pieces = new List<TabPiece>();
        var x = lineStart;
        var from = start;
        TabStop? pending = null;
        double pendingAt = 0;
        for (var index = start; index <= end; index++)
        {
            if (index < end && content[index] != '\t') continue;
            var pieceWidth = index > from ? width(from, index) : 0;
            var left = pending is null ? x : Aligned(content, from, index, pending, pendingAt, x, pieceWidth, width);
            if (index > from || index == end && from > start) pieces.Add(new TabPiece(from, index, left));
            x = left + pieceWidth;
            if (index == end) break;
            (pending, pendingAt) = Next(x, origin, stops, interval);
            if (pending.Alignment == TabAlignment.Left) x = Math.Max(x, pendingAt);
            from = index + 1;
        }
        return pieces;
    }

    private (TabStop Stop, double At) Next(double x, double origin, IReadOnlyList<TabStop>? stops, double interval)
    {
        if (stops is not null)
            foreach (var stop in stops.OrderBy(stop => stop.Position))
                if (origin + stop.Position > x + 0.01) return (stop, origin + stop.Position);
        var step = Math.Max(1, interval);
        var count = Math.Floor((x - origin) / step + 0.0001) + 1;
        return (new TabStop(count * step), origin + count * step);
    }

    private double Aligned(string content, int from, int to, TabStop stop, double at, double x, double pieceWidth, Func<int, int, double> width)
    {
        var placed = stop.Alignment switch
        {
            TabAlignment.Right => at - pieceWidth,
            TabAlignment.Center => at - pieceWidth / 2,
            TabAlignment.Decimal => at - Before(content, from, to, stop.DecimalCharacter, pieceWidth, width),
            _ => at,
        };
        return Math.Max(x, placed);
    }

    private double Before(string content, int from, int to, char separator, double pieceWidth, Func<int, int, double> width)
    {
        var mark = content.IndexOf(separator, from, to - from);
        return mark < 0 ? pieceWidth : mark > from ? width(from, mark) : 0;
    }
}
