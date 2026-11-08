using PulseOffice.Charts;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Charts;

public enum ChartPickerMode { Insert, Edit }

public sealed record ChartPickerRequest
{
    public required Func<ChartSettings, ChartData> Data { get; init; }

    public required ChartSettings Settings { get; init; }

    public ChartPickerMode Mode { get; init; } = ChartPickerMode.Insert;

    public required System.Globalization.CultureInfo Culture { get; init; }

    public bool HeaderEditable { get; init; } = true;

    public bool SeriesOrientationEditable { get; init; } = true;

    public Func<string, bool>? RangeValidator { get; init; }

    public PulseOffice.UI.Controls.References.IReferencePicker? RangePicker { get; init; }

    public ChartSeriesRangeEditing? SeriesRanges { get; init; }

    public bool SuggestsTitle => Mode == ChartPickerMode.Insert && string.IsNullOrEmpty(Settings.Title);
}

public interface IChartPicker
{
    Task<ChartSettings?> ChooseAsync(ChartPickerRequest request, CancellationToken ct = default);
}

public interface IChartSampleData
{
    ChartData For(ChartFamily family);
}

public sealed class ChartSampleData(ITextCatalog texts) : IChartSampleData
{
    private static readonly double[][] Values =
    [
        [10, 12, 11, 14, 13, 16],
        [14, 15, 14, 17, 16, 19],
        [8, 10, 9, 11, 11, 13],
        [12, 11, 13, 15, 14, 17],
    ];

    public ChartData For(ChartFamily family)
    {
        var categories = Categories(family);
        var count = family switch
        {
            ChartFamily.Pie or ChartFamily.Indicator or ChartFamily.Statistical => 1,
            ChartFamily.Financial => 4,
            ChartFamily.Hierarchy => 1,
            _ => 3,
        };

        return new ChartData(categories,
        [
            .. Values.Take(count).Select((values, index) =>
                new ChartSeries(texts.Format("chart.series.fallback", index + 1), values) { ColorHex = ChartPalette.For(index) }),
        ]);
    }

    private IReadOnlyList<string> Categories(ChartFamily family) =>
        family == ChartFamily.Scatter ? ["1", "2", "3", "4", "5", "6"] : ["A", "B", "C", "D", "E", "F"];
}

public sealed class ChartPicker(
    IDialogService dialogs,
    IChartFamilyCatalog families,
    IChartRecommender recommender,
    IChartSampleData samples,
    ChartRenderer renderer,
    IChartSettingsMapper mapper,
    PulseOffice.UI.Icons.IIconCatalog icons,
    ITextCatalog texts) : IChartPicker
{
    public Task<ChartSettings?> ChooseAsync(ChartPickerRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var view = new ChartPickerView(request,
            new ChartPickerServices(families, recommender, samples, renderer, mapper, icons, texts));

        var titleKey = request.Mode == ChartPickerMode.Edit ? "dialog.chartEdit.title" : "dialog.chartGallery.title";

        return dialogs.ShowAsync<ChartSettings>(view,
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 1060, PreferredHeight = 760 }, ct);
    }
}
