using System.Xml.Linq;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Formats.Odf;

public sealed class OdfAspectLockMapper
{
    private static readonly XNamespace Pulse = "urn:pulseoffice:drawing";

    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    private readonly XName _lock = Pulse + "lock-aspect-ratio";

    public XElement Write(XElement node, EmbeddedObject content)
    {
        if (content.AspectLock is not { } locked) return node;
        if (node.Attribute(XNamespace.Xmlns + "pulsedraw") is null && node.GetPrefixOfNamespace(Pulse) is null)
            node.Add(new XAttribute(XNamespace.Xmlns + "pulsedraw", Pulse.NamespaceName));
        node.SetAttributeValue(_lock, locked ? "true" : "false");
        return node;
    }

    public EmbeddedObject Read(EmbeddedObject content, XElement node) =>
        (string?)node.Attribute(_lock) switch
        {
            "true" => content.WithAspectLock(true),
            "false" => content.WithAspectLock(false),
            _ => content,
        };

    public void WriteByName(XElement root, Func<string, EmbeddedObject?> lookup)
    {
        foreach (var node in Named(root).ToList())
            if (lookup((string)node.Attribute(Draw + "name")!) is { } content) Write(node, content);
    }

    public IReadOnlyDictionary<string, bool> ReadByName(XElement root)
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var node in Named(root))
            if ((string?)node.Attribute(_lock) is { } value) result.TryAdd((string)node.Attribute(Draw + "name")!, value == "true");
        return result;
    }

    private static IEnumerable<XElement> Named(XElement root) =>
        root.Descendants().Where(node => node.Attribute(Draw + "name") is not null);
}
