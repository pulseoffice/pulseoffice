using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

public sealed class DrawingDimensionLines(IDimensionLineGeometry geometry)
{
    public const string Uri = "{5B0C3E1A-7D2F-4E8B-9A61-3C4D5E6F7A80}";
    public const string Namespace = "urn:pulseoffice:dimension-line";

    private const string DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string Prefix = "pd";
    private readonly OoxmlGeometry _custom = new();

    public DrawingDimensionLines() : this(new DimensionLineGeometry())
    {
    }

    public ShapeObject Fallback(ShapeObject shape, DocumentSize size)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Dimension is not { } dimension) return shape;
        var layout = geometry.Layout(dimension, size.Width.ToPoints(), size.Height.ToPoints());
        return shape with
        {
            NaturalSize = size,
            Text = shape.Text is { Length: > 0 } ? shape.Text : geometry.Label(dimension, layout.MeasuredLength, CultureInfo.InvariantCulture),
            Style = shape.Style with
            {
                Fill = ShapeFill.Default with { ColorHex = shape.Style.Line.ColorHex, Opacity = shape.Style.Line.Opacity },
                Line = shape.Style.Line with { StartArrow = ArrowEnd.None, EndArrow = ArrowEnd.None },
            },
        };
    }

    public A.CustomGeometry Geometry(ShapeObject fallback, DimensionLine dimension, ShapeLine line)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(line);
        var width = fallback.NaturalSize.Width.ToPoints();
        var height = fallback.NaturalSize.Height.ToPoints();
        var size = Math.Max(6, line.Width.ToPoints() * 4) * line.ArrowScale;
        var strokes = geometry.Outline(dimension, width, height);
        var heads = geometry.Heads(dimension, width, height, line.StartArrow, line.EndArrow, size);
        return _custom.CustomGeometry(new ShapeOutline([.. strokes.Figures, .. heads.Figures]));
    }

    public void Write(OpenXmlCompositeElement properties, ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Dimension is not { } dimension) return;
        var value = new OpenXmlUnknownElement(Prefix, "dimension", Namespace);
        void Set(string name, string text) => value.SetAttribute(new OpenXmlAttribute(name, string.Empty, text));
        Set("sx", Number(dimension.Start.X));
        Set("sy", Number(dimension.Start.Y));
        Set("ex", Number(dimension.End.X));
        Set("ey", Number(dimension.End.Y));
        Set("lineDistance", Twips(dimension.LineDistance));
        Set("guideOverhang", Twips(dimension.GuideOverhang));
        Set("guideDistance", Twips(dimension.GuideDistance));
        Set("startGuide", Twips(dimension.StartGuide));
        Set("endGuide", Twips(dimension.EndGuide));
        Set("placing", dimension.Placing.ToString());
        Set("parallel", dimension.Parallel ? "1" : "0");
        Set("unit", dimension.Unit);
        Set("showUnit", dimension.ShowUnit ? "1" : "0");
        Set("decimalPlaces", dimension.DecimalPlaces.ToString(CultureInfo.InvariantCulture));
        Set("align", dimension.HorizontalAlignment);
        Set("verticalAlign", dimension.VerticalAlignment);
        Set("startArrow", shape.Style.Line.StartArrow.ToString());
        Set("endArrow", shape.Style.Line.EndArrow.ToString());
        Set("fill", shape.Style.Fill.IsVisible ? shape.Style.Fill.ColorHex : "none");
        if (dimension.Label is { } label) Set("label", label);

        var extension = new OpenXmlUnknownElement("a", "ext", DrawingNamespace);
        extension.SetAttribute(new OpenXmlAttribute("uri", string.Empty, Uri));
        extension.AppendChild(value);
        var list = properties.ChildElements.FirstOrDefault(child => child.LocalName == "extLst" && child.NamespaceUri == DrawingNamespace);
        if (list is null) properties.AppendChild(list = new A.ShapePropertiesExtensionList());
        list.AppendChild(extension);
    }

    public bool Carries(OpenXmlElement? properties) => Element(properties) is not null;

    public ShapeObject Restore(ShapeObject shape, OpenXmlElement? properties)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (Element(properties) is not { } value) return shape;
        string? Get(string name) => value.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name).Value;
        double Fraction(string name) => double.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? Math.Clamp(number, 0, 1) : 0;
        Twip Length(string name, Twip fallback) =>
            int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var twips) ? new Twip(Math.Clamp(twips, -1_000_000, 1_000_000)) : fallback;
        TEnum Choice<TEnum>(string name, TEnum fallback) where TEnum : struct, Enum =>
            Enum.TryParse<TEnum>(Get(name), out var parsed) && Enum.IsDefined(parsed) ? parsed : fallback;

        var defaults = new DimensionLine { Start = default, End = default };
        var dimension = defaults with
        {
            Start = new ShapePoint(Fraction("sx"), Fraction("sy")),
            End = new ShapePoint(Fraction("ex"), Fraction("ey")),
            LineDistance = Length("lineDistance", defaults.LineDistance),
            GuideOverhang = Length("guideOverhang", defaults.GuideOverhang),
            GuideDistance = Length("guideDistance", defaults.GuideDistance),
            StartGuide = Length("startGuide", defaults.StartGuide),
            EndGuide = Length("endGuide", defaults.EndGuide),
            Placing = Choice("placing", defaults.Placing),
            Parallel = Get("parallel") != "0",
            Unit = Get("unit") is { } unit && geometry.IsKnownUnit(unit) ? unit : defaults.Unit,
            ShowUnit = Get("showUnit") == "1",
            DecimalPlaces = int.TryParse(Get("decimalPlaces"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var places) ? Math.Clamp(places, 0, 10) : defaults.DecimalPlaces,
            HorizontalAlignment = Get("align") is { Length: > 0 } align ? align : defaults.HorizontalAlignment,
            VerticalAlignment = Get("verticalAlign") is { Length: > 0 } vertical ? vertical : defaults.VerticalAlignment,
            Label = Get("label"),
        };
        var fill = Get("fill");
        return shape with
        {
            Shape = ShapeKind.Line,
            Computed = null,
            Text = null,
            Runs = null,
            Dimension = dimension,
            Style = shape.Style with
            {
                Fill = fill is null or "none" ? ShapeFill.None : shape.Style.Fill with { ColorHex = fill },
                Line = shape.Style.Line with
                {
                    StartArrow = Choice("startArrow", ArrowEnd.None),
                    EndArrow = Choice("endArrow", ArrowEnd.None),
                },
            },
        };
    }

    private OpenXmlElement? Element(OpenXmlElement? properties) =>
        properties?.ChildElements.FirstOrDefault(child => child.LocalName == "extLst")?
            .Descendants().FirstOrDefault(element => element.LocalName == "dimension" && element.NamespaceUri == Namespace);

    private string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private string Twips(Twip value) => value.Value.ToString(CultureInfo.InvariantCulture);
}
