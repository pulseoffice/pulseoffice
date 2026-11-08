using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfTextBackdrops(OdfShapeStyleReader styles)
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public TextBackdrop? Read(XElement frame, IReadOnlyDictionary<string, XElement> definitions, IReadOnlyDictionary<string, XElement>? presentation = null)
    {
        XElement? style = null;
        if ((string?)frame.Attribute(Draw + "style-name") is { } name) definitions.TryGetValue(name, out style);
        if (style is null && (string?)frame.Attribute(OdfPackage.Namespaces.Presentation + "style-name") is { } named) presentation?.TryGetValue(named, out style);
        if (style is null) return null;
        var graphic = style.Element(Style + "graphic-properties");
        var filled = (string?)graphic?.Attribute(Draw + "fill") is { } fill && fill != "none";
        var stroked = (string?)graphic?.Attribute(Draw + "stroke") is { } stroke && stroke != "none";
        if (!filled && !stroked) return null;
        var read = styles.Read(style, definitions);
        return new TextBackdrop(ShapeKind.Rectangle, filled ? read.Fill : ShapeFill.None, stroked ? read.Line : read.Line with { Dash = ShapeStrokeStyle.None });
    }

    public void Write(XElement graphic, TextBackdrop backdrop, string name, OdfShapeStyleMapper mapper)
    {
        var built = mapper.Build(name, ShapeStyle.Default with { Fill = backdrop.Fill, Line = backdrop.Line }).Element(Style + "graphic-properties");
        foreach (var attribute in built?.Attributes() ?? [])
            if (attribute.Name.Namespace == Draw || attribute.Name.Namespace == OdfPackage.Namespaces.Svg || attribute.Name.LocalName == "repeat")
                graphic.SetAttributeValue(attribute.Name, attribute.Value);
    }
}
