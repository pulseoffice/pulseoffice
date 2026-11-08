using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI;

public readonly record struct SparklineBar(int Index, double Top, double Bottom, bool Negative);

public interface ISparklineBars
{
    IReadOnlyList<SparklineBar> Layout(SparklinePoints points, SparklineKind kind, double top, double bottom);
}

public sealed class SparklineBars : ISparklineBars
{
    private const double MinimumHeight = 1;

    public IReadOnlyList<SparklineBar> Layout(SparklinePoints points, SparklineKind kind, double top, double bottom)
    {
        ArgumentNullException.ThrowIfNull(points);
        var bars = new List<SparklineBar>();
        if (bottom <= top) return bars;

        if (kind == SparklineKind.Stacked)
        {
            var middle = (top + bottom) / 2;
            for (var index = 0; index < points.Values.Count; index++)
            {
                if (points.Values[index] is not { } value || value == 0) continue;
                bars.Add(value > 0
                    ? new SparklineBar(index, top, middle, false)
                    : new SparklineBar(index, middle, bottom, true));
            }
            return bars;
        }

        var span = Math.Max(1e-9, points.Maximum - points.Minimum);
        double Y(double value) => bottom - (value - points.Minimum) / span * (bottom - top);

        var baseline = Y(Math.Clamp(0, points.Minimum, points.Maximum));

        for (var index = 0; index < points.Values.Count; index++)
        {
            if (points.Values[index] is not { } value) continue;

            var y = Y(value);
            var upper = Math.Min(y, baseline);
            var lower = Math.Max(y, baseline);
            if (lower - upper < MinimumHeight) lower = upper + MinimumHeight;

            bars.Add(new SparklineBar(index, upper, lower, value < 0));
        }

        return bars;
    }
}
