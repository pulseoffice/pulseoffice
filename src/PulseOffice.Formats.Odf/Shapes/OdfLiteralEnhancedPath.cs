using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfLiteralEnhancedPath(IShapePresets presets, OdfComputedGeometry computed)
{
    public OdfLiteralEnhancedPath(IShapePresets presets) : this(presets, new OdfComputedGeometry())
    {
    }

    private const string OwnPrefix = "pulseoffice-";
    private readonly SvgPathParser _parser = new();

    public bool Unknown(XElement customShape) => TypeOf(customShape) is not { } type || !type.StartsWith(OwnPrefix, StringComparison.Ordinal) && !presets.Knows(type) && !computed.Maps(type);

    public bool Approximated(XElement customShape) =>
        customShape.Name == Namespaces.Draw + "custom-shape" && Unknown(customShape) && Outline(customShape) is null
        && customShape.Element(Namespaces.Draw + "enhanced-geometry") is { } geometry && (string?)geometry.Attribute(Namespaces.Draw + "text-path") != "true" && !computed.Evaluates(customShape);

    public ShapeOutline? Outline(XElement customShape)
    {
        if (customShape.Element(Namespaces.Draw + "enhanced-geometry") is not { } geometry || Literal(geometry) is not { } data) return null;
        var outline = _parser.Parse(data);
        return outline.Figures.Count == 0 ? null : new OdfViewBox(geometry, EnhancedPathWriter.ViewBox).Normalize(outline);
    }

    private string? TypeOf(XElement element) =>
        (string?)element.Element(Namespaces.Draw + "enhanced-geometry")?.Attribute(Namespaces.Draw + "type");

    private string? Literal(XElement geometry)
    {
        var data = (string?)geometry.Attribute(Namespaces.Draw + "enhanced-path");
        if (string.IsNullOrWhiteSpace(data) || data.Any(character => character is '?' or '$' or '@')) return null;
        var tokens = data.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token is not ("N" or "F" or "S")).ToList();
        return tokens.All(token => token is "M" or "L" or "C" or "Z" || double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            ? string.Join(' ', tokens)
            : null;
    }
}
