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

    public IReadOnlyList<IReadOnlyList<string>>? CategoryLevels { get; init; }

    public bool HasCategoryLevels => CategoryLevels is { Count: > 0 };

    public IReadOnlyList<ChartHiddenColumn>? HiddenColumns { get; init; }

    public IReadOnlyList<string>? MissingTables { get; init; }

    public bool LostTables => MissingTables is { Count: > 0 };

    public ChartData WithCategoryLevels(IReadOnlyList<IReadOnlyList<string>>? levels) =>
        levels is { Count: > 0 } && levels.All(level => level.Count == Categories.Count)
            ? this with { CategoryLevels = levels }
            : this with { CategoryLevels = null };

    public ChartHiddenColumn? HiddenColumn(int column) => HiddenColumns?.FirstOrDefault(hidden => hidden.Column == column);

    public bool IsEmpty => Series.Count == 0 || Series.All(s => s.Values.Count == 0);

    public ChartData WithoutGaps() =>
        Series.All(series => series.Values.All(double.IsFinite))
            ? this
            : this with { Series = [.. Series.Select(series => series with { Values = [.. series.Values.Select(value => double.IsFinite(value) ? value : 0)] })] };

    public double Minimum => IsEmpty ? 0 : Series.SelectMany(s => s.Values).Where(double.IsFinite).DefaultIfEmpty(0).Min();
    public double Maximum => IsEmpty ? 0 : Series.SelectMany(s => s.Values).Where(double.IsFinite).DefaultIfEmpty(0).Max();
}

public sealed record ChartHiddenColumn(int Column, IReadOnlyList<double> Values);

public sealed record ChartSeriesSource(string? Name, string? Values, string? Categories);

public sealed record ChartPivotSource(string PivotName)
{
    public int FormatId { get; init; }

    public bool ShowFieldButtons { get; init; } = true;
}

public enum LegendPosition { None, Right, Bottom, Top, Left }

public sealed record ChartDefinition : EmbeddedObject
{
    public required ChartType Type { get; init; }

    public required string SourceSheet { get; init; }
    public required string SourceRange { get; init; }

    public ChartData? InlineData { get; init; }

    public IReadOnlyList<ChartSeriesSource>? SeriesSources { get; init; }

    public ChartPivotSource? PivotSource { get; init; }

    public IReadOnlyList<IReadOnlyList<string>>? CategoryLevels { get; init; }

    public bool IsPivotChart => PivotSource is not null;

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

    public ChartDataLabelOptions DataLabels { get; init; } = ChartDataLabelOptions.ValuesOnly;

    public ChartAxisScale CategoryAxis { get; init; } = ChartAxisScale.Automatic;

    public ChartAxisScale ValueAxis { get; init; } = ChartAxisScale.Automatic;

    public ChartAxisScale SecondaryValueAxis { get; init; } = ChartAxisScale.Automatic;

    public IReadOnlyList<ChartSeriesFormat?>? SeriesFormats { get; init; }

    public ChartSeriesFormat SeriesFormat(int series) =>
        SeriesFormats is { } formats && series >= 0 && series < formats.Count && formats[series] is { } format ? format : ChartSeriesFormat.Plain;

    public bool HasSecondaryAxis => SeriesFormats?.Any(format => format?.SecondaryAxis == true) == true;

    public ChartDefinition WithSeriesFormat(int series, ChartSeriesFormat format)
    {
        var formats = new List<ChartSeriesFormat?>(SeriesFormats ?? []);
        while (formats.Count <= series) formats.Add(null);
        formats[series] = format.IsPlain ? null : format;
        return this with { SeriesFormats = formats.All(item => item is null) ? null : formats };
    }

    public ChartPointFormat PointFormat(int series, int point) => SeriesFormat(series).Point(point);

    public ChartDefinition WithPointFormat(int series, ChartPointFormat point) =>
        WithSeriesFormat(series, SeriesFormat(series).WithPoint(point));

    public string PointColor(int series, int point, string automatic) => PointFormat(series, point).ColorHex ?? automatic;

    public bool ThreeDimensional { get; init; }

    public int LineSeriesCount { get; init; } = 1;

    public IReadOnlyList<int>? LineSeriesIndices { get; init; }

    public bool IsLineSeries(int series, int seriesCount) =>
        Type == ChartType.ColumnLine && (LineSeriesIndices is { Count: > 0 } lines && lines.Any(index => index < seriesCount) && lines.Count(index => index < seriesCount) < seriesCount
            ? lines.Contains(series)
            : series >= seriesCount - Math.Clamp(LineSeriesCount, 1, Math.Max(seriesCount - 1, 1)));

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
