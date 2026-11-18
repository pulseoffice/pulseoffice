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

    public ChartAxisScale ValueAxisScale { get; init; } = ChartAxisScale.Automatic;

    public ChartTrendline? Trendline { get; init; }

    public ChartErrorBars? ErrorBars { get; init; }

    public int SeriesCount { get; init; }
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
            ValueAxisScale = chart.ValueAxis,
            Trendline = chart.SeriesFormat(0).Trendline,
            ErrorBars = chart.SeriesFormat(0).ErrorBars,
        };
    }

    public ChartDefinition Apply(ChartDefinition chart, ChartSettings settings)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(settings);

        var formatted = Analysed(chart, settings);
        return formatted with
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
            ValueAxis = settings.ValueAxisScale.IsAutomatic ? ChartAxisScale.Automatic : settings.ValueAxisScale,
        };
    }

    private ChartDefinition Analysed(ChartDefinition chart, ChartSettings settings)
    {
        var first = chart.SeriesFormat(0);
        var trendChanged = settings.Trendline != first.Trendline;
        var errorsChanged = settings.ErrorBars != first.ErrorBars && settings.ErrorBars?.Kind != ErrorBarKind.Custom;
        if (!trendChanged && !errorsChanged) return chart;
        var count = Math.Max(settings.SeriesCount, chart.SeriesFormats?.Count ?? 0);
        var result = chart;
        for (var series = 0; series < Math.Max(count, 1); series++)
        {
            var format = result.SeriesFormat(series);
            if (trendChanged) format = format with { Trendline = settings.Trendline };
            if (errorsChanged) format = format with { ErrorBars = settings.ErrorBars };
            result = result.WithSeriesFormat(series, format);
        }
        return result;
    }

    private string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
