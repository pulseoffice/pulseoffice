using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Pivot;

public enum PivotArea { Unused, Row, Column, Value, Filter }

public enum PivotAggregation
{
    Sum, Count, CountNumbers, Average, Max, Min, Product,
    StandardDeviation, StandardDeviationPopulation,
    Variance, VariancePopulation,
    Median, First, Last, DistinctCount,
}

public enum ValueDisplayMode
{
    Normal,
    PercentOfTotal, PercentOfRow, PercentOfColumn, PercentOfParent,
    DifferenceFrom, PercentDifferenceFrom,
    RunningTotal, PercentRunningTotal,
    RankAscending, RankDescending,
    Index,
}

public enum DateGrouping { None, Seconds, Minutes, Hours, Days, Months, Quarters, Years }

public sealed record PivotField
{
    public required string SourceName { get; init; }
    public string? DisplayName { get; init; }
    public required int SourceColumnIndex { get; init; }

    public PivotArea Area { get; init; } = PivotArea.Unused;
    public int AreaOrder { get; init; }

    public PivotAggregation Aggregation { get; init; } = PivotAggregation.Sum;
    public ValueDisplayMode DisplayMode { get; init; } = ValueDisplayMode.Normal;

    public DateGrouping DateGrouping { get; init; } = DateGrouping.None;
    public double? NumericGroupInterval { get; init; }

    public bool ShowSubtotals { get; init; } = true;
    public bool ShowItemsWithNoData { get; init; }
    public bool SortDescending { get; init; }

    public IReadOnlyCollection<string> ExcludedValues { get; init; } = [];

    public string? Formula { get; init; }

    public bool IsCalculated => Formula is not null;

    public string Label => DisplayName ?? SourceName;
}

public enum PivotLayout { Compact, Outline, Tabular }

public sealed record PivotDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    public required string SourceSheet { get; init; }
    public required CellRange SourceRange { get; init; }

    public string? SourceQueryBindingId { get; init; }

    public PivotExternalSource? ExternalSource { get; init; }

    public bool IsExternal => ExternalSource is not null;

    public required CellAddress Destination { get; init; }
    public required string DestinationSheet { get; init; }

    public IReadOnlyList<PivotField> Fields { get; init; } = [];

    public PivotLayout Layout { get; init; } = PivotLayout.Compact;
    public bool ShowGrandTotalRows { get; init; } = true;
    public bool ShowGrandTotalColumns { get; init; } = true;
    public bool RepeatItemLabels { get; init; }
    public bool AutoRefreshOnOpen { get; init; }

    public string? EmptyCellText { get; init; }

    public CellRange? OutputRange { get; init; }

    public IEnumerable<PivotField> In(PivotArea area) =>
        Fields.Where(f => f.Area == area).OrderBy(f => f.AreaOrder);

    public bool IsValid => Fields.Any(f => f.Area == PivotArea.Value);
}
