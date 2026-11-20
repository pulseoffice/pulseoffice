using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed record ChartExSeries(string? NameFormula, string? Name, string? Values, string? Categories, IReadOnlyList<string> CachedCategories, IReadOnlyList<double> CachedValues);

internal sealed record ChartExContent(ChartType Type, string? Title, LegendPosition Legend, IReadOnlyList<ChartExSeries> Series);

internal sealed class ChartExReader
{
    public const string Namespace = "http://schemas.microsoft.com/office/drawing/2014/chartex";
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const int MaximumPoints = 100_000;

    private readonly XNamespace _cx = Namespace;
    private readonly SafeXmlLoader _loader = new();

    private readonly IReadOnlyDictionary<string, ChartType> _layouts = new Dictionary<string, ChartType>(StringComparer.Ordinal)
    {
        ["treemap"] = ChartType.Treemap,
        ["sunburst"] = ChartType.Sunburst,
        ["boxWhisker"] = ChartType.BoxPlot,
        ["waterfall"] = ChartType.Waterfall,
        ["funnel"] = ChartType.Funnel,
        ["clusteredColumn"] = ChartType.Histogram,
        ["paretoLine"] = ChartType.Pareto,
        ["regionMap"] = ChartType.Column,
    };

    public OpenXmlCompositeElement? ChoiceAnchor(OpenXmlElement alternate) =>
        alternate.ChildElements.Where(child => child.LocalName == "Choice")
            .SelectMany(choice => choice.ChildElements.OfType<OpenXmlCompositeElement>())
            .FirstOrDefault(anchor => ChartId(anchor) is not null);

    public string? ChartId(OpenXmlElement anchor) =>
        anchor.Descendants().FirstOrDefault(element => element.LocalName == "chart" && element.NamespaceUri == Namespace)
            ?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "id" && attribute.NamespaceUri == Relationships).Value is { Length: > 0 } id
            ? id
            : null;

    public ChartExContent? Read(OpenXmlPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        XDocument document;
        using (var stream = part.GetStream(FileMode.Open, FileAccess.Read)) document = _loader.Document(stream);
        return Read(document.Root);
    }

    public ChartExContent? Read(XElement? space)
    {
        if (space is null) return null;
        var data = space.Element(_cx + "chartData")?.Elements(_cx + "data")
            .GroupBy(element => (string?)element.Attribute("id") ?? "0", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal) ?? new Dictionary<string, XElement>(StringComparer.Ordinal);
        var chart = space.Element(_cx + "chart");
        var all = chart?.Descendants(_cx + "series").ToList() ?? [];
        if (all.Count == 0) return null;

        var layouts = all.Select(series => (string?)series.Attribute("layoutId") ?? string.Empty).ToList();
        var type = layouts.Contains("paretoLine") ? ChartType.Pareto
            : layouts.Select(layout => _layouts.GetValueOrDefault(layout, ChartType.Column)).First();
        var series = all.Where(element => (string?)element.Attribute("layoutId") != "paretoLine")
            .Select(element => Series(element, data))
            .Where(read => read is not null)
            .Select(read => read!)
            .ToList();
        if (series.Count == 0) return null;

        return new ChartExContent(type, Text(chart?.Element(_cx + "title")), Legend(chart?.Element(_cx + "legend")), series);
    }

    private ChartExSeries? Series(XElement series, IReadOnlyDictionary<string, XElement> data)
    {
        var id = (string?)series.Element(_cx + "dataId")?.Attribute("val") ?? "0";
        if (!data.TryGetValue(id, out var block)) return null;
        var values = block.Elements(_cx + "numDim").FirstOrDefault(dimension => (string?)dimension.Attribute("type") is "val" or "size" or null);
        if (values is null) return null;
        var categories = block.Elements(_cx + "strDim").Concat(block.Elements(_cx + "numDim"))
            .FirstOrDefault(dimension => (string?)dimension.Attribute("type") is "cat" or "x");
        var name = series.Element(_cx + "tx")?.Element(_cx + "txData");

        return new ChartExSeries(
            Formula(name),
            (string?)name?.Element(_cx + "v"),
            Formula(values),
            Formula(categories),
            [.. Points(categories?.Elements(_cx + "lvl").FirstOrDefault()).Select(point => point.Text)],
            [.. Points(values.Elements(_cx + "lvl").FirstOrDefault()).Select(point => Number(point.Text))]);
    }

    private string? Formula(XElement? owner) =>
        owner?.Element(_cx + "f")?.Value.Trim() is { Length: > 0 } formula ? formula : null;

    private IReadOnlyList<(int Index, string Text)> Points(XElement? level)
    {
        if (level is null) return [];
        var points = level.Elements(_cx + "pt")
            .Select(point => (Index: int.TryParse((string?)point.Attribute("idx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : -1, Text: point.Value))
            .Where(point => point.Index is >= 0 and < MaximumPoints)
            .ToList();
        var count = int.TryParse((string?)level.Attribute("ptCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var declared)
            ? Math.Clamp(declared, 0, MaximumPoints)
            : points.Count == 0 ? 0 : points.Max(point => point.Index) + 1;
        var slots = Enumerable.Repeat(string.Empty, Math.Max(count, points.Count == 0 ? 0 : points.Max(point => point.Index) + 1)).ToArray();
        foreach (var point in points) slots[point.Index] = point.Text;
        return [.. slots.Select((text, index) => (index, text))];
    }

    private double Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

    private string? Text(XElement? title)
    {
        if (title?.Element(_cx + "tx") is not { } text) return null;
        var plain = text.Element(_cx + "txData")?.Element(_cx + "v")?.Value
            ?? string.Concat(text.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
        return string.IsNullOrWhiteSpace(plain) ? null : plain;
    }

    private LegendPosition Legend(XElement? legend) =>
        legend is null ? LegendPosition.None
        : (string?)legend.Attribute("pos") switch
        {
            "t" => LegendPosition.Top,
            "b" => LegendPosition.Bottom,
            "l" => LegendPosition.Left,
            _ => LegendPosition.Right,
        };
}
