namespace PulseOffice.Charts;

public enum ChartQuartileMethod { Exclusive, Inclusive }

public enum ChartParentLabels { Overlapping, Banner, None }

public sealed record ChartStatisticalLayout
{
    public static readonly ChartStatisticalLayout Automatic = new();

    public int? BinCount { get; init; }

    public double? BinWidth { get; init; }

    public double? Underflow { get; init; }

    public double? Overflow { get; init; }

    public bool IntervalClosedRight { get; init; } = true;

    public ChartQuartileMethod QuartileMethod { get; init; }

    public bool MeanMarker { get; init; } = true;

    public bool MeanLine { get; init; }

    public bool InnerPoints { get; init; }

    public bool Outliers { get; init; } = true;

    public IReadOnlyList<int> Subtotals { get; init; } = [];

    public ChartParentLabels ParentLabels { get; init; }

    public bool HasBinning => BinCount is not null || BinWidth is not null || Underflow is not null || Overflow is not null || !IntervalClosedRight;

    public bool IsAutomatic => Equals(Automatic);

    public bool Equals(ChartStatisticalLayout? other) =>
        other is not null && BinCount == other.BinCount && Nullable.Equals(BinWidth, other.BinWidth)
        && Nullable.Equals(Underflow, other.Underflow) && Nullable.Equals(Overflow, other.Overflow)
        && IntervalClosedRight == other.IntervalClosedRight && QuartileMethod == other.QuartileMethod
        && MeanMarker == other.MeanMarker && MeanLine == other.MeanLine && InnerPoints == other.InnerPoints
        && Outliers == other.Outliers && ParentLabels == other.ParentLabels && Subtotals.SequenceEqual(other.Subtotals);

    public override int GetHashCode() =>
        HashCode.Combine(BinCount, BinWidth, QuartileMethod, MeanMarker, Outliers, ParentLabels, Subtotals.Count);
}

public sealed record ChartStyleReference
{
    public static readonly ChartStyleReference None = new();

    public int? StyleId { get; init; }

    public string? ColorStyle { get; init; }

    public string? ChartStyle { get; init; }

    public bool IsEmpty => StyleId is null && ColorStyle is null && ChartStyle is null;
}
