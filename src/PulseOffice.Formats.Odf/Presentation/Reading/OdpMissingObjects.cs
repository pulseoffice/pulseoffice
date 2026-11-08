using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpMissingObjects
{
    private readonly XNamespace _draw = OdfPackage.Namespaces.Draw;
    private readonly XNamespace _xlink = OdfPackage.Namespaces.Xlink;
    private readonly XNamespace _office = OdfPackage.Namespaces.Office;

    public Diagnostic Loss { get; } = Diagnostic.Warning("ODP180",
        "Embedded objects and pictures whose data is missing from the file are not imported, and neither are their frames and the animations that play on them.");

    public bool Present(XElement? body, Func<string, Stream?> open) =>
        body?.Descendants(_draw + "frame").Any(frame => Embeds(frame) && Missing(frame, open)) == true;

    private bool Embeds(XElement frame) =>
        frame.Elements().Any(child => child.Name == _draw + "object" || child.Name == _draw + "object-ole" || child.Name == _draw + "image");

    private bool Missing(XElement frame, Func<string, Stream?> open) =>
        frame.Elements().Where(child => child.Name == _draw + "object" || child.Name == _draw + "object-ole" || child.Name == _draw + "image")
            .All(child => !child.Elements().Any(inner => inner.Name == _office + "binary-data" || inner.Name == _office + "document")
                && ((string?)child.Attribute(_xlink + "href") is not { Length: > 0 } href || !Exists(href, open)));

    private bool Exists(string href, Func<string, Stream?> open)
    {
        if (href.Contains(':', StringComparison.Ordinal)) return true;
        var path = href.StartsWith("./", StringComparison.Ordinal) ? href[2..] : href;
        using var entry = open(path) ?? open(path.TrimEnd('/') + "/content.xml");
        return entry is not null;
    }
}
