using System.Xml.Linq;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Formats.Odf;

public sealed class OdfObjectNameMapper
{
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    public XElement Write(XElement node, EmbeddedObject content)
    {
        if (content.Name is { Length: > 0 } name) node.SetAttributeValue(Draw + "name", name);
        return node;
    }

    public EmbeddedObject Read(EmbeddedObject content, XElement node) =>
        (string?)node.Attribute(Draw + "name") is { Length: > 0 } name && !Markers.Contains(name) ? content with { Name = name } : content;

    private readonly HashSet<string> Markers = new(StringComparer.Ordinal) { "DeckShape", "DeckFormula" };
}
