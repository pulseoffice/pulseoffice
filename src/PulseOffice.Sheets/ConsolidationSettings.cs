using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum ConsolidationFunction
{
    Average = 1, CountNumbers = 2, Count = 3, Max = 4, Min = 5,
    Product = 6, StandardDeviation = 7, StandardDeviationPopulation = 8,
    Sum = 9, Variance = 10, VariancePopulation = 11,
}

public sealed record ConsolidationArea(int SheetIndex, CellRange Range);

public sealed record ConsolidationSettings
{
    public IReadOnlyList<ConsolidationArea> Sources { get; init; } = [];

    public int TargetSheet { get; init; }

    public CellAddress Target { get; init; }

    public ConsolidationFunction Function { get; init; } = ConsolidationFunction.Sum;

    public bool TopRowLabels { get; init; }

    public bool LeftColumnLabels { get; init; }

    public bool LinkToSource { get; init; }

    public ConsolidationSettings? Reindexed(Func<int, int> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var target = map(TargetSheet);
        if (target < 0) return null;
        var sources = Sources
            .Select(area => map(area.SheetIndex) is var index and >= 0 ? area with { SheetIndex = index } : null)
            .OfType<ConsolidationArea>()
            .ToList();
        return this with { TargetSheet = target, Sources = sources };
    }

    public bool Equivalent(ConsolidationSettings? other) =>
        other is not null && Sources.SequenceEqual(other.Sources) && this with { Sources = other.Sources } == other;
}
