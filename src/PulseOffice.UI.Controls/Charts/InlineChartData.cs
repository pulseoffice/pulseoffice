using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts;

public interface IInlineChartData
{
    ChartData For(ChartDefinition chart);

    ChartDefinition Settle(ChartDefinition chart);

    ChartEditSource Source(System.Globalization.CultureInfo culture);
}

public sealed class InlineChartData(IChartSampleData samples, IChartFamilyCatalog families) : IInlineChartData
{
    public ChartData For(ChartDefinition chart) =>
        chart.InlineData ?? samples.For(families.FamilyOf(chart.Type));

    private readonly ChartInlineErrorValues _errorValues = new();

    public ChartDefinition Settle(ChartDefinition chart) =>
        _errorValues.Refresh(chart.InlineData is null ? chart with { InlineData = For(chart) } : chart);

    public ChartEditSource Source(System.Globalization.CultureInfo culture) => new() { Data = For, Culture = culture, DataLayoutEditable = false };
}
