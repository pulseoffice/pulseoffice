using PulseOffice.Charts;
using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.Charts;

public interface IEmbeddedChartPrompt
{
    Task<ChartDefinition?> CreateAsync(string id, System.Globalization.CultureInfo culture, CancellationToken ct = default);
}

public sealed class EmbeddedChartPrompt(IChartPicker charts, IChartSettingsMapper settings, ITextCatalog texts) : IEmbeddedChartPrompt
{
    public async Task<ChartDefinition?> CreateAsync(string id, System.Globalization.CultureInfo culture, CancellationToken ct = default)
    {
        var data = Sample();
        var choice = await charts.ChooseAsync(new ChartPickerRequest
        {
            Data = _ => data,
            Culture = culture,
            Settings = new ChartSettings { Type = ChartType.Column, Title = texts["chart.sample.title"] },
            HeaderEditable = false,
            SeriesOrientationEditable = false,
        }, ct).ConfigureAwait(true);
        if (choice is null) return null;
        return settings.Apply(new ChartDefinition
        {
            Id = id,
            Type = choice.Type,
            SourceSheet = string.Empty,
            SourceRange = string.Empty,
            InlineData = data,
        }, choice with { SourceRange = null });
    }

    private ChartData Sample() => new(
        texts["chart.sample.categories"].Split(';'),
        [
            new ChartSeries(texts.Format("chart.series.fallback", 1), [12, 19, 14, 22]) { ColorHex = ChartPalette.For(0) },
            new ChartSeries(texts.Format("chart.series.fallback", 2), [8, 11, 9, 15]) { ColorHex = ChartPalette.For(1) },
        ]);
}
