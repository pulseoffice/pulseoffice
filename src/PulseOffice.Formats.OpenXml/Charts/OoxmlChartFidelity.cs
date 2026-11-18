using PulseOffice.Charts;

namespace PulseOffice.Formats.OpenXml;

public sealed class OoxmlChartFidelity
{
    private readonly ChartFamilies _families = new();

    public bool LosesType(ChartDefinition chart) => !_families.IsNative(chart.Type);

    public bool DropsSeriesAnalysis(ChartDefinition chart) =>
        _families.IsNative(chart.Type) && chart.Type is ChartType.Pie or ChartType.ExplodedPie or ChartType.PieOfPie or ChartType.BarOfPie or ChartType.Doughnut or ChartType.Radar or ChartType.FilledRadar
        && chart.SeriesFormats?.Any(format => format?.Trendline is not null || format?.ErrorBars is not null || format?.SecondaryAxis == true) == true;

    public bool DropsCategoryUnits(ChartDefinition chart) =>
        chart.Type is not (ChartType.Scatter or ChartType.ScatterWithLines or ChartType.Bubble)
        && (chart.CategoryAxis.MajorUnit is not null || chart.CategoryAxis.MinorUnit is not null);

    public bool FlattensThreeDimensional(ChartDefinition chart) => chart.ThreeDimensional && !_families.KeepsThreeDimensional(chart.Type);
}
