using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf;

public sealed class OdfExtrusion
{
    private const double MaximumDepthCentimeters = 100;

    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    public void Write(XElement geometry, ShapeExtrusion extrusion)
    {
        if (!extrusion.Enabled) return;
        geometry.SetAttributeValue(Draw + "extrusion", "true");
        geometry.SetAttributeValue(Draw + "extrusion-depth",
            string.Create(CultureInfo.InvariantCulture, $"{extrusion.Depth.ToCentimeters():0.###}cm 0"));
    }

    public ShapeStyle Read(XElement? geometry, ShapeStyle style)
    {
        if ((string?)geometry?.Attribute(Draw + "extrusion") != "true") return style;
        var depth = ((string?)geometry!.Attribute(Draw + "extrusion-depth"))?.Split(' ')[0];
        var centimeters = depth is not null && depth.EndsWith("cm", StringComparison.Ordinal)
            && double.TryParse(depth[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is > 0 and <= MaximumDepthCentimeters ? value : 1;
        return style with { Extrusion = new ShapeExtrusion { Kind = ExtrusionKind.Extrude, Depth = Twip.FromCentimeters(centimeters) } };
    }
}
