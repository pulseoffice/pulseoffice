using PulseOffice.Charts;

namespace PulseOffice.Formats.OpenXml;

public sealed class OoxmlChartFidelity
{
    private readonly ChartFamilies _families = new();

    public bool LosesType(ChartDefinition chart) => !_families.IsNative(chart.Type);

    public bool FlattensThreeDimensional(ChartDefinition chart) => chart.ThreeDimensional && !_families.KeepsThreeDimensional(chart.Type);
}
