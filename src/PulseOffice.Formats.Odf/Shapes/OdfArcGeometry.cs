using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfArcGeometry
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private const double FullTurn = 360;

    private readonly ArcAdjustments _arcs = new();

    public void Write(XElement geometry, ShapeObject shape)
    {
        if (!_arcs.Applies(shape.Shape)) return;
        var (first, second) = _arcs.ToOoxml(shape);
        geometry.SetAttributeValue(Draw + "modifiers", string.Create(CultureInfo.InvariantCulture, $"{first} {second}"));
    }

    public ShapeObject Read(ShapeObject shape, XElement? geometry)
    {
        if (!_arcs.Applies(shape.Shape)) return shape;
        var parts = ((string?)geometry?.Attribute(Draw + "modifiers"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        long? Part(int index) => index < parts.Length && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? (long)Math.Round(value) : null;
        var (start, end) = _arcs.FromOoxml(shape.Shape, Part(0), Part(1));
        return shape with { ArcStartDegrees = start, ArcEndDegrees = end };
    }

    public ShapeKind KindOfEllipse(XElement ellipse) => (string?)ellipse.Attribute(Draw + "kind") switch
    {
        "arc" => ShapeKind.Arc,
        "section" => ShapeKind.Pie,
        "cut" => ShapeKind.Chord,
        _ => ShapeKind.Ellipse,
    };

    public ShapeObject FromEllipse(ShapeObject shape, XElement ellipse)
    {
        var kind = KindOfEllipse(ellipse);
        if (!_arcs.Applies(kind)) return shape with { Shape = kind };
        var arc = shape with
        {
            Shape = kind,
            ArcStartDegrees = Degrees((string?)ellipse.Attribute(Draw + "start-angle"), 0),
            ArcEndDegrees = Degrees((string?)ellipse.Attribute(Draw + "end-angle"), FullTurn),
        };
        return kind is ShapeKind.Arc ? arc with { Style = arc.Style with { Fill = ShapeFill.None } } : arc;
    }

    private double Degrees(string? value, double fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim();
        var (number, factor) = text switch
        {
            _ when text.EndsWith("grad", StringComparison.Ordinal) => (text[..^4], 0.9),
            _ when text.EndsWith("deg", StringComparison.Ordinal) => (text[..^3], 1.0),
            _ when text.EndsWith("rad", StringComparison.Ordinal) => (text[..^3], 180 / Math.PI),
            _ => (text, 1.0),
        };
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed * factor) ? parsed * factor : fallback;
    }
}
