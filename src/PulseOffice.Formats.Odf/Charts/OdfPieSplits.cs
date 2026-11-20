using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Charts;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfPieSplits(XNamespace pulse)
{
    private readonly IReadOnlyList<(PieSplitKind Kind, string Name)> _kinds =
    [
        (PieSplitKind.Position, "position"),
        (PieSplitKind.Value, "value"),
        (PieSplitKind.Percent, "percent"),
        (PieSplitKind.Custom, "custom"),
    ];

    public IEnumerable<XAttribute> Attributes(ChartDefinition definition)
    {
        var split = definition.PieSplit;
        if (split.IsAutomatic || definition.Type is not (ChartType.PieOfPie or ChartType.BarOfPie)) yield break;
        if (_kinds.FirstOrDefault(entry => entry.Kind == split.Kind).Name is { } name)
        {
            yield return new XAttribute(pulse + "pie-split", name);
            if (split.Kind == PieSplitKind.Custom)
                yield return new XAttribute(pulse + "pie-split-points", string.Join(' ', split.CustomPoints.Where(point => point >= 0).Distinct().Order().Select(point => point.ToString(CultureInfo.InvariantCulture))));
            else
                yield return new XAttribute(pulse + "pie-split-threshold", split.Threshold.ToString("R", CultureInfo.InvariantCulture));
        }
        if (split.SecondPlotSize != ChartPieSplit.DefaultSecondPlotSize)
            yield return new XAttribute(pulse + "second-plot-size", split.SecondPlotSize.ToString(CultureInfo.InvariantCulture));
    }

    public ChartPieSplit Read(XElement chart)
    {
        var name = (string?)chart.Attribute(pulse + "pie-split");
        var kind = _kinds.FirstOrDefault(entry => entry.Name == name) is { Name: not null } found ? found.Kind : PieSplitKind.Automatic;
        var size = int.TryParse((string?)chart.Attribute(pulse + "second-plot-size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var read)
            ? Math.Clamp(read, ChartPieSplit.MinimumSecondPlotSize, ChartPieSplit.MaximumSecondPlotSize)
            : ChartPieSplit.DefaultSecondPlotSize;
        return new ChartPieSplit
        {
            Kind = kind,
            Threshold = kind is PieSplitKind.Position or PieSplitKind.Value or PieSplitKind.Percent
                && double.TryParse((string?)chart.Attribute(pulse + "pie-split-threshold"), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
                && double.IsFinite(threshold) ? threshold : 0,
            CustomPoints = kind == PieSplitKind.Custom
                ? [.. ((string?)chart.Attribute(pulse + "pie-split-points") ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var point) ? point : -1)
                    .Where(point => point >= 0)]
                : [],
            SecondPlotSize = size,
        };
    }
}
