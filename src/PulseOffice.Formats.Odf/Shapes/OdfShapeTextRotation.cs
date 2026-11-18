using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfShapeTextRotation
{
    private readonly OdfTextLayouts _layouts = new();

    public void Write(XElement geometry, ShapeObject shape) => _layouts.WriteGeometryRotation(geometry, shape.Style.Text.RotationDegrees);

    public ShapeObject Read(ShapeObject shape, XElement? geometry) =>
        Math.Abs(shape.Style.Text.RotationDegrees) < 0.001 && _layouts.GeometryRotation(geometry) is { } degrees and > 0.001
            ? shape with { Style = shape.Style with { Text = shape.Style.Text with { RotationDegrees = degrees } } }
            : shape;
}
