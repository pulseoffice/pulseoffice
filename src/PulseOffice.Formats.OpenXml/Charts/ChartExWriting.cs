using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed record ChartExLevel(int Count, IReadOnlyList<(uint Index, string Text)> Points);

internal sealed record ChartExDimension(string? Formula, IReadOnlyList<ChartExLevel> Levels);

internal sealed class ChartExLayouts
{
    private readonly IReadOnlyDictionary<ChartType, string> _layouts = new Dictionary<ChartType, string>
    {
        [ChartType.Treemap] = "treemap",
        [ChartType.Sunburst] = "sunburst",
        [ChartType.BoxPlot] = "boxWhisker",
        [ChartType.Waterfall] = "waterfall",
        [ChartType.Funnel] = "funnel",
        [ChartType.Histogram] = "clusteredColumn",
        [ChartType.Pareto] = "clusteredColumn",
    };

    public bool Writes(ChartType type) => _layouts.ContainsKey(type);

    public string Layout(ChartType type) => _layouts[type];

    public string Requires(ChartType type) => type == ChartType.Funnel ? "cx2" : "cx1";

    public string RequiresNamespace(ChartType type) => type == ChartType.Funnel
        ? "http://schemas.microsoft.com/office/drawing/2015/10/21/chartex"
        : "http://schemas.microsoft.com/office/drawing/2015/9/8/chartex";
}

internal sealed class ChartExSpaceWriter
{
    private const string Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private readonly XNamespace _cx = ChartExReader.Namespace;
    private readonly ChartExLayouts _layouts;
    private readonly ChartExLayoutProperties _properties = new();

    public ChartExSpaceWriter(ChartExLayouts layouts) => _layouts = layouts;

    public XDocument Build(ChartDefinition definition, C.ChartSpace classic)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(classic);

        var series = classic.Descendants().Where(element => element.LocalName == "ser" && element.NamespaceUri == classic.NamespaceUri).ToList();
        var data = new XElement(_cx + "chartData");
        var region = new XElement(_cx + "plotAreaRegion");
        var layout = _layouts.Layout(definition.Type);
        var hasAxes = definition.Type is not (ChartType.Treemap or ChartType.Sunburst);

        for (var index = 0; index < series.Count; index++)
        {
            var element = series[index];
            var categories = Dimension(element.ChildElements.FirstOrDefault(child => child.LocalName is "cat" or "xVal"));
            var values = Dimension(element.ChildElements.FirstOrDefault(child => child.LocalName is "val" or "yVal"));
            var block = new XElement(_cx + "data", new XAttribute("id", index));
            if (categories is not null) block.Add(Dimension(categories, "strDim", "cat"));
            if (values is not null) block.Add(Dimension(values, "numDim", "val"));
            data.Add(block);

            var written = new XElement(_cx + "series", new XAttribute("layoutId", layout), new XAttribute("uniqueId", Guid(index)));
            if (Name(element) is { } name) written.Add(name);
            written.Add(new XElement(_cx + "dataId", new XAttribute("val", index)));
            if (_properties.Write(definition.Type, definition.Statistical, categories is not null) is { } properties) written.Add(properties);
            if (definition.Type is ChartType.Histogram or ChartType.Pareto)
                written.Add(new XElement(_cx + "axisId", new XAttribute("val", 0)), new XElement(_cx + "axisId", new XAttribute("val", 1)));
            region.Add(written);

            if (definition.Type == ChartType.Pareto)
                region.Add(new XElement(_cx + "series", new XAttribute("layoutId", "paretoLine"), new XAttribute("ownerIdx", index),
                    new XAttribute("uniqueId", Guid(index + 0x1000)), new XElement(_cx + "axisId", new XAttribute("val", 2))));
        }

        var plot = new XElement(_cx + "plotArea", region);
        if (hasAxes) plot.Add(Axes(definition.Type));

        var chart = new XElement(_cx + "chart");
        if (!string.IsNullOrWhiteSpace(definition.ChartTitle))
            chart.Add(new XElement(_cx + "title", new XAttribute("pos", "t"), new XAttribute("align", "ctr"), new XAttribute("overlay", 0),
                new XElement(_cx + "tx", new XElement(_cx + "txData", new XElement(_cx + "v", definition.ChartTitle)))));
        chart.Add(plot);
        if (definition.Legend != LegendPosition.None)
            chart.Add(new XElement(_cx + "legend", new XAttribute("pos", Legend(definition.Legend)), new XAttribute("align", "ctr"), new XAttribute("overlay", 0)));

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(_cx + "chartSpace",
                new XAttribute(XNamespace.Xmlns + "a", Drawing),
                new XAttribute(XNamespace.Xmlns + "r", Relationships),
                new XAttribute(XNamespace.Xmlns + "cx", ChartExReader.Namespace),
                data, chart));
    }

    private ChartExDimension? Dimension(OpenXmlElement? owner)
    {
        if (owner is null) return null;
        var formula = owner.Descendants<C.Formula>().FirstOrDefault()?.Text;
        var multi = owner.Descendants<C.Level>().ToList();
        var levels = multi.Count > 0
            ? multi.Select(level => Level(level, Count(owner))).ToList()
            : [Level(owner, Count(owner))];
        return new ChartExDimension(string.IsNullOrWhiteSpace(formula) ? null : formula, levels);
    }

    private int Count(OpenXmlElement owner) => (int)(owner.Descendants<C.PointCount>().FirstOrDefault()?.Val?.Value ?? 0u);

    private ChartExLevel Level(OpenXmlElement owner, int count)
    {
        var points = owner.Descendants().Where(element => element is C.StringPoint or C.NumericPoint)
            .Select(element => (Index: element.GetAttributes().Where(attribute => attribute.LocalName == "idx").Select(attribute => uint.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0u).FirstOrDefault(),
                Text: element.GetFirstChild<C.NumericValue>()?.Text ?? string.Empty))
            .ToList();
        return new ChartExLevel(Math.Max(count, points.Count == 0 ? 0 : (int)points.Max(point => point.Index) + 1), points);
    }

    private XElement Dimension(ChartExDimension dimension, string kind, string type)
    {
        var element = new XElement(_cx + kind, new XAttribute("type", type));
        if (dimension.Formula is { } formula) element.Add(new XElement(_cx + "f", formula));
        foreach (var level in dimension.Levels)
        {
            var written = new XElement(_cx + "lvl", new XAttribute("ptCount", level.Count));
            if (kind == "numDim") written.Add(new XAttribute("formatCode", "General"));
            written.Add(level.Points.Select(point => new XElement(_cx + "pt", new XAttribute("idx", point.Index), point.Text)));
            element.Add(written);
        }
        return element;
    }

    private XElement? Name(OpenXmlElement series)
    {
        if (series.GetFirstChild<C.SeriesText>() is not { } text) return null;
        var data = new XElement(_cx + "txData");
        if (text.Descendants<C.Formula>().FirstOrDefault()?.Text is { Length: > 0 } formula) data.Add(new XElement(_cx + "f", formula));
        data.Add(new XElement(_cx + "v", text.Descendants<C.NumericValue>().FirstOrDefault()?.Text ?? string.Empty));
        return new XElement(_cx + "tx", data);
    }

    private IEnumerable<XElement> Axes(ChartType type)
    {
        if (type == ChartType.Funnel)
        {
            yield return new XElement(_cx + "axis", new XAttribute("id", 1), new XElement(_cx + "catScaling", new XAttribute("gapWidth", "0.06")), new XElement(_cx + "tickLabels"));
            yield break;
        }

        yield return new XElement(_cx + "axis", new XAttribute("id", 0), new XElement(_cx + "catScaling", new XAttribute("gapWidth", type is ChartType.Histogram or ChartType.Pareto ? "0" : "0.5")), new XElement(_cx + "tickLabels"));
        yield return new XElement(_cx + "axis", new XAttribute("id", 1), new XElement(_cx + "valScaling"), new XElement(_cx + "majorGridlines"), new XElement(_cx + "tickLabels"));
        if (type == ChartType.Pareto)
            yield return new XElement(_cx + "axis", new XAttribute("id", 2), new XElement(_cx + "valScaling", new XAttribute("max", 1), new XAttribute("min", 0)),
                new XElement(_cx + "units", new XAttribute("unit", "percentage")), new XElement(_cx + "tickLabels"));
    }

    private string Guid(int index) => $"{{{index.ToString("X8", CultureInfo.InvariantCulture)}-5050-4F43-8000-000000000330}}";

    private string Legend(LegendPosition position) => position switch
    {
        LegendPosition.Top => "t",
        LegendPosition.Bottom => "b",
        LegendPosition.Left => "l",
        _ => "r",
    };
}

internal sealed class ChartExAnchors
{
    private const string Compatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private readonly ChartExLayouts _layouts;
    private readonly ChartExSpaceWriter _spaces;

    public ChartExAnchors(ChartExLayouts layouts, ChartExSpaceWriter spaces)
    {
        _layouts = layouts;
        _spaces = spaces;
    }

    public bool Writes(ChartDefinition definition) => _layouts.Writes(definition.Type);

    public OpenXmlCompositeElement Wrap(DrawingsPart drawings, ChartDefinition definition, C.ChartSpace classic, OpenXmlCompositeElement fallback)
    {
        ArgumentNullException.ThrowIfNull(drawings);
        var part = drawings.AddNewPart<ExtendedChartPart>();
        using (var stream = part.GetStream(FileMode.Create, FileAccess.Write)) _spaces.Build(definition, classic).Save(stream);

        var choiceAnchor = (OpenXmlCompositeElement)fallback.CloneNode(true);
        var data = choiceAnchor.Descendants<A.GraphicData>().Single();
        data.RemoveAllChildren();
        data.Uri = ChartExReader.Namespace;
        var reference = new OpenXmlUnknownElement("cx", "chart", ChartExReader.Namespace);
        reference.SetAttribute(new OpenXmlAttribute("r", "id", Relationships, drawings.GetIdOfPart(part)));
        data.AppendChild(reference);

        var choice = new AlternateContentChoice { Requires = _layouts.Requires(definition.Type) };
        choice.AddNamespaceDeclaration(_layouts.Requires(definition.Type), _layouts.RequiresNamespace(definition.Type));
        choice.AppendChild(choiceAnchor);
        var alternate = new AlternateContent(choice, new AlternateContentFallback(fallback));
        alternate.AddNamespaceDeclaration("mc", Compatibility);
        return alternate;
    }

    public OpenXmlCompositeElement? FallbackAnchor(OpenXmlElement alternate) =>
        alternate.ChildElements.Where(child => child.LocalName == "Fallback")
            .SelectMany(fallback => fallback.ChildElements.OfType<OpenXmlCompositeElement>())
            .FirstOrDefault(anchor => anchor.Descendants<Xdr.GraphicFrame>().Any() || anchor.Descendants().Any(element => element.LocalName == "graphicFrame"));
}
