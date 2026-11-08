using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public sealed record ChartEditSource
{
    public required Func<ChartDefinition, ChartData> Data { get; init; }

    public required System.Globalization.CultureInfo Culture { get; init; }

    public bool DataLayoutEditable { get; init; } = true;

    public Func<string, bool>? RangeValidator { get; init; }

    public PulseOffice.UI.Controls.References.IReferencePicker? RangePicker { get; init; }

    public ChartSeriesRangeEditing? SeriesRanges { get; init; }
}

public interface IChartEditor
{
    Task<ChartDefinition?> EditAsync(ChartDefinition chart, ChartEditSource source, CancellationToken ct = default);
}

public sealed class ChartEditor(IChartPicker picker, IChartSettingsMapper mapper) : IChartEditor
{
    public async Task<ChartDefinition?> EditAsync(ChartDefinition chart, ChartEditSource source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(source);

        var settings = mapper.From(chart);
        var request = new ChartPickerRequest
        {
            Mode = ChartPickerMode.Edit,
            Settings = settings,
            Culture = source.Culture,
            Data = current => source.Data(mapper.Apply(chart, current)),
            HeaderEditable = source.DataLayoutEditable,
            SeriesOrientationEditable = source.DataLayoutEditable,
            RangeValidator = settings.SourceRange is null ? null : source.RangeValidator,
            RangePicker = settings.SourceRange is null ? null : source.RangePicker,
            SeriesRanges = settings.SourceRange is null && settings.SeriesSources is null ? null : source.SeriesRanges,
        };

        if (await picker.ChooseAsync(request, ct).ConfigureAwait(true) is not { } chosen) return null;

        var updated = mapper.Apply(chart, chosen);
        return updated == chart ? null : updated;
    }
}
