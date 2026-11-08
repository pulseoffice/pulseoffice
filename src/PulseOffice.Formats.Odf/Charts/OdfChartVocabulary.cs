using PulseOffice.Charts;

namespace PulseOffice.Formats.Odf;

internal readonly record struct ChartPlotFlags(bool Horizontal, bool Stacked, bool Percent);

internal readonly record struct OdfChartTraits(string? Interpolation, bool Candlestick, int SeriesCount, bool Exploded, bool ScatterLines);

internal sealed class OdfChartVocabulary
{
    private readonly IReadOnlyList<(string Class, ChartType[] Types)> _classes =
    [
        ("chart:bar", [ChartType.Column, ChartType.ColumnLine, ChartType.Bar, ChartType.StackedBar, ChartType.PercentStackedBar,
            ChartType.StackedColumn, ChartType.PercentStackedColumn]),
        ("chart:line", [ChartType.Line, ChartType.SmoothLine, ChartType.SteppedLine, ChartType.StackedLine, ChartType.PercentStackedLine]),
        ("chart:area", [ChartType.Area, ChartType.StackedArea, ChartType.PercentStackedArea]),
        ("chart:circle", [ChartType.Pie, ChartType.ExplodedPie]),
        ("chart:ring", [ChartType.Doughnut]),
        ("chart:scatter", [ChartType.Scatter, ChartType.ScatterWithLines]),
        ("chart:bubble", [ChartType.Bubble]),
        ("chart:radar", [ChartType.Radar]),
        ("chart:filled-radar", [ChartType.FilledRadar]),
        ("chart:stock", [ChartType.StockHighLowClose, ChartType.StockOpenHighLowClose, ChartType.Candlestick]),
    ];

    private readonly IReadOnlyList<(LegendPosition Position, string Name)> _legends =
    [
        (LegendPosition.Bottom, "bottom"),
        (LegendPosition.Top, "top"),
        (LegendPosition.Left, "start"),
        (LegendPosition.Right, "end"),
    ];

    public string ClassOf(ChartType type) =>
        _classes.FirstOrDefault(entry => entry.Types.Contains(type)).Class ?? "chart:bar";

    public ChartPlotFlags FlagsOf(ChartType type) => new(
        type is ChartType.Bar or ChartType.StackedBar or ChartType.PercentStackedBar,
        type is ChartType.StackedColumn or ChartType.StackedBar or ChartType.StackedLine or ChartType.StackedArea,
        type is ChartType.PercentStackedColumn or ChartType.PercentStackedBar or ChartType.PercentStackedLine or ChartType.PercentStackedArea);

    public bool IsCircular(ChartType type) => type is ChartType.Pie or ChartType.ExplodedPie or ChartType.Doughnut;

    public bool IsStandard(ChartType type) => _classes.Any(entry => entry.Types.Contains(type));

    public string? InterpolationOf(ChartType type) => type switch
    {
        ChartType.SmoothLine => "cubic-spline",
        ChartType.SteppedLine => "step-start",
        _ => null,
    };

    public bool IsCandlestick(ChartType type) => type is ChartType.Candlestick or ChartType.StockOpenHighLowClose;

    public bool KeptOnlyByExtension(ChartType type) => !IsStandard(type) || type == ChartType.StockOpenHighLowClose;

    public int PieOffsetOf(ChartType type) => type == ChartType.ExplodedPie ? ExplodedPieOffset : 0;

    public bool? ScatterLinesOf(ChartType type) => type switch
    {
        ChartType.Scatter => false,
        ChartType.ScatterWithLines => true,
        _ => null,
    };

    public ChartType Declared(string? declared, string? chartClass, ChartType read) =>
        Enum.TryParse<ChartType>(declared, out var type) && Enum.IsDefined(type) && ClassOf(type) == (chartClass ?? "chart:bar")
        && (IsStandard(type) || read == ChartType.Column)
            ? type
            : read;

    public ChartType Refine(ChartType read, OdfChartTraits traits) => read switch
    {
        ChartType.Line when traits.Interpolation is "cubic-spline" or "b-spline" => ChartType.SmoothLine,
        ChartType.Line when traits.Interpolation is { } step && step.StartsWith("step-", StringComparison.Ordinal) => ChartType.SteppedLine,
        ChartType.StockHighLowClose when traits.Candlestick => ChartType.Candlestick,
        ChartType.Pie when traits.Exploded => ChartType.ExplodedPie,
        ChartType.Scatter when traits.ScatterLines => ChartType.ScatterWithLines,
        _ => read,
    };

    private const int ExplodedPieOffset = 10;

    public ChartType TypeOf(string? chartClass, ChartPlotFlags flags) => chartClass switch
    {
        "chart:bar" => (flags.Horizontal, flags.Percent, flags.Stacked) switch
        {
            (true, true, _) => ChartType.PercentStackedBar,
            (true, _, true) => ChartType.StackedBar,
            (true, _, _) => ChartType.Bar,
            (_, true, _) => ChartType.PercentStackedColumn,
            (_, _, true) => ChartType.StackedColumn,
            _ => ChartType.Column,
        },
        "chart:line" => flags.Percent ? ChartType.PercentStackedLine : flags.Stacked ? ChartType.StackedLine : ChartType.Line,
        "chart:area" => flags.Percent ? ChartType.PercentStackedArea : flags.Stacked ? ChartType.StackedArea : ChartType.Area,
        "chart:circle" => ChartType.Pie,
        "chart:ring" => ChartType.Doughnut,
        "chart:scatter" => ChartType.Scatter,
        "chart:bubble" => ChartType.Bubble,
        "chart:radar" => ChartType.Radar,
        "chart:filled-radar" => ChartType.FilledRadar,
        "chart:stock" => ChartType.StockHighLowClose,
        _ => ChartType.Column,
    };

    public string LegendName(LegendPosition position) =>
        _legends.FirstOrDefault(entry => entry.Position == position).Name ?? "end";

    public LegendPosition LegendOf(string? name) =>
        name is null ? LegendPosition.None
        : _legends.FirstOrDefault(entry => entry.Name == name) is { Name: not null } found ? found.Position
        : LegendPosition.Right;
}
