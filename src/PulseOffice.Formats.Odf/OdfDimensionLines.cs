using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfDimensionLines(IDimensionLineGeometry geometry)
{
    private const int MaximumStyleDepth = 16;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;

    private readonly OdfLength _length = new();

    public OdfDimensionLines() : this(new DimensionLineGeometry())
    {
    }

    public XName Element => Draw + "measure";

    public bool Is(XElement node) => node.Name == Element;

    public (DocumentRect Bounds, ShapeObject Shape) Read(XElement node, ShapeObject shape, Func<string, XElement?> styles)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(shape);
        var first = new ShapePoint(Coordinate(node, "x1"), Coordinate(node, "y1"));
        var second = new ShapePoint(Coordinate(node, "x2"), Coordinate(node, "y2"));
        string? Property(string name) => Lookup(styles((string?)node.Attribute(Draw + "style-name") ?? string.Empty), styles, Draw + name, 0);
        Twip Length(string name, Twip fallback) => _length.Parse(Property(name), fallback);

        var defaults = new DimensionLine { Start = default, End = default };
        var fields = node.Descendants(Text + "measure").ToList();
        var unitField = fields.FirstOrDefault(field => (string?)field.Attribute(Text + "kind") == "unit")?.Value.Trim();
        var unit = Property("unit") is { } declared && geometry.IsKnownUnit(declared)
            ? declared
            : unitField is { } field && geometry.IsKnownUnit(field) ? field : DimensionLine.AutomaticUnit;
        var dimension = defaults with
        {
            LineDistance = Length("line-distance", defaults.LineDistance),
            GuideOverhang = Length("guide-overhang", defaults.GuideOverhang),
            GuideDistance = Length("guide-distance", defaults.GuideDistance),
            StartGuide = Length("start-guide", defaults.StartGuide),
            EndGuide = Length("end-guide", defaults.EndGuide),
            Placing = Property("placing") == "below" ? DimensionPlacing.Below : DimensionPlacing.Above,
            Parallel = Property("parallel") != "false",
            Unit = unit,
            ShowUnit = Property("show-unit") is { } show ? show == "true" : unitField is { Length: > 0 },
            DecimalPlaces = int.TryParse(Property("decimal-places"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var places) ? Math.Clamp(places, 0, 10) : defaults.DecimalPlaces,
            HorizontalAlignment = Property("measure-align") ?? DimensionLine.AutomaticAlignment,
            VerticalAlignment = Property("measure-vertical-align") ?? DimensionLine.AutomaticAlignment,
            Label = ExplicitLabel(node),
        };

        var frame = geometry.Frame(first, second, dimension);
        var bounds = new DocumentRect(Twip.FromPoints(frame.X), Twip.FromPoints(frame.Y), Twip.FromPoints(frame.Width), Twip.FromPoints(frame.Height));
        return (bounds, shape with
        {
            Shape = ShapeKind.Line,
            Text = null,
            Runs = null,
            Computed = null,
            FlipHorizontal = false,
            FlipVertical = false,
            NaturalSize = new DocumentSize(bounds.Width, bounds.Height),
            Dimension = dimension with { Start = frame.Start, End = frame.End },
        });
    }

    public OdfPlacedDimension Placed(DimensionLine line, DocumentSize size, double rotationDegrees, bool flipHorizontal, bool flipVertical)
    {
        ArgumentNullException.ThrowIfNull(line);
        var width = size.Width.ToPoints();
        var height = size.Height.ToPoints();
        var fitted = geometry.Fitted(line, width, height);
        var radians = double.IsFinite(rotationDegrees) ? rotationDegrees * Math.PI / 180 : 0;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        ShapePoint Turned(ShapePoint relative)
        {
            var point = relative.Scaled(width, height);
            var x = (flipHorizontal ? width - point.X : point.X) - width / 2;
            var y = (flipVertical ? height - point.Y : point.Y) - height / 2;
            return new ShapePoint(width / 2 + x * cos - y * sin, height / 2 + x * sin + y * cos);
        }
        var mirrored = flipHorizontal != flipVertical;
        var placing = mirrored ? fitted.Placing is DimensionPlacing.Above ? DimensionPlacing.Below : DimensionPlacing.Above : fitted.Placing;
        return new OdfPlacedDimension(fitted with { Placing = placing }, Turned(line.Start), Turned(line.End));
    }

    public OdfPlacedDimension Placed(PulseOffice.Presentations.ObjectElement element, ShapeObject shape, DimensionLine line)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(shape);
        return Placed(line, new DocumentSize(element.Bounds.Width, element.Bounds.Height), element.RotationDegrees + shape.RotationDegrees, shape.FlipHorizontal, shape.FlipVertical);
    }

    public XElement Build(DocumentRect bounds, OdfPlacedDimension placed, string styleName, string id, params object[] extra)
    {
        ArgumentNullException.ThrowIfNull(placed);
        var left = bounds.Left.ToPoints();
        var top = bounds.Top.ToPoints();
        return new XElement(Element,
            new XAttribute(Draw + "style-name", styleName),
            new XAttribute(OdfPackage.Namespaces.Xml + "id", id),
            new XAttribute(Draw + "id", id),
            new XAttribute(Svg + "x1", Cm(left + placed.Start.X)),
            new XAttribute(Svg + "y1", Cm(top + placed.Start.Y)),
            new XAttribute(Svg + "x2", Cm(left + placed.End.X)),
            new XAttribute(Svg + "y2", Cm(top + placed.End.Y)),
            extra,
            Paragraphs(placed.Line, geometry.Layout(placed.Start, placed.End, placed.Line).MeasuredLength));
    }

    public void WriteStyle(XElement style, OdfPlacedDimension placed)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(placed);
        var dimension = placed.Line;
        var properties = style.Element(Style + "graphic-properties");
        if (properties is null) style.Add(properties = new XElement(Style + "graphic-properties"));
        properties.SetAttributeValue(Draw + "line-distance", Cm(dimension.LineDistance.ToPoints()));
        properties.SetAttributeValue(Draw + "guide-overhang", Cm(dimension.GuideOverhang.ToPoints()));
        properties.SetAttributeValue(Draw + "guide-distance", Cm(dimension.GuideDistance.ToPoints()));
        properties.SetAttributeValue(Draw + "start-guide", Cm(dimension.StartGuide.ToPoints()));
        properties.SetAttributeValue(Draw + "end-guide", Cm(dimension.EndGuide.ToPoints()));
        properties.SetAttributeValue(Draw + "placing", dimension.Placing is DimensionPlacing.Below ? "below" : "above");
        properties.SetAttributeValue(Draw + "parallel", dimension.Parallel ? "true" : "false");
        properties.SetAttributeValue(Draw + "measure-align", dimension.HorizontalAlignment);
        properties.SetAttributeValue(Draw + "measure-vertical-align", dimension.VerticalAlignment);
        properties.SetAttributeValue(Draw + "unit", dimension.Unit);
        properties.SetAttributeValue(Draw + "show-unit", dimension.ShowUnit ? "true" : "false");
        properties.SetAttributeValue(Draw + "decimal-places", dimension.DecimalPlaces.ToString(CultureInfo.InvariantCulture));
    }

    private IEnumerable<XElement> Paragraphs(DimensionLine dimension, double measuredPoints)
    {
        if (dimension.Label is { } label) return label.Split('\n').Select(line => new XElement(Text + "p", line)).ToList();
        var paragraph = new XElement(Text + "p",
            new XElement(Text + "measure", new XAttribute(Text + "kind", "value"),
                geometry.Label(dimension with { ShowUnit = false }, measuredPoints, CultureInfo.InvariantCulture)));
        if (dimension.ShowUnit)
            paragraph.Add(new XElement(Text + "measure", new XAttribute(Text + "kind", "unit"),
                dimension.Unit == DimensionLine.AutomaticUnit ? "cm" : dimension.Unit));
        return [paragraph];
    }

    private string? ExplicitLabel(XElement node)
    {
        var paragraphs = node.Elements(Text + "p").ToList();
        if (paragraphs.Count == 0) return null;
        var free = string.Concat(paragraphs.SelectMany(paragraph => paragraph.DescendantNodes()).OfType<XText>()
            .Where(text => text.Ancestors(Text + "measure").FirstOrDefault() is null)
            .Select(text => text.Value));
        return string.IsNullOrWhiteSpace(free) ? null : string.Join('\n', paragraphs.Select(paragraph => paragraph.Value));
    }

    private string? Lookup(XElement? style, Func<string, XElement?> styles, XName name, int depth)
    {
        if (style is null || depth > MaximumStyleDepth) return null;
        if ((string?)style.Element(Style + "graphic-properties")?.Attribute(name) is { } value) return value;
        return (string?)style.Attribute(Style + "parent-style-name") is { Length: > 0 } parent
            ? Lookup(styles(parent), styles, name, depth + 1)
            : null;
    }

    private double Coordinate(XElement node, string name) => _length.Parse((string?)node.Attribute(Svg + name), Twip.Zero).ToPoints();

    private string Cm(double points) => (points * 2.54 / 72).ToString("0.####", CultureInfo.InvariantCulture) + "cm";
}

internal sealed record OdfPlacedDimension(DimensionLine Line, ShapePoint Start, ShapePoint End);
