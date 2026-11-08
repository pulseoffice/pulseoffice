using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfDeckChartMapper
{
    private readonly OdfHrefs _hrefs = new();

    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;
    private static readonly XNamespace Xml = OdfPackage.Namespaces.Xml;
    private static readonly XNamespace Chart = OdfPackage.Namespaces.Chart;

    private readonly OdfUnits _units = new();
    private readonly OdfValues _values = new();
    private readonly InlineChartDocumentBuilder _builder;
    private readonly InlineChartDocumentReader _reader;

    public OdfDeckChartMapper()
    {
        var vocabulary = new OdfChartVocabulary();
        _builder = new InlineChartDocumentBuilder(vocabulary, _units);
        _reader = new InlineChartDocumentReader(vocabulary);
    }

    public XElement BuildFrame(ObjectElement element, string href) =>
        new(Draw + "frame",
            new XAttribute(Xml + "id", element.Id),
            new XAttribute(Draw + "id", element.Id),
            _units.Bounds(element.Bounds, Svg),
            new XElement(Draw + "object",
                new XAttribute(Xlink + "href", href),
                new XAttribute(Xlink + "type", "simple"),
                new XAttribute(Xlink + "show", "embed"),
                new XAttribute(Xlink + "actuate", "onLoad")));

    public XDocument BuildDocument(ChartDefinition definition, ChartData data, DocumentSize frame, System.Globalization.CultureInfo culture) => _builder.Build(definition, data, frame, culture);

    public ObjectElement? Read(XElement frame, string id, Func<string, XDocument?> readDoc)
    {
        if ((string?)frame.Element(Draw + "object")?.Attribute(Xlink + "href") is not { } href) return null;

        var path = _hrefs.Normalize(href).TrimEnd('/') + "/content.xml";
        var chart = readDoc(path)?.Root?.Element(Office + "body")?.Element(Office + "chart")?.Element(Chart + "chart");
        if (chart is null || _reader.Read(chart, id) is not { } content) return null;

        return new ObjectElement { Id = id, Bounds = _values.Bounds(frame), Content = content };
    }
}
