using System.Xml.Linq;

namespace PulseOffice.Formats.Odf;

public sealed class OdfAltTextMapper : AltTextMapper<XElement>
{
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";

    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    protected override XElement? Target(XElement node) => node;

    protected override IEnumerable<(string Name, XElement Target)> Named(XElement root) =>
        root.Descendants()
            .Where(e => e.Attribute(Draw + "name") is not null)
            .Select(e => ((string)e.Attribute(Draw + "name")!, e));

    protected override void Store(XElement target, AltText text)
    {
        if (target.Element(Svg + "title") is not null || target.Element(Svg + "desc") is not null) return;
        if (text.Title is { } title) target.Add(new XElement(Svg + "title", title));
        if (text.Description is { } description) target.Add(new XElement(Svg + "desc", description));
    }

    protected override AltText Load(XElement target) =>
        new(target.Element(Svg + "title")?.Value, target.Element(Svg + "desc")?.Value);
}
