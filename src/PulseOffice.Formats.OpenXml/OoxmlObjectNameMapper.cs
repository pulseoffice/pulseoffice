using DocumentFormat.OpenXml;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Formats.OpenXml;

public sealed class OoxmlObjectNameMapper
{
    private const string Uri = "{6F0B7E7C-2D44-4C1B-9E5A-5155A0E1C0DE}";
    private const string Namespace = "urn:pulseoffice:object-name";
    private const string DrawingNamespace = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public TElement Write<TElement>(TElement node, EmbeddedObject content) where TElement : OpenXmlElement
    {
        if (content.Name is not { Length: > 0 } name) return node;
        if (node.Descendants().FirstOrDefault(element => element.LocalName == "cNvPr") is not { } properties) return node;

        var extensions = new OpenXmlUnknownElement("a", "extLst", DrawingNamespace);
        var extension = new OpenXmlUnknownElement("a", "ext", DrawingNamespace);
        extension.SetAttribute(new OpenXmlAttribute("uri", string.Empty, Uri));
        var value = new OpenXmlUnknownElement("qo", "name", Namespace);
        value.SetAttribute(new OpenXmlAttribute("val", string.Empty, name));
        extension.AppendChild(value);
        extensions.AppendChild(extension);
        properties.AppendChild(extensions);
        return node;
    }

    public EmbeddedObject Read(EmbeddedObject content, OpenXmlElement node)
    {
        var value = node.Descendants()
            .FirstOrDefault(element => element.LocalName == "name" && element.NamespaceUri == Namespace)?
            .GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val").Value;
        if (value is { Length: > 0 }) return content with { Name = value };

        var visible = node.Descendants().FirstOrDefault(element => element.LocalName == "cNvPr")?
            .GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "name").Value;
        return visible is { Length: > 0 } && !_generated.IsMatch(visible) ? content with { Name = visible } : content;
    }

    private readonly System.Text.RegularExpressions.Regex _generated = new(
        @"^(Deck\w*|PlaceHolder|Shape|CustomShape|Custom Shape|Picture|Image|Freeform|Group|Line|Connector|Straight Connector|TextBox|Text Box|TextShape|Rectangle|Oval|Title|Content Placeholder|Media|Table|Object|Chart|Graphic|Formula|Fontwork|Control|\w+\s?\d+#\d+)(\s*\d+)?$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
