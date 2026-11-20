using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Charts;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfStatisticalLayouts(XNamespace pulse)
{
    private const int MostSubtotals = 100_000;

    public IEnumerable<XAttribute> Attributes(ChartDefinition definition)
    {
        var layout = definition.Statistical;
        if (!layout.IsAutomatic)
        {
            if (layout.BinCount is { } count) yield return new XAttribute(pulse + "bin-count", count.ToString(CultureInfo.InvariantCulture));
            if (layout.BinWidth is { } width) yield return new XAttribute(pulse + "bin-width", Number(width));
            if (layout.Underflow is { } underflow) yield return new XAttribute(pulse + "underflow", Number(underflow));
            if (layout.Overflow is { } overflow) yield return new XAttribute(pulse + "overflow", Number(overflow));
            if (!layout.IntervalClosedRight) yield return new XAttribute(pulse + "interval-closed", "left");
            if (layout.QuartileMethod == ChartQuartileMethod.Inclusive) yield return new XAttribute(pulse + "quartile-method", "inclusive");
            if (!layout.MeanMarker) yield return new XAttribute(pulse + "mean-marker", "false");
            if (layout.MeanLine) yield return new XAttribute(pulse + "mean-line", "true");
            if (layout.InnerPoints) yield return new XAttribute(pulse + "inner-points", "true");
            if (!layout.Outliers) yield return new XAttribute(pulse + "outliers", "false");
            if (layout.Subtotals.Count > 0)
                yield return new XAttribute(pulse + "subtotals", string.Join(' ', layout.Subtotals.Where(index => index >= 0).Distinct().Order().Select(index => index.ToString(CultureInfo.InvariantCulture))));
            if (layout.ParentLabels != ChartParentLabels.Overlapping)
                yield return new XAttribute(pulse + "parent-labels", layout.ParentLabels == ChartParentLabels.Banner ? "banner" : "none");
        }

        var style = definition.Style;
        if (style.StyleId is { } id) yield return new XAttribute(pulse + "chart-style-id", id.ToString(CultureInfo.InvariantCulture));
        if (style.ColorStyle is { } colors) yield return new XAttribute(pulse + "color-style", colors);
        if (style.ChartStyle is { } chartStyle) yield return new XAttribute(pulse + "chart-style", chartStyle);
    }

    public ChartStatisticalLayout Read(XElement chart) => new()
    {
        BinCount = int.TryParse((string?)chart.Attribute(pulse + "bin-count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count is > 0 and <= 10_000 ? count : null,
        BinWidth = Number((string?)chart.Attribute(pulse + "bin-width")) is > 0 and var width ? width : null,
        Underflow = Number((string?)chart.Attribute(pulse + "underflow")),
        Overflow = Number((string?)chart.Attribute(pulse + "overflow")),
        IntervalClosedRight = (string?)chart.Attribute(pulse + "interval-closed") != "left",
        QuartileMethod = (string?)chart.Attribute(pulse + "quartile-method") == "inclusive" ? ChartQuartileMethod.Inclusive : ChartQuartileMethod.Exclusive,
        MeanMarker = (string?)chart.Attribute(pulse + "mean-marker") != "false",
        MeanLine = (string?)chart.Attribute(pulse + "mean-line") == "true",
        InnerPoints = (string?)chart.Attribute(pulse + "inner-points") == "true",
        Outliers = (string?)chart.Attribute(pulse + "outliers") != "false",
        Subtotals = [.. ((string?)chart.Attribute(pulse + "subtotals") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : -1)
            .Where(index => index is >= 0 and < MostSubtotals).Distinct().Order()],
        ParentLabels = (string?)chart.Attribute(pulse + "parent-labels") switch
        {
            "banner" => ChartParentLabels.Banner,
            "none" => ChartParentLabels.None,
            _ => ChartParentLabels.Overlapping,
        },
    };

    public ChartStyleReference Style(XElement chart) => new()
    {
        StyleId = int.TryParse((string?)chart.Attribute(pulse + "chart-style-id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id is >= 1 and <= 48 ? id : null,
        ColorStyle = (string?)chart.Attribute(pulse + "color-style"),
        ChartStyle = (string?)chart.Attribute(pulse + "chart-style"),
    };

    private string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;
}
