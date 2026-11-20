using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class ChartExLayoutProperties
{
    private const int MostSubtotals = 100_000;

    private readonly XNamespace _cx = ChartExReader.Namespace;

    public XElement? Write(ChartType type, ChartStatisticalLayout layout, bool categorical)
    {
        var properties = new XElement(_cx + "layoutPr");
        switch (type)
        {
            case ChartType.Treemap:
                properties.Add(new XElement(_cx + "parentLabelLayout", new XAttribute("val", ParentLabels(layout.ParentLabels))));
                break;
            case ChartType.BoxPlot:
                properties.Add(
                    new XElement(_cx + "visibility", new XAttribute("meanLine", Flag(layout.MeanLine)), new XAttribute("meanMarker", Flag(layout.MeanMarker)),
                        new XAttribute("nonoutliers", Flag(layout.InnerPoints)), new XAttribute("outliers", Flag(layout.Outliers))),
                    new XElement(_cx + "statistics", new XAttribute("quartileMethod", layout.QuartileMethod == ChartQuartileMethod.Inclusive ? "inclusive" : "exclusive")));
                break;
            case ChartType.Histogram:
                properties.Add(Binning(layout));
                break;
            case ChartType.Pareto:
                properties.Add(categorical ? new XElement(_cx + "aggregation") : Binning(layout));
                break;
            case ChartType.Waterfall:
                if (layout.Subtotals.Count == 0) return null;
                properties.Add(new XElement(_cx + "subtotals", layout.Subtotals.Where(index => index >= 0).Distinct().Order()
                    .Select(index => new XElement(_cx + "idx", new XAttribute("val", index)))));
                break;
            default:
                return null;
        }
        return properties;
    }

    public ChartStatisticalLayout Read(XElement? properties)
    {
        if (properties is null) return ChartStatisticalLayout.Automatic;
        var layout = ChartStatisticalLayout.Automatic;

        if (properties.Element(_cx + "parentLabelLayout") is { } parent)
            layout = layout with
            {
                ParentLabels = (string?)parent.Attribute("val") switch
                {
                    "banner" => ChartParentLabels.Banner,
                    "none" => ChartParentLabels.None,
                    _ => ChartParentLabels.Overlapping,
                },
            };

        if (properties.Element(_cx + "visibility") is { } visibility)
            layout = layout with
            {
                MeanLine = Flag(visibility, "meanLine", false),
                MeanMarker = Flag(visibility, "meanMarker", true),
                InnerPoints = Flag(visibility, "nonoutliers", false),
                Outliers = Flag(visibility, "outliers", true),
            };

        if ((string?)properties.Element(_cx + "statistics")?.Attribute("quartileMethod") == "inclusive")
            layout = layout with { QuartileMethod = ChartQuartileMethod.Inclusive };

        if (properties.Element(_cx + "binning") is { } binning)
            layout = layout with
            {
                IntervalClosedRight = (string?)binning.Attribute("intervalClosed") != "l",
                Underflow = Number((string?)binning.Attribute("underflow")),
                Overflow = Number((string?)binning.Attribute("overflow")),
                BinWidth = Number((string?)binning.Element(_cx + "binSize")?.Attribute("val")) is > 0 and var width ? width : null,
                BinCount = int.TryParse((string?)binning.Element(_cx + "binCount")?.Attribute("val"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count is > 0 and <= 10_000 ? count : null,
            };

        if (properties.Element(_cx + "subtotals") is { } subtotals)
            layout = layout with
            {
                Subtotals = [.. subtotals.Elements(_cx + "idx")
                    .Select(index => int.TryParse((string?)index.Attribute("val"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1)
                    .Where(value => value is >= 0 and < MostSubtotals).Distinct().Order()],
            };

        return layout;
    }

    private XElement Binning(ChartStatisticalLayout layout)
    {
        var binning = new XElement(_cx + "binning", new XAttribute("intervalClosed", layout.IntervalClosedRight ? "r" : "l"));
        if (layout.Underflow is { } underflow) binning.Add(new XAttribute("underflow", underflow.ToString("R", CultureInfo.InvariantCulture)));
        if (layout.Overflow is { } overflow) binning.Add(new XAttribute("overflow", overflow.ToString("R", CultureInfo.InvariantCulture)));
        if (layout.BinWidth is { } width) binning.Add(new XElement(_cx + "binSize", new XAttribute("val", width.ToString("R", CultureInfo.InvariantCulture))));
        else if (layout.BinCount is { } count) binning.Add(new XElement(_cx + "binCount", new XAttribute("val", count)));
        return binning;
    }

    private string ParentLabels(ChartParentLabels labels) => labels switch
    {
        ChartParentLabels.Banner => "banner",
        ChartParentLabels.None => "none",
        _ => "overlapping",
    };

    private int Flag(bool value) => value ? 1 : 0;

    private bool Flag(XElement owner, string name, bool fallback) =>
        (string?)owner.Attribute(name) switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => fallback,
        };

    private double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;
}
