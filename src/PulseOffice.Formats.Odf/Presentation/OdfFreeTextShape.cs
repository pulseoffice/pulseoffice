using System.Xml.Linq;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfFreeTextShape
{
    public const string Type = "pulseoffice-text-box";

    public const string OutlinedType = "pulseoffice-text-outline";

    private readonly EnhancedPathWriter _paths = new();

    private readonly PulseOffice.Drawing.Shapes.SvgPathParser _parser = new();

    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public bool Applies(TextElement element) =>
        element.Text.Role == PlaceholderRole.Free
        && !element.Text.Frame.WordWrap
        && element.Text.Frame.Autofit != TextAutofit.ResizeShape;

    public bool Is(XElement node) =>
        node.Name == Draw + "custom-shape"
        && (string?)node.Element(Draw + "enhanced-geometry")?.Attribute(Draw + "type") is Type or OutlinedType;

    public bool Outlined(TextElement element) => element.Backdrop?.Outline is not null;

    public XElement Geometry(TextElement element) => element.Backdrop?.Outline is { } outline
        ? new(Draw + "enhanced-geometry",
            new XAttribute(OdfPackage.Namespaces.Svg + "viewBox", $"0 0 {EnhancedPathWriter.ViewBox} {EnhancedPathWriter.ViewBox}"),
            new XAttribute(Draw + "type", OutlinedType),
            new XAttribute(Draw + "enhanced-path", _paths.Write(outline.ToOutline())))
        : Geometry();

    public PulseOffice.Drawing.Freeform.FreeformPath? Outline(XElement node)
    {
        if (node.Element(Draw + "enhanced-geometry") is not { } geometry || (string?)geometry.Attribute(Draw + "type") != OutlinedType) return null;
        if ((string?)geometry.Attribute(Draw + "enhanced-path") is not { Length: > 0 } data) return null;
        var tokens = data.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token is not ("N" or "F" or "S")).ToList();
        var outline = _parser.Parse(string.Join(' ', tokens));
        return outline.Figures.Count == 0 ? null : PulseOffice.Drawing.Freeform.FreeformPath.FromOutline(new OdfViewBox(geometry, EnhancedPathWriter.ViewBox).Normalize(outline));
    }

    public XElement Geometry() =>
        new(Draw + "enhanced-geometry",
            new XAttribute(OdfPackage.Namespaces.Svg + "viewBox", "0 0 21600 21600"),
            new XAttribute(Draw + "type", Type),
            new XAttribute(Draw + "enhanced-path", "M 0 0 L 21600 0 21600 21600 0 21600 0 0 Z N"));
}
