namespace PulseOffice.Charts;

public enum ChartFamily
{
    Column,
    Bar,
    Line,
    Area,
    Pie,
    Scatter,
    Statistical,
    Financial,
    Radar,
    Surface,
    Hierarchy,
    Indicator,
}

public sealed record ChartFamilyInfo(ChartFamily Family, IReadOnlyList<ChartType> Types)
{
    public ChartType Primary => Types[0];
}

public interface IChartFamilyCatalog
{
    IReadOnlyList<ChartFamilyInfo> Families { get; }

    ChartFamily FamilyOf(ChartType type);

    ChartFamilyInfo Find(ChartFamily family);
}

public sealed class ChartFamilyCatalog : IChartFamilyCatalog
{
    private readonly IReadOnlyList<ChartFamilyInfo> _families =
    [
        new(ChartFamily.Column, [ChartType.Column, ChartType.StackedColumn, ChartType.PercentStackedColumn, ChartType.ColumnLine, ChartType.Waterfall, ChartType.Pareto]),
        new(ChartFamily.Bar, [ChartType.Bar, ChartType.StackedBar, ChartType.PercentStackedBar, ChartType.PopulationPyramid, ChartType.Gantt, ChartType.Bullet]),
        new(ChartFamily.Line, [ChartType.Line, ChartType.SmoothLine, ChartType.SteppedLine, ChartType.StackedLine, ChartType.PercentStackedLine, ChartType.RunChart, ChartType.ControlChart]),
        new(ChartFamily.Area, [ChartType.Area, ChartType.StackedArea, ChartType.PercentStackedArea]),
        new(ChartFamily.Pie, [ChartType.Pie, ChartType.Doughnut, ChartType.ExplodedPie, ChartType.PieOfPie, ChartType.BarOfPie]),
        new(ChartFamily.Scatter, [ChartType.Scatter, ChartType.ScatterWithLines, ChartType.Bubble]),
        new(ChartFamily.Statistical,
        [
            ChartType.Histogram, ChartType.BoxPlot, ChartType.ViolinPlot, ChartType.DensityPlot, ChartType.QuantileQuantile,
            ChartType.CumulativeDistribution, ChartType.StemAndLeaf, ChartType.DotPlot, ChartType.BeeswarmPlot,
        ]),
        new(ChartFamily.Financial, [ChartType.StockHighLowClose, ChartType.StockOpenHighLowClose, ChartType.Candlestick]),
        new(ChartFamily.Radar, [ChartType.Radar, ChartType.FilledRadar, ChartType.Polar, ChartType.ParallelCoordinates]),
        new(ChartFamily.Surface, [ChartType.Heatmap, ChartType.Contour, ChartType.Surface, ChartType.Surface3D]),
        new(ChartFamily.Hierarchy, [ChartType.Treemap, ChartType.Sunburst, ChartType.Funnel, ChartType.Sankey, ChartType.Network]),
        new(ChartFamily.Indicator, [ChartType.Gauge]),
    ];

    private readonly Dictionary<ChartType, ChartFamily> _byType;

    public ChartFamilyCatalog()
    {
        _byType = _families.SelectMany(info => info.Types.Select(type => (type, info.Family)))
            .ToDictionary(entry => entry.type, entry => entry.Family);

        var missing = Enum.GetValues<ChartType>().Where(type => !_byType.ContainsKey(type)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Chart types without a family: {string.Join(", ", missing)}");
    }

    public IReadOnlyList<ChartFamilyInfo> Families => _families;

    public ChartFamily FamilyOf(ChartType type) => _byType[type];

    public ChartFamilyInfo Find(ChartFamily family) => _families.First(info => info.Family == family);
}
