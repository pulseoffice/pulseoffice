using DocumentFormat.OpenXml;

namespace PulseOffice.Formats.OpenXml;

public sealed class OoxmlAltTextMapper : AltTextMapper<OpenXmlElement>
{
    protected override OpenXmlElement? Target(OpenXmlElement node) =>
        node.Descendants().FirstOrDefault(IsDrawingProperties);

    protected override IEnumerable<(string Name, OpenXmlElement Target)> Named(OpenXmlElement root) =>
        root.Descendants().Where(IsDrawingProperties)
            .Select(properties => (Name: Attribute(properties, "name"), Target: properties))
            .Where(pair => pair.Name is not null)
            .Select(pair => (pair.Name!, pair.Target));

    protected override void Store(OpenXmlElement target, AltText text)
    {
        if (text.Title is { } title) target.SetAttribute(new OpenXmlAttribute("title", string.Empty, title));
        if (text.Description is { } description) target.SetAttribute(new OpenXmlAttribute("descr", string.Empty, description));
    }

    protected override AltText Load(OpenXmlElement target) =>
        new(Attribute(target, "title"), Attribute(target, "descr"));

    private bool IsDrawingProperties(OpenXmlElement element) => element.LocalName == "cNvPr";

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(a => a.LocalName == name && string.IsNullOrEmpty(a.NamespaceUri)).Value
            is { Length: > 0 } value ? value : null;
}
