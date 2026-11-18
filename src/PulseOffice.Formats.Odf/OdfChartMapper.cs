using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.Odf;

public sealed class OdfChartMapper
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;
    private static readonly XNamespace Chart = OdfPackage.Namespaces.Chart;

    private readonly List<(string Path, XDocument Content)> _objects = [];
    private readonly OdfUnits _units = new();
    private readonly OdfValues _values = new();
    private readonly SheetChartDocumentBuilder _builder;
    private readonly SheetChartDocumentReader _reader;
    private readonly InlineChartDocumentBuilder _staticBuilder;
    private readonly InlineChartDocumentReader _staticReader;
    private readonly AnchoredFrames _frames = new();
    private readonly OdfDrawUnits _draw = new();
    private readonly ISheetPlacement _placement;
    private readonly SeriesSourceAddresses _sources = new();
    private readonly PulseOffice.Sheets.Engine.Operations.ChartTableSources _tables = new();
    private readonly OdfChartTableSources _tableSources = new();
    private readonly PulseOffice.Sheets.Engine.Operations.ChartErrorValueRefresh _errorValues = new();
    private readonly PulseOffice.Sheets.Engine.Operations.ChartDataExtractor _data = new(
        new PulseOffice.Sheets.Engine.Operations.CanonicalSeriesNames(), new PulseOffice.Sheets.Engine.Operations.DocumentLanguageChartCellText());

    public OdfChartMapper(ISheetPlacement? placement = null)
    {
        _placement = placement ?? new SheetPlacement();
        var vocabulary = new OdfChartVocabulary();
        _builder = new SheetChartDocumentBuilder(vocabulary, _units, new PulseOffice.Formats.Odf.TextCodecs.OdfParagraphText());
        _reader = new SheetChartDocumentReader(vocabulary, new CellRangeAddresses(), new PulseOffice.Formats.Odf.TextCodecs.OdfParagraphText());
        _staticBuilder = new InlineChartDocumentBuilder(vocabulary, _units, new PulseOffice.Formats.Odf.TextCodecs.OdfParagraphText());
        _staticReader = new InlineChartDocumentReader(vocabulary, new PulseOffice.Formats.Odf.TextCodecs.OdfParagraphText());
    }

    public IReadOnlyList<(string Path, XDocument Content)> Objects => _objects;

    public IReadOnlyDictionary<CellAddress, List<XElement>> BuildFrames(Worksheet sheet, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var frames = new Dictionary<CellAddress, List<XElement>>();
        foreach (var placed in sheet.Objects)
        {
            if (placed.Content is not ChartDefinition stored) continue;
            var structured = _errorValues.Refresh(workbook, stored);
            var definition = _tables.Flatten(structured, workbook);

            var path = $"Object {_objects.Count + 1}";
            var content = IsStatic(definition)
                ? _staticBuilder.Build(definition, definition.InlineData!, placed.Size)
                : _builder.Build(definition, _data.Extract(workbook, definition), placed.Size);
            _tableSources.Write(content, structured);
            _objects.Add((path, content));

            if (!frames.TryGetValue(placed.Anchor, out var list)) frames[placed.Anchor] = list = [];
            list.Add(Frame(placed, definition, path, sheet));
        }
        return frames;
    }

    public IReadOnlyList<PlacedObject> ReadCharts(XElement table, OdfPackage package)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(package);

        return [.. _frames.In(table).Select(anchored => Read(anchored, package)).OfType<PlacedObject>()];
    }

    private XElement Frame(PlacedObject placed, ChartDefinition definition, string path, Worksheet sheet) =>
        new(Draw + "frame",
            new XAttribute(Draw + "name", placed.Id),
            new XAttribute(Draw + "z-index", placed.ZOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new OdfCellEnd(sheet.Name, _placement.End(sheet.Layout, placed), _units.Cm).Attributes(),
            new XAttribute(Svg + "x", _units.Cm(placed.OffsetX)),
            new XAttribute(Svg + "y", _units.Cm(placed.OffsetY)),
            new XAttribute(Svg + "width", _units.Cm(placed.Size.Width)),
            new XAttribute(Svg + "height", _units.Cm(placed.Size.Height)),
            new XElement(Draw + "object",
                new XAttribute(Xlink + "href", "./" + path),
                new XAttribute(Xlink + "type", "simple"),
                new XAttribute(Xlink + "show", "embed"),
                new XAttribute(Xlink + "actuate", "onLoad"),
                IsStatic(definition) ? null : new XAttribute(Draw + "notify-on-update-of-ranges", definition.HasSeriesSources ? _sources.Whole(definition) : definition.SourceRange)));

    private static bool IsStatic(ChartDefinition definition) =>
        definition.InlineData is not null && !definition.HasSeriesSources && string.IsNullOrEmpty(definition.SourceSheet);

    private static bool Referenced(XElement chart) =>
        chart.Element(Table + "table") is null
        || chart.Descendants().SelectMany(element => element.Attributes())
            .Any(attribute => attribute.Name.LocalName is "values-cell-range-address" or "cell-range-address" && !attribute.Value.StartsWith("local-table", StringComparison.Ordinal));

    private PlacedObject? Read(AnchoredFrame anchored, OdfPackage package)
    {
        if ((string?)anchored.Frame.Element(Draw + "object")?.Attribute(Xlink + "href") is not { } href) return null;

        var path = href.TrimStart('.', '/');
        if (package.ReadXml($"{path}/content.xml")?.Descendants(Chart + "chart").FirstOrDefault() is not { } chart) return null;

        var name = (string?)anchored.Frame.Attribute(Draw + "name") ?? path;
        if ((Referenced(chart) ? _reader.Read(chart, name) : _staticReader.Read(chart, name)) is not { } definition) return null;
        if (_tableSources.Read(chart) is { } kept) definition = definition with { SeriesSources = kept };

        return new PlacedObject
        {
            Id = name,
            Content = definition,
            Anchor = anchored.Anchor,
            OffsetX = _draw.Parse((string?)anchored.Frame.Attribute(Svg + "x")),
            OffsetY = _draw.Parse((string?)anchored.Frame.Attribute(Svg + "y")),
            ZOrder = _draw.Order(anchored.Frame) is var order and >= 0 ? order : 0,
            Size = new DocumentSize(
                _values.Twips((string?)anchored.Frame.Attribute(Svg + "width"), 12),
                _values.Twips((string?)anchored.Frame.Attribute(Svg + "height"), 7)),
        };
    }
}
