using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum SparklineKind { Line, Column, Stacked }

public enum SparklineEmptyCells { Gap, Zero, Span }

public enum SparklineAxisBound { Individual, Group, Custom }

public sealed record SparklineStyle
{
    public const double ThinnestLine = 0.01;

    public const double WidestLine = 100;

    public string ColorHex { get; init; } = "#2563EB";

    public string NegativeHex { get; init; } = "#DC2626";

    public string MarkerHex { get; init; } = "#0F172A";

    public bool ShowHighPoint { get; init; }

    public bool ShowLowPoint { get; init; }

    public bool ShowFirstPoint { get; init; }

    public bool ShowLastPoint { get; init; }

    public bool ShowNegative { get; init; } = true;

    public double LineWidth { get; init; } = 1.4;

    public bool ShowMarkers { get; init; }

    public bool ShowAxis { get; init; }

    public string AxisHex { get; init; } = "#000000";

    public string? FirstHex { get; init; }

    public string? LastHex { get; init; }

    public string? HighHex { get; init; }

    public string? LowHex { get; init; }
}

public sealed record Sparkline(CellAddress Cell, CellRange Source, string? SourceSheet = null)
{
    public string SourceSheetOr(string host) => SourceSheet ?? host;
}

public sealed record SparklineGroup
{
    public required string Id { get; init; }

    public required IReadOnlyList<Sparkline> Sparklines { get; init; }

    public SparklineKind Kind { get; init; } = SparklineKind.Line;

    public SparklineStyle Style { get; init; } = new();

    public bool SameScale
    {
        get => MinimumAxis == SparklineAxisBound.Group && MaximumAxis == SparklineAxisBound.Group;
        init
        {
            MinimumAxis = value ? SparklineAxisBound.Group : SparklineAxisBound.Individual;
            MaximumAxis = MinimumAxis;
        }
    }

    public SparklineAxisBound MinimumAxis { get; init; }

    public SparklineAxisBound MaximumAxis { get; init; }

    public double? ManualMinimum { get; init; }

    public double? ManualMaximum { get; init; }

    public SparklineEmptyCells EmptyCells { get; init; }

    public bool ShowHidden { get; init; }

    public bool RightToLeft { get; init; }

    public CellRange? DateRange { get; init; }

    public string? DateSheet { get; init; }

    public bool Contains(CellAddress address) =>
        Sparklines.Any(sparkline => sparkline.Cell == address);
}

public sealed class SparklineCollection
{
    private readonly List<SparklineGroup> _groups = [];

    public IReadOnlyList<SparklineGroup> Groups => _groups;

    public int Count => _groups.Count;

    public void Add(SparklineGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RemoveAt(group.Sparklines.Select(sparkline => sparkline.Cell));
        _groups.Add(group);
    }

    public void AddRange(IEnumerable<SparklineGroup> groups)
    {
        foreach (var group in groups) Add(group);
    }

    public (SparklineGroup Group, Sparkline Sparkline)? At(CellAddress address)
    {
        foreach (var group in _groups)
            foreach (var sparkline in group.Sparklines)
                if (sparkline.Cell == address)
                    return (group, sparkline);

        return null;
    }

    public void RemoveWithin(CellRange range) =>
        RemoveAt([.. _groups.SelectMany(group => group.Sparklines).Select(sparkline => sparkline.Cell).Where(range.Contains)]);

    public void RemoveAt(IEnumerable<CellAddress> cells)
    {
        var targets = cells.ToHashSet();

        for (var index = _groups.Count - 1; index >= 0; index--)
        {
            var remaining = _groups[index].Sparklines
                .Where(sparkline => !targets.Contains(sparkline.Cell))
                .ToList();

            if (remaining.Count == 0) _groups.RemoveAt(index);
            else if (remaining.Count != _groups[index].Sparklines.Count)
                _groups[index] = _groups[index] with { Sparklines = remaining };
        }
    }

    public void Clear() => _groups.Clear();
}
