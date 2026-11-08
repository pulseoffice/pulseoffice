namespace PulseOffice.Charts;

public sealed record ChartSettings
{
    public required ChartType Type { get; init; }

    public string? Title { get; init; }

    public LegendPosition Legend { get; init; } = LegendPosition.Right;

    public bool FirstRowIsHeader { get; init; } = true;

    public bool SeriesInColumns { get; init; } = true;

    public string? CategoryAxisTitle { get; init; }

    public string? ValueAxisTitle { get; init; }

    public bool ShowDataLabels { get; init; }

    public bool ShowGridLines { get; init; } = true;

    public string? SourceRange { get; init; }

    public IReadOnlyList<ChartSeriesSource>? SeriesSources { get; init; }
}

public interface IChartSettingsMapper
{
    ChartSettings From(ChartDefinition chart);

    ChartDefinition Apply(ChartDefinition chart, ChartSettings settings);
}

public sealed class ChartSettingsMapper : IChartSettingsMapper
{
    public ChartSettings From(ChartDefinition chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        return new ChartSettings
        {
            Type = chart.Type,
            Title = chart.ChartTitle,
            Legend = chart.Legend,
            FirstRowIsHeader = chart.FirstRowIsHeader,
            SeriesInColumns = chart.SeriesInColumns,
            CategoryAxisTitle = chart.CategoryAxisTitle,
            ValueAxisTitle = chart.ValueAxisTitle,
            ShowDataLabels = chart.ShowDataLabels,
            ShowGridLines = chart.ShowGridLines,
            SourceRange = string.IsNullOrEmpty(chart.SourceRange) ? null : chart.SourceRange,
            SeriesSources = chart.SeriesSources,
        };
    }

    public ChartDefinition Apply(ChartDefinition chart, ChartSettings settings)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(settings);

        return chart with
        {
            Type = settings.Type,
            ChartTitle = Clean(settings.Title),
            Legend = settings.Legend,
            FirstRowIsHeader = settings.FirstRowIsHeader,
            SeriesInColumns = settings.SeriesInColumns,
            CategoryAxisTitle = Clean(settings.CategoryAxisTitle),
            ValueAxisTitle = Clean(settings.ValueAxisTitle),
            ShowDataLabels = settings.ShowDataLabels,
            ShowGridLines = settings.ShowGridLines,
            SourceRange = Clean(settings.SourceRange) ?? chart.SourceRange,
            SeriesSources = settings.SeriesSources is { Count: > 0 } sources ? sources : null,
        };
    }

    private string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
