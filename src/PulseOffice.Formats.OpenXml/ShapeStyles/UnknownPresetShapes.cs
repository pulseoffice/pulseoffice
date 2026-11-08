using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

public sealed class UnknownPresetShapes(IShapePresets presets, PulseOffice.Drawing.Shapes.Geometry.IPresetGeometryEngine engine)
{
    public UnknownPresetShapes(IShapePresets presets) : this(presets, new PulseOffice.Drawing.Shapes.Geometry.PresetGeometryEngine())
    {
    }

    private readonly HashSet<string> _shapes = new(StringComparer.Ordinal) { "sp", "wsp" };

    public bool Present(OpenXmlElement? root) =>
        root?.Descendants<A.PresetGeometry>().Any(geometry => geometry.Parent?.Parent is { } shape && _shapes.Contains(shape.LocalName)
            && geometry.Preset?.InnerText is { Length: > 0 } preset && !presets.Knows(preset) && !engine.Knows(preset)) == true;
}
