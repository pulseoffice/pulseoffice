using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpShapeTextLayout
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;

    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private readonly PulseOffice.Text.Rich.EastAsianUprightText _upright = new();

    public Diagnostic Loss { get; } = Diagnostic.Warning("ODP217",
        "Some shapes have East Asian vertical text: Deck turns its East Asian characters with the line instead of keeping them upright.");

    public bool Present(XContainer content)
    {
        var vertical = content.Descendants(Style + "style")
            .Where(style => style.Element(Style + "graphic-properties")?.Attribute(Style + "writing-mode")?.Value is "tb-rl" or "tb-lr" or "tb")
            .Select(style => (string?)style.Attribute(Style + "name"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        return vertical.Count > 0 && content.Descendants()
            .Any(element => (string?)element.Attribute(Draw + "style-name") is { } name && vertical.Contains(name) && _upright.Contains(element.Value));
    }
}
