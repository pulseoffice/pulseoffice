using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfImageTypes
{
    private readonly IReadOnlyList<(string Extension, string MediaType)> _types =
        [("png", "image/png"), ("jpg", "image/jpeg"), ("jpeg", "image/jpeg"), ("gif", "image/gif"), ("bmp", "image/bmp"), ("tif", "image/tiff"), ("tiff", "image/tiff"), ("webp", "image/webp"), ("wmf", "image/x-wmf"), ("emf", "image/x-emf")];

    public string Extension(string mediaType) =>
        _types.Where(type => type.MediaType == mediaType).Select(type => type.Extension).DefaultIfEmpty("png").First();

    public string MediaTypeOf(string href)
    {
        var extension = href[(href.LastIndexOf('.') + 1)..].ToLowerInvariant();
        return _types.Where(type => type.Extension == extension).Select(type => type.MediaType).DefaultIfEmpty("image/png").First();
    }
}

internal abstract class DeckObjectCodec
{

    protected static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    protected static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    protected static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;
    protected static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    protected static readonly XNamespace Xml = OdfPackage.Namespaces.Xml;
    protected static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    protected static readonly XNamespace Style = OdfPackage.Namespaces.Style;

    protected OdfHrefs Hrefs { get; } = new();

    protected XElement Placed(XName name, ObjectElement element, params object?[] content) =>
        Styled(name, element, null, content);

    protected XElement Styled(XName name, ObjectElement element, string? styleName, params object?[] content) =>
        new(name,
            styleName is null ? null : new XAttribute(Draw + "style-name", styleName),
            new XAttribute(Xml + "id", element.Id),
            new XAttribute(Draw + "id", element.Id),
            new XAttribute(Svg + "x", Cm(element.Bounds.Left)),
            new XAttribute(Svg + "y", Cm(element.Bounds.Top)),
            new XAttribute(Svg + "width", Cm(element.Bounds.Width)),
            new XAttribute(Svg + "height", Cm(element.Bounds.Height)),
            content);

    protected XElement Embedded(XName name, string href, params object[] content) =>
        new(name, content,
            new XAttribute(Xlink + "href", href),
            new XAttribute(Xlink + "type", "simple"),
            new XAttribute(Xlink + "show", "embed"),
            new XAttribute(Xlink + "actuate", "onLoad"));

    protected DocumentRect Bounds(XElement node) => new(
        ReadCm(node.Attribute(Svg + "x")), ReadCm(node.Attribute(Svg + "y")),
        ReadCm(node.Attribute(Svg + "width")), ReadCm(node.Attribute(Svg + "height")));

    protected double Centimeters(string value) => (new OdfLength().Parse(value) ?? Twip.Zero).ToCentimeters();

    protected string Cm(Twip value) => (value.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter).ToString("0.###", CultureInfo.InvariantCulture) + "cm";

    private Twip ReadCm(XAttribute? attribute) => new OdfLength().Parse(attribute?.Value, Twip.Zero);
}

internal sealed class DeckPathCodec(IShapePathWriter paths, OdfShapeStyleReader styles) : DeckObjectCodec
{
    private const int Box = 21600;

    public XElement Build(ObjectElement element, ShapeOutline outline, string styleName)
    {
        return Styled(Draw + "path", element, styleName,
            new XAttribute(Svg + "viewBox", $"0 0 {Box} {Box}"),
            new XAttribute(Svg + "d", paths.Write(outline.Scaled(Box, Box))));
    }

    private ShapeOutline? Outline(XElement node)
    {
        var outline = (string?)node.Attribute(Svg + "d") is { } data
            ? new SvgPathParser().Parse(data)
            : (string?)node.Attribute(Draw + "points") is { } points ? Polyline(points, node.Name == Draw + "polygon") : null;
        return outline is null || outline.Figures.Count == 0 ? null : new OdfViewBox(node, Box).Normalize(outline);
    }

    private ShapeOutline Polyline(string text, bool closed)
    {
        var points = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(','))
            .Where(parts => parts.Length == 2)
            .Select(parts => (Ok: double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) & double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && double.IsFinite(x) && double.IsFinite(y), Point: new ShapePoint(x, y)))
            .Where(item => item.Ok)
            .Select(item => item.Point)
            .ToList();
        if (points.Count < 2) return ShapeOutline.Empty;
        List<ShapeSegment> segments = [new MoveSegment(points[0]), .. points.Skip(1).Select(point => new LineSegment(point))];
        if (closed) segments.Add(new CloseSegment());
        return new ShapeOutline([new ShapeFigure(segments)]);
    }

    public ObjectElement? Read(XElement node, string id, IReadOnlyDictionary<string, XElement>? definitions = null)
    {
        if (Outline(node) is not { } outline) return null;

        var styleName = (string?)node.Attribute(Draw + "style-name");
        var style = definitions is null ? ShapeStyle.Default : styles.Read(styleName is not null && definitions.TryGetValue(styleName, out var found) ? found : null, definitions);
        var path = FreeformPath.FromOutline(outline) with { Filled = style.Fill.Kind != ShapeFillKind.None && node.Name != Draw + "polyline" && node.Name != Draw + "line" };
        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(node),
            Content = new FreeformObject
            {
                Id = id,
                Path = path,
                Style = style,
                StyleName = styleName is not null && definitions is not null && definitions.TryGetValue(styleName, out var automatic)
                    ? (string?)automatic.Attribute(Style + "parent-style-name")
                    : null,
            },
        };
    }
}

internal sealed class OdfViewBox
{
    private readonly double _left;
    private readonly double _top;
    private readonly double _width;
    private readonly double _height;

    public OdfViewBox(XElement node, double fallback)
    {
        var parts = ((string?)node.Attribute(OdfPackage.Namespaces.Svg + "viewBox"))?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        var values = parts?.Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : double.NaN).ToArray();
        var valid = values is { Length: 4 } && values.All(double.IsFinite) && values[2] > 0 && values[3] > 0;
        (_left, _top, _width, _height) = valid ? (values![0], values[1], values[2], values[3]) : (0, 0, fallback, fallback);
    }

    public ShapeOutline Normalize(ShapeOutline outline) =>
        new SegmentMapper(point => new ShapePoint((point.X - _left) / _width, (point.Y - _top) / _height), 1 / _width, 1 / _height).Map(outline);
}

internal sealed class DeckShapeCodec(IShapePresets presets, OdfShapeStyleReader styles, IOdfParagraphText paragraphs, IArcFlattener? flattener = null, IShapeGeometry? geometry = null) : DeckObjectCodec
{
    private readonly EnhancedPathWriter _path = new();
    private readonly IShapeGeometry _geometry = geometry ?? new ShapeGeometry();
    private readonly IArcFlattener _flattener = flattener ?? new ArcFlattener();
    private readonly OdfArcGeometry _arcs = new();
    private readonly OdfLiteralEnhancedPath _literal = new(presets);
    private readonly OdfComputedGeometry _computed = new();
    private readonly OdfShapeTextRuns _runs = new(paragraphs);
    private readonly OdfDimensionLines _dimensions = new();

    public XElement Build(ObjectElement element, ShapeObject shape, string styleName, IOdfTextStyleSink textStyles)
    {
        if (shape.Dimension is { } dimension) return _dimensions.Build(element.Bounds, _dimensions.Placed(element, shape, dimension), styleName, element.Id);
        var node = Styled(Draw + "custom-shape", element, styleName);
        if (!string.IsNullOrEmpty(shape.Text) || _runs.Formatted(shape)) node.Add(_runs.Build(shape, textStyles));

        var geometry = new XElement(Draw + "enhanced-geometry",
            new XAttribute(Draw + "type", "ooxml-" + presets.OoxmlName(shape.Shape)),
            new XAttribute(Svg + "viewBox", $"0 0 {EnhancedPathWriter.ViewBox} {EnhancedPathWriter.ViewBox}"),
            new XAttribute(Draw + "enhanced-path", _path.Write(_flattener.Flatten(_geometry.Outline(shape)))));
        if (shape.FlipHorizontal) geometry.Add(new XAttribute(Draw + "mirror-horizontal", "true"));
        if (shape.FlipVertical) geometry.Add(new XAttribute(Draw + "mirror-vertical", "true"));
        _arcs.Write(geometry, shape);
        new OdfExtrusion().Write(geometry, shape.Style.Extrusion);
        _computed.Write(geometry, shape, element.Bounds.Width, element.Bounds.Height);
        node.Add(geometry);
        return node;
    }

    private ObjectElement Freeform(XElement node, string id, ShapeOutline outline, string? styleName, IReadOnlyDictionary<string, XElement> definitions)
    {
        var style = styles.Read(styleName is not null && definitions.TryGetValue(styleName, out var found) ? found : null, definitions);
        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(node),
            Content = new FreeformObject
            {
                Id = id,
                Path = FreeformPath.FromOutline(outline) with { Filled = style.Fill.Kind != ShapeFillKind.None },
                Style = style,
                StyleName = styleName is not null && definitions.TryGetValue(styleName, out var automatic)
                    ? (string?)automatic.Attribute(Style + "parent-style-name")
                    : null,
            },
        };
    }

    public static readonly IReadOnlyList<XName> Primitives = [Draw + "rect", Draw + "ellipse", Draw + "circle", Draw + "line", Draw + "connector", Draw + "measure"];

    public ObjectElement? ReadPrimitive(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions)
    {
        if (Read(node, id, definitions) is not { Content: ShapeObject shape } element) return null;
        if (_dimensions.Is(node))
        {
            var (bounds, dimension) = _dimensions.Read(node, shape, name => definitions.TryGetValue(name, out var style) ? style : null);
            return element with { Bounds = bounds, Content = dimension };
        }
        if (node.Name != Draw + "line" && node.Name != Draw + "connector")
        {
            var rounded = node.Name == Draw + "rect" && Centimeters((string?)node.Attribute(Draw + "corner-radius") ?? "0") > 0;
            return element with
            {
                Content = node.Name == Draw + "rect"
                    ? shape with { Shape = rounded ? ShapeKind.RoundedRectangle : ShapeKind.Rectangle }
                    : _arcs.FromEllipse(shape, node),
            };
        }

        var length = new OdfLength();
        var x1 = length.Parse((string?)node.Attribute(Svg + "x1"), Twip.Zero).Value;
        var y1 = length.Parse((string?)node.Attribute(Svg + "y1"), Twip.Zero).Value;
        var x2 = length.Parse((string?)node.Attribute(Svg + "x2"), Twip.Zero).Value;
        var y2 = length.Parse((string?)node.Attribute(Svg + "y2"), Twip.Zero).Value;
        return element with
        {
            Bounds = new DocumentRect(new Twip(Math.Min(x1, x2)), new Twip(Math.Min(y1, y2)), new Twip(Math.Abs(x2 - x1)), new Twip(Math.Abs(y2 - y1))),
            Content = shape with { Shape = ShapeKind.Line, FlipHorizontal = x2 < x1, FlipVertical = y2 < y1 },
        };
    }

    public ObjectElement? Read(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions)
    {
        var geometry = node.Element(Draw + "enhanced-geometry");
        var text = paragraphs.Read(node);
        var styleName = (string?)node.Attribute(Draw + "style-name");
        if (string.IsNullOrEmpty(text) && _literal.Unknown(node) && _literal.Outline(node) is { } outline)
            return Freeform(node, id, outline, styleName, definitions);

        var bounds = Bounds(node);
        return new ObjectElement
        {
            Id = id,
            Bounds = bounds,
            Content = _computed.Apply(_arcs.Read(_runs.Read(node, new ShapeObject
            {
                Id = id,
                Shape = presets.KindOf((string?)geometry?.Attribute(Draw + "type")),
                Text = string.IsNullOrEmpty(text) ? null : text,
                Style = new OdfExtrusion().Read(geometry, styles.Read(styleName is not null && definitions.TryGetValue(styleName, out var found) ? found : null, definitions)),
                FlipHorizontal = (string?)geometry?.Attribute(Draw + "mirror-horizontal") == "true",
                FlipVertical = (string?)geometry?.Attribute(Draw + "mirror-vertical") == "true",
                StyleName = styleName is not null && definitions.TryGetValue(styleName, out var automatic)
                    ? (string?)automatic.Attribute(Style + "parent-style-name")
                    : null,
            }, (name, family) => definitions.TryGetValue(name, out var text) && (string?)text.Attribute(Style + "family") == family ? text : null,
                _runs.Lists(definitions.Where(pair => pair.Key.StartsWith(OdfShapeTextLists.KeyPrefix, StringComparison.Ordinal)).Select(pair => pair.Value))), geometry), geometry, bounds.Width, bounds.Height),
        };
    }
}

internal sealed class DeckImageCodec(OdfImageTypes types, VectorFallback vectors) : DeckObjectCodec
{
    public DeckImageCodec(OdfImageTypes types) : this(types, new VectorFallback())
    {
    }

    public XElement BuildVector(ObjectElement element, string svgHref, string fallbackHref, string? styleName)
    {
        var svg = Embedded(Draw + "image", svgHref);
        svg.SetAttributeValue(Draw + "mime-type", vectors.MediaType);
        var fallback = Embedded(Draw + "image", fallbackHref);
        fallback.SetAttributeValue(Draw + "mime-type", vectors.FallbackMediaType);
        var frame = Placed(Draw + "frame", element, svg);
        frame.Add(fallback);
        if (styleName is not null) frame.Add(new XAttribute(Draw + "style-name", styleName));
        return frame;
    }

    private ObjectElement? Vector(XElement frame, string id, Func<string, byte[]?> readBytes, IReadOnlyDictionary<string, XElement>? definitions)
    {
        foreach (var image in frame.Elements(Draw + "image"))
        {
            if ((string?)image.Attribute(Xlink + "href") is not { Length: > 0 } href || new PulseOffice.Formats.LinkedFiles().IsExternal(href)) continue;
            if (readBytes(Hrefs.Normalize(href)) is not { } data || !vectors.IsSvg(data, (string?)image.Attribute(Draw + "mime-type"), href)) continue;
            var styleName = (string?)frame.Attribute(Draw + "style-name");
            var mirror = styleName is not null && definitions is not null && definitions.TryGetValue(styleName, out var style)
                ? (string?)style.Element(Style + "graphic-properties")?.Attribute(Style + "mirror") ?? string.Empty
                : string.Empty;
            var tokens = mirror.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return new ObjectElement
            {
                Id = id,
                Bounds = Bounds(frame),
                Content = vectors.Read(id, data, tokens.Any(token => token.StartsWith("horizontal", StringComparison.Ordinal)), tokens.Contains("vertical")),
            };
        }
        return null;
    }

    public XElement Build(ObjectElement element, string href, string? styleName)
    {
        var frame = Placed(Draw + "frame", element, Embedded(Draw + "image", href));
        if (styleName is not null) frame.Add(new XAttribute(Draw + "style-name", styleName));
        if (element.Content is RasterImageObject raster
            && new OdfImageMapCodec().Build(raster.Map, new DocumentSize(element.Bounds.Width, element.Bounds.Height)) is { } map)
            frame.Add(map);
        return frame;
    }

    public ObjectElement? Read(XElement frame, string id, Func<string, byte[]?> readBytes, IReadOnlyDictionary<string, XElement>? definitions)
    {
        if (Vector(frame, id, readBytes, definitions) is { } vector) return vector;
        if (Source(frame, readBytes) is not var (href, linkPath, data, mediaType)) return null;

        var bounds = Bounds(frame);
        var filter = ImageFilter.None;
        (double, double, double, double) crop = default;

        var styleName = (string?)frame.Attribute(Draw + "style-name");
        if (styleName is not null && definitions is not null && definitions.TryGetValue(styleName, out var style)
            && style.Element(Style + "graphic-properties") is { } graphic)
        {
            if ((string?)graphic.Attribute(Draw + "color-mode") == "greyscale") filter = ImageFilter.Grayscale;
            if ((string?)graphic.Attribute(OdfPackage.Namespaces.Fo + "clip") is { } clip) crop = Clip(clip, bounds);
        }

        return new ObjectElement
        {
            Id = id,
            Bounds = bounds,
            Content = new RasterImageObject
            {
                Id = id,
                Data = data,
                MediaType = mediaType,
                LinkPath = linkPath,
                NaturalSize = new DocumentSize(bounds.Width, bounds.Height),
                Filter = filter,
                Crop = crop,
                Map = new OdfImageMapCodec().Read(frame, new DocumentSize(bounds.Width, bounds.Height)),
            },
        };
    }

    private (string Href, string? LinkPath, byte[] Data, string MediaType)? Source(XElement frame, Func<string, byte[]?> readBytes)
    {
        var links = new PulseOffice.Formats.LinkedFiles();
        (string Href, string? LinkPath, byte[] Data, string MediaType)? first = null;
        foreach (var image in frame.Elements(Draw + "image"))
        {
            if ((string?)image.Attribute(Xlink + "href") is not { Length: > 0 } href) continue;
            var linkPath = links.IsExternal(href) ? links.ToPath(links.FromPackageHref(href)) : null;
            var data = linkPath is null ? readBytes(Hrefs.Normalize(href)) : [];
            if (data is null) continue;

            var detected = _header.MediaType(data);
            var declared = (string?)image.Attribute(Draw + "mime-type");
            var candidate = (href, linkPath, data, detected ?? (declared is { Length: > 0 } ? declared : types.MediaTypeOf(href)));
            if (linkPath is not null || detected is not null) return candidate;
            first ??= candidate;
        }
        return first;
    }

    private readonly ImageHeader _header = new();

    private (double Left, double Top, double Right, double Bottom) Clip(string clip, DocumentRect bounds)
    {
        var open = clip.IndexOf('(');
        var close = clip.IndexOf(')');
        if (open < 0 || close < 0) return default;

        var parts = clip[(open + 1)..close].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return default;

        double Twips(string part) => Centimeters(part) * PulseOffice.Foundation.Units.Twip.PerCentimeter;
        double width = bounds.Width.Value, height = bounds.Height.Value;
        return (width > 0 ? Twips(parts[3]) / width : 0, height > 0 ? Twips(parts[0]) / height : 0,
            width > 0 ? Twips(parts[1]) / width : 0, height > 0 ? Twips(parts[2]) / height : 0);
    }
}

internal sealed class DeckFormulaCodec(PulseOffice.Formulas.IMathMlSyntax mathml) : DeckObjectCodec
{
    public DeckFormulaCodec() : this(new PulseOffice.Formulas.MathMlSyntax())
    {
    }

    public XElement Build(ObjectElement element, FormulaObject formula, string href) =>
        Placed(Draw + "frame", element,
            Embedded(Draw + "object", href),
            new XElement(Draw + "image",
                new XAttribute(Draw + "mime-type", "application/vnd.oasis.opendocument.formula"),
                new XElement(Text + "p", formula.Expression)));

    public ObjectElement? Read(XElement frame, string id, Func<string, XDocument?> readXml)
    {
        if (frame.Element(Draw + "object") is not { } embedded) return null;

        var expression = frame.Element(Draw + "image")?.Element(Text + "p")?.Value;
        if (string.IsNullOrEmpty(expression) && (string?)embedded.Attribute(Xlink + "href") is { Length: > 0 } href
            && readXml($"{href.TrimStart('.', '/')}/content.xml")?.Root is { Name.LocalName: "math" } math)
            expression = mathml.Read(math).Source;
        return string.IsNullOrEmpty(expression)
            ? null
            : new ObjectElement { Id = id, Bounds = Bounds(frame), Content = new FormulaObject { Id = id, Expression = expression } };
    }
}

internal sealed class DeckOleCodec : DeckObjectCodec
{
    public XElement Build(ObjectElement element, PackagedObject package, string href, string? replacementHref) =>
        Placed(Draw + "frame", element,
            Embedded(Draw + "object-ole", href,
                new XAttribute(Draw + "mime-type", package.MediaType),
                new XAttribute(Draw + "name", package.FileName)),
            replacementHref is null ? null : Embedded(Draw + "image", replacementHref, new XAttribute(Draw + "mime-type", "image/png")));

    public ObjectElement? Read(XElement frame, string id, Func<string, byte[]?> readBytes)
    {
        var ole = frame.Element(Draw + "object-ole");
        if (ole is null || (string?)ole.Attribute(Xlink + "href") is not { } href) return null;

        var path = Hrefs.Normalize(href);
        if (readBytes(path) is not { } data) return null;

        var mediaType = (string?)ole.Attribute(Draw + "mime-type") ?? "application/octet-stream";
        var kind = mediaType.StartsWith("video/", StringComparison.Ordinal) || mediaType.StartsWith("audio/", StringComparison.Ordinal)
            ? PackagedObjectKind.Media
            : PackagedObjectKind.File;
        var bounds = Bounds(frame);
        var replacement = (string?)frame.Element(Draw + "image")?.Attribute(Xlink + "href") is { Length: > 0 } picture
            ? readBytes(Hrefs.Normalize(picture))
            : null;

        return new ObjectElement
        {
            Id = id,
            Bounds = bounds,
            Content = new PackagedObject
            {
                Id = id,
                Packaged = kind,
                Replacement = replacement,
                FileName = (string?)ole.Attribute(Draw + "name") ?? path.Split('/').Last(),
                MediaType = mediaType,
                Data = data,
                NaturalSize = new DocumentSize(bounds.Width, bounds.Height),
            },
        };
    }
}

public sealed record OdfTableStyles(
    IReadOnlyDictionary<string, CellVerticalAlign> CellAligns,
    IReadOnlyDictionary<string, double> ColumnWidths,
    IReadOnlyDictionary<string, double> RowHeights,
    IReadOnlyDictionary<string, XElement>? FrameStyles = null,
    IReadOnlyDictionary<string, XElement>? CellStyles = null);

internal sealed class DeckTableCodec(IOdfParagraphText paragraphs) : DeckObjectCodec
{
    private readonly OdfTableCellLooks _looks = new();

    private readonly OptimalColumnSizing _optimal = new(new PulseOffice.Document.Tables.ColumnWidths());

    private XElement Sized(XElement row, DataTableObject table, string prefix, int displayed, XElement automaticStyles)
    {
        var name = $"{prefix}_r{displayed}";
        automaticStyles.Add(SizeStyle(name, "table-row", "table-row-properties", "row-height",
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{TableMetrics.RowHeight(table, displayed):0.##}pt")));
        row.SetAttributeValue(Table + "style-name", name);
        return row;
    }

    private XElement SizeStyle(string name, string family, string properties, string attribute, string value) =>
        new(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", family),
            new XElement(Style + properties, new XAttribute(Style + attribute, value)));

    private string Centimetres(double value) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value:0.###}cm");

    private TableLayout ReadLayout(XElement table, IReadOnlyList<XElement> rows, OdfTableStyles styles)
    {
        var widths = table.Descendants(Table + "table-column")
            .SelectMany(column => Enumerable.Repeat(
                (string?)column.Attribute(Table + "style-name") is { } n && styles.ColumnWidths.TryGetValue(n, out var w) ? w : 0,
                Count(column, Table + "number-columns-repeated")))
            .Take(MostColumns)
            .ToList();
        var heights = rows
            .Select(row => (string?)row.Attribute(Table + "style-name") is { } n && styles.RowHeights.TryGetValue(n, out var h) ? h : 0)
            .ToList();

        var total = widths.Sum();
        return new TableLayout
        {
            ColumnFractions = widths.Count > 0 && widths.All(w => w > 0) ? [.. widths.Select(w => w / total)] : [],
            RowHeights = heights.Count > 0 && heights.All(h => h > 0) ? heights : [],
        };
    }

    public XElement Build(ObjectElement element, DataTableObject table, XElement automaticStyles)
    {
        var snapshot = table.Snapshot;
        var columns = Math.Max(1, snapshot.Columns.Count);

        var prefix = "tva_" + new string([.. element.Id.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')]);
        var body = new XElement(Table + "table",
            new XAttribute(Table + "use-first-row-styles", table.Style.HeaderRow ? "true" : "false"),
            new XAttribute(Table + "use-banding-rows-styles", table.Style.BandedRows ? "true" : "false"));

        var fractions = table.Layout.ColumnFractions.Count == snapshot.Columns.Count && snapshot.Columns.Count > 0
            ? table.Layout.ColumnFractions
            : _optimal.Apply(table).ColumnFractions;
        var widthCm = element.Bounds.Width.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter;
        for (var column = 0; column < columns; column++)
        {
            var fraction = column < fractions.Count ? fractions[column] : 1.0 / columns;
            var name = $"{prefix}_c{column}";
            automaticStyles.Add(SizeStyle(name, "table-column", "table-column-properties", "column-width",
                Centimetres(fraction * widthCm)));
            body.Add(new XElement(Table + "table-column", new XAttribute(Table + "style-name", name)));
        }

        var displayed = 0;
        var index = new TableCellIndex(table);
        if (table.Style.HeaderRow && snapshot.Columns.Count > 0)
            body.Add(Sized(BodyRow([.. snapshot.Columns.Select(column => (object?)column.Name)], TableCellFormat.HeaderRow, columns, table, index, prefix, automaticStyles),
                table, prefix, displayed++, automaticStyles));

        for (var row = 0; row < snapshot.Rows.Count; row++)
            body.Add(Sized(BodyRow(snapshot.Rows[row], row, columns, table, index, prefix, automaticStyles),
                table, prefix, displayed++, automaticStyles));

        var frameStyle = $"{prefix}_g";
        automaticStyles.Add(new XElement(Style + "style",
            new XAttribute(Style + "name", frameStyle),
            new XAttribute(Style + "family", "graphic"),
            new XElement(Style + "graphic-properties",
                new XAttribute(Table + "use-first-row-styles", table.Style.HeaderRow ? "true" : "false"),
                new XAttribute(Table + "use-banding-rows-styles", table.Style.BandedRows ? "true" : "false"))));
        var frame = Placed(Draw + "frame", element, body);
        frame.SetAttributeValue(Draw + "style-name", frameStyle);
        return frame;
    }

    public ObjectElement? Read(XElement frame, string id, OdfTableStyles styles)
    {
        var cellAligns = styles.CellAligns;
        var tableElement = frame.Element(Table + "table");
        if (tableElement is null) return null;
        var rows = ExpandedRows(tableElement).Take(MostRows).ToList();
        if (rows.Count == 0) return null;

        var settings = (string?)frame.Attribute(Draw + "style-name") is { } frameStyleName && styles.FrameStyles?.GetValueOrDefault(frameStyleName) is { } frameStyle
            ? frameStyle.Element(Style + "graphic-properties")
            : null;
        var header = HasHeader(settings, tableElement);
        var width = rows.Max(row => ExpandedCells(row).Count());
        rows = rows.Take(Math.Max(1, MostCells / Math.Max(1, width))).ToList();
        var columns = header
            ? ExpandedCells(rows[0]).Select(cell => new DataColumn(paragraphs.Read(cell), DataValueKind.Text)).ToList()
            : [.. Enumerable.Range(0, width).Select(_ => new DataColumn(string.Empty, DataValueKind.Text))];
        while (columns.Count < width) columns.Add(new DataColumn(string.Empty, DataValueKind.Text));
        var dataRows = new List<IReadOnlyList<object?>>();
        var merges = new List<TableMerge>();
        var formats = new List<TableCellFormat>();
        var looks = new List<TableCellFormat>();
        TableCellLook? LookOf(XElement cell) =>
            (string?)cell.Attribute(Table + "style-name") is { } name && styles.CellStyles?.GetValueOrDefault(name) is { } style ? _looks.Read(style) : null;
        CellVerticalAlign? AlignOf(XElement cell) =>
            (string?)cell.Attribute(Table + "style-name") is { } styleName && cellAligns.TryGetValue(styleName, out var found) ? found : null;
        if (header)
            foreach (var (cell, column) in ExpandedCells(rows[0]).Select((element, index) => (element, index)))
            {
                if (cell.Name == Table + "table-cell" && Spans(cell) is var (rowSpan, columnSpan) && (columnSpan > 1 || rowSpan > 1))
                    merges.Add(new TableMerge(TableCellFormat.HeaderRow, column, rowSpan, columnSpan));
                var align = AlignOf(cell);
                if (align is { } vertical) formats.Add(new TableCellFormat(TableCellFormat.HeaderRow, column, vertical));
                if (LookOf(cell) is { } look) looks.Add(new TableCellFormat(TableCellFormat.HeaderRow, column, align ?? CellVerticalAlign.Top) { Look = look });
            }

        foreach (var (rowElement, row) in rows.Skip(header ? 1 : 0).Select((element, index) => (element, index)))
        {
            var values = new List<object?>();
            var column = 0;
            foreach (var cell in ExpandedCells(rowElement))
            {
                if (cell.Name == Table + "table-cell")
                {
                    values.Add(paragraphs.Read(cell));
                    var (rowSpan, columnSpan) = Spans(cell);
                    if (columnSpan > 1 || rowSpan > 1) merges.Add(new TableMerge(row, column, rowSpan, columnSpan));
                    var align = AlignOf(cell);
                    if (align is { } vertical) formats.Add(new TableCellFormat(row, column, vertical));
                    if (LookOf(cell) is { } look) looks.Add(new TableCellFormat(row, column, align ?? CellVerticalAlign.Top) { Look = look });
                }
                else
                {
                    values.Add(string.Empty);
                }
                column++;
            }
            dataRows.Add(values);
        }

        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(frame),
            Content = new DataTableObject
            {
                Id = id,
                Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
                Snapshot = new DataSnapshot { Columns = columns, Rows = dataRows },
                Style = new DataTableStyle
                {
                    HeaderRow = header,
                    BandedRows = (string?)(settings?.Attribute(Table + "use-banding-rows-styles") ?? tableElement.Attribute(Table + "use-banding-rows-styles")) == "true",
                    OwnLook = looks.Count > 0,
                    Grid = looks.Count == 0,
                },
                Merges = merges,
                CellFormats = looks.Count > 0 ? looks : formats,
                Layout = ReadLayout(tableElement, rows, styles),
            },
        };
    }

    private const int MaxRepeat = 1000;

    private const int MostRows = ImportedTableLimits.SlideTableRows;

    private const int MostColumns = ImportedTableLimits.SlideTableColumns;

    private const int MostCells = ImportedTableLimits.SlideTableCells;

    private int Count(XElement element, XName attribute) =>
        int.TryParse((string?)element.Attribute(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 1, MaxRepeat) : 1;

    private (int Rows, int Columns) Spans(XElement cell) =>
        (Count(cell, Table + "number-rows-spanned"),
         Count(cell, Table + "number-columns-spanned"));

    private bool HasHeader(XElement? settings, XElement table) =>
        (string?)(settings?.Attribute(Table + "use-first-row-styles") ?? table.Attribute(Table + "use-first-row-styles")) == "true"
        || table.Elements(Table + "table-header-rows").Any();

    private const int MaximumRowGroupDepth = 64;

    private IEnumerable<XElement> ExpandedRows(XElement container, int depth = 0)
    {
        foreach (var child in container.Elements())
        {
            if (child.Name == Table + "table-row")
                for (var i = 0; i < Count(child, Table + "number-rows-repeated"); i++) yield return child;
            else if (depth < MaximumRowGroupDepth
                     && (child.Name == Table + "table-header-rows" || child.Name == Table + "table-rows" || child.Name == Table + "table-row-group"))
                foreach (var row in ExpandedRows(child, depth + 1)) yield return row;
        }
    }

    private IEnumerable<XElement> ExpandedCells(XElement row)
    {
        var emitted = 0;
        foreach (var cell in row.Elements().Where(e => e.Name == Table + "table-cell" || e.Name == Table + "covered-table-cell"))
            for (var i = 0; i < Count(cell, Table + "number-columns-repeated"); i++)
            {
                if (++emitted > MostColumns) yield break;
                yield return cell;
            }
    }

    private XElement Looked(XElement cell, TableCellFormat? format, int row, int column, string prefix, XElement automaticStyles)
    {
        var name = $"{prefix}_l{(row < 0 ? "h" : row.ToString(System.Globalization.CultureInfo.InvariantCulture))}_{column}";
        automaticStyles.Add(_looks.Write(name, format?.Look ?? TableCellLook.Plain, format?.VerticalAlign ?? CellVerticalAlign.Top));
        cell.SetAttributeValue(Table + "style-name", name);
        return cell;
    }

    private XElement BodyRow(IReadOnlyList<object?> cells, int row, int columns, DataTableObject table, TableCellIndex index, string prefix, XElement automaticStyles)
    {
        var element = new XElement(Table + "table-row");

        for (var column = 0; column < columns; column++)
        {
            if (index.Cover(row, column) is not null)
            {
                element.Add(new XElement(Table + "covered-table-cell"));
                continue;
            }

            var text = column < cells.Count ? cells[column]?.ToString() ?? string.Empty : string.Empty;
            var cell = new XElement(Table + "table-cell", paragraphs.Paragraphs(text));

            var format = index.Format(row, column);
            var align = format?.VerticalAlign ?? CellVerticalAlign.Top;
            if (table.Style.OwnLook)
                Looked(cell, format, row, column, prefix, automaticStyles);
            else if (align != CellVerticalAlign.Top)
            {
                var styleName = $"{prefix}_{(row < 0 ? "h" : row.ToString(System.Globalization.CultureInfo.InvariantCulture))}_{column}";
                automaticStyles.Add(new XElement(Style + "style",
                    new XAttribute(Style + "name", styleName),
                    new XAttribute(Style + "family", "table-cell"),
                    new XElement(Style + "table-cell-properties",
                        new XAttribute(Style + "vertical-align", align == CellVerticalAlign.Center ? "middle" : "bottom"))));
                cell.Add(new XAttribute(Table + "style-name", styleName));
            }

            if (index.Origin(row, column) is { } origin)
            {
                if (origin.ColumnSpan > 1) cell.Add(new XAttribute(Table + "number-columns-spanned", origin.ColumnSpan));
                if (origin.RowSpan > 1) cell.Add(new XAttribute(Table + "number-rows-spanned", origin.RowSpan));
            }

            element.Add(cell);
        }
        return element;
    }
}

internal sealed class DeckFontworkCodec(OdfShapeStyleReader styles) : DeckObjectCodec
{
    private readonly OdfFontworkGeometry _geometry = new();

    public XElement Build(ObjectElement element, PulseOffice.Drawing.Fontwork.FontworkObject fontwork, string styleName) =>
        Styled(Draw + "custom-shape", element, styleName,
            new XElement(Text + "p", fontwork.Text),
            _geometry.Geometry(fontwork.Warp));

    public bool Reads(XElement node) => _geometry.IsFontwork(node);

    public ObjectElement Read(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions)
    {
        var styleName = (string?)node.Attribute(Draw + "style-name");
        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(node),
            Content = new PulseOffice.Drawing.Fontwork.FontworkObject
            {
                Id = id,
                Text = _geometry.TextOf(node),
                Warp = _geometry.WarpOf(node),
                Style = styles.Read(styleName is not null && definitions.TryGetValue(styleName, out var found) ? found : null, definitions),
            },
        };
    }
}
