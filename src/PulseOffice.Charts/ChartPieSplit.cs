namespace PulseOffice.Charts;

public enum PieSplitKind { Automatic, Position, Value, Percent, Custom }

public sealed record ChartPieSplit
{
    public const int DefaultSecondPlotSize = 75;

    public const int MinimumSecondPlotSize = 5;

    public const int MaximumSecondPlotSize = 200;

    public static readonly ChartPieSplit Automatic = new();

    public PieSplitKind Kind { get; init; }

    public double Threshold { get; init; }

    public IReadOnlyList<int> CustomPoints { get; init; } = [];

    public int SecondPlotSize { get; init; } = DefaultSecondPlotSize;

    public bool IsAutomatic => Kind == PieSplitKind.Automatic && SecondPlotSize == DefaultSecondPlotSize;

    public bool Equals(ChartPieSplit? other) =>
        other is not null && Kind == other.Kind && Threshold.Equals(other.Threshold) && SecondPlotSize == other.SecondPlotSize
        && CustomPoints.SequenceEqual(other.CustomPoints);

    public override int GetHashCode() => HashCode.Combine(Kind, Threshold, SecondPlotSize, CustomPoints.Count);
}

public interface IPieSplitter
{
    IReadOnlyList<int> SecondPlot(ChartPieSplit split, IReadOnlyList<int> plotted, IReadOnlyList<double> values);
}

public sealed class PieSplitter : IPieSplitter
{
    public IReadOnlyList<int> SecondPlot(ChartPieSplit split, IReadOnlyList<int> plotted, IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(plotted);
        ArgumentNullException.ThrowIfNull(values);
        if (plotted.Count < 2) return [];
        var total = plotted.Sum(index => Math.Abs(values[index]));
        List<int> second = split.Kind switch
        {
            PieSplitKind.Position => [.. plotted.Skip(plotted.Count - Math.Clamp((int)Math.Round(split.Threshold), 0, plotted.Count))],
            PieSplitKind.Value => [.. plotted.Where(index => values[index] < split.Threshold)],
            PieSplitKind.Percent => [.. plotted.Where(index => total > 0 && Math.Abs(values[index]) / total * 100 < split.Threshold)],
            PieSplitKind.Custom => [.. plotted.Where(split.CustomPoints.Contains)],
            _ => [.. plotted.Skip(plotted.Count - Math.Clamp((plotted.Count + 2) / 3, 1, plotted.Count - 1))],
        };
        return second.Count == 0 || second.Count == plotted.Count ? [] : second;
    }
}
