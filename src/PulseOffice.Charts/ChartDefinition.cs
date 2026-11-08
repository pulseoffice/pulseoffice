using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Charts;

public sealed record ChartSeries(string Name, IReadOnlyList<double> Values)
{
    public string? ColorHex { get; init; }
}

public sealed record ChartData(IReadOnlyList<string> Categories, IReadOnlyList<ChartSeries> Series)
{
    public static readonly ChartData Empty = new([], []);

    public bool OrdinalCategories { get; init; }

    public bool IsEmpty => Series.Count == 0 || Series.All(s => s.Values.Count == 0);

    public ChartData WithoutGaps() =>
        Series.All(series => series.Values.All(double.IsFinite))
            ? this
            : this with { Series = [.. Series.Select(series => series with { Values = [.. series.Values.Select(value => double.IsFinite(value) ? value : 0)] })] };

    public double Minimum => IsEmpty ? 0 : Series.SelectMany(s => s.Values).Where(double.IsFinite).DefaultIfEmpty(0).Min();
    public double Maximum => IsEmpty ? 0 : Series.SelectMany(s => s.Values).Where(double.IsFinite).DefaultIfEmpty(0).Max();
}

public sealed record ChartSeriesSource(string? Name, string? Values, string? Categories);

public enum LegendPosition { None, Right, Bottom, Top, Left }

public sealed record ChartDefinition : EmbeddedObject
{
    public required ChartType Type { get; init; }

    public required string SourceSheet { get; init; }
    public required string SourceRange { get; init; }

    public ChartData? InlineData { get; init; }

    public IReadOnlyList<ChartSeriesSource>? SeriesSources { get; init; }

    public bool HasSeriesSources => SeriesSources is { Count: > 0 };

    public bool FirstRowIsHeader { get; init; } = true;

    public bool FirstColumnIsCategory { get; init; } = true;

    public bool SeriesInColumns { get; init; } = true;

    public IReadOnlyList<string>? SeriesNames { get; init; }

    public string? ExplicitSeriesName(int series) =>
        SeriesNames is { } names && series >= 0 && series < names.Count && !string.IsNullOrEmpty(names[series]) ? names[series] : null;

    public IReadOnlyList<string?>? SeriesColors { get; init; }

    public string? ExplicitSeriesColor(int series) =>
        SeriesColors is { } colors && series >= 0 && series < colors.Count && !string.IsNullOrEmpty(colors[series]) ? colors[series] : null;

    public string SeriesColor(int series) => ExplicitSeriesColor(series) ?? ChartPalette.For(series);

    public ChartData Coloured(ChartData data) =>
        SeriesColors is null
            ? data
            : data with { Series = [.. data.Series.Select((series, index) => ExplicitSeriesColor(index) is { } color ? series with { ColorHex = color } : series)] };

    public string? ChartTitle { get; init; }
    public string? CategoryAxisTitle { get; init; }
    public string? ValueAxisTitle { get; init; }

    public LegendPosition Legend { get; init; } = LegendPosition.Right;

    public bool ShowGridLines { get; init; } = true;

    public bool ShowDataLabels { get; init; }

    public bool ThreeDimensional { get; init; }

    public int LineSeriesCount { get; init; } = 1;

    public bool IsLineSeries(int series, int seriesCount) =>
        Type == ChartType.ColumnLine && series >= seriesCount - Math.Clamp(LineSeriesCount, 1, Math.Max(seriesCount - 1, 1));

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Chart;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
    [ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations];
}

public static class ChartPalette
{
    private static readonly string[] Colors =
    [
        "#2563EB", "#EA580C", "#16A34A", "#9333EA",
        "#0891B2", "#CA8A04", "#DC2626", "#4B5563",
    ];

    public static string For(int index) => Colors[((index % Colors.Length) + Colors.Length) % Colors.Length];
}
