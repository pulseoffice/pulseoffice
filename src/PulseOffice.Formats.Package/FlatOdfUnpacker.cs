using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging;

public sealed class FlatOdfUnpacker
{
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;

    private const long MaxDocumentBytes = 512L * 1024 * 1024;

    private readonly IReadOnlyList<FlatPart> _parts =
    [
        new("content.xml", "document-content", ["scripts", "font-face-decls", "automatic-styles", "body"]),
        new("styles.xml", "document-styles", ["font-face-decls", "styles", "automatic-styles", "master-styles"]),
        new("meta.xml", "document-meta", ["meta"]),
        new("settings.xml", "document-settings", ["settings"]),
    ];

    private sealed record FlatPart(string Path, string RootName, IReadOnlyList<string> Sections);

    public bool IsFlat(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) return false;

        var origin = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[64];
            var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            var text = System.Text.Encoding.UTF8.GetString(header[..read]).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return text.StartsWith('<');
        }
        finally
        {
            stream.Position = origin;
        }
    }

    public string? MimeTypeOf(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var origin = stream.Position;
        try
        {
            using var reader = XmlReader.Create(stream, Settings(), new XmlParserContext(null, null, null, XmlSpace.None));
            reader.MoveToContent();
            return reader.GetAttribute("mimetype", Office.NamespaceName);
        }
        catch (XmlException)
        {
            return null;
        }
        finally
        {
            stream.Position = origin;
        }
    }

    public MemoryStream Unpack(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.CanSeek && stream.Length - stream.Position > MaxDocumentBytes)
            throw new InvalidDataException("Flat ODF document is too large.");

        XDocument flat;
        using (var reader = XmlReader.Create(stream, Settings()))
            flat = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        if (flat.Root is not { } root || root.Name != Office + "document")
            throw new InvalidDataException("The document is not a flat OpenDocument file.");

        var mimeType = (string?)root.Attribute(Office + "mimetype") ?? string.Empty;
        var output = new MemoryStream();
        using (var package = OdfPackage.Create(output, mimeType))
        {
            var pictures = ExtractObjects(root, package);
            pictures.AddRange(ExtractPictures(root, package));
            foreach (var part in _parts)
                package.WriteXml(part.Path, new XDocument(Split(root, part)));
            package.WriteManifest(mimeType, [.. _parts.Select(part => (part.Path, "text/xml")), .. pictures.Select(path => (path, "application/octet-stream"))]);
        }

        output.Position = 0;
        return output;
    }

    private XmlReaderSettings Settings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false };

    private XElement Split(XElement root, FlatPart part)
    {
        var element = new XElement(Office + part.RootName,
            root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration || attribute.Name == Office + "version"));
        foreach (var section in part.Sections)
            if (root.Element(Office + section) is { } source)
                element.Add(new XElement(source));
        return element;
    }

    private List<string> ExtractObjects(XElement root, OdfPackage package)
    {
        var paths = new List<string>();
        var count = 0;
        foreach (var nested in root.Descendants(Office + "document").ToList())
        {
            if (nested.Parent is not { } owner) continue;
            var folder = $"Object {++count}";
            nested.Remove();
            foreach (var part in _parts.Where(part => part.Path is "content.xml" or "styles.xml"))
            {
                package.WriteXml($"{folder}/{part.Path}", new XDocument(Split(nested, part)));
                paths.Add($"{folder}/{part.Path}");
            }

            owner.SetAttributeValue(Xlink + "href", "./" + folder);
            owner.SetAttributeValue(Xlink + "type", "simple");
            owner.SetAttributeValue(Xlink + "show", "embed");
            owner.SetAttributeValue(Xlink + "actuate", "onLoad");
        }

        return paths;
    }

    private List<string> ExtractPictures(XElement root, OdfPackage package)
    {
        var paths = new List<string>();
        foreach (var data in root.Descendants(Office + "binary-data").ToList())
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(string.Concat(data.Value.Where(character => !char.IsWhiteSpace(character))));
            }
            catch (FormatException)
            {
                data.Remove();
                continue;
            }

            var path = $"Pictures/flat{paths.Count + 1}{ExtensionOf(bytes)}";
            package.WriteBytes(path, bytes);
            paths.Add(path);
            var owner = data.Parent;
            data.Remove();
            owner?.SetAttributeValue(Xlink + "href", path);
            owner?.SetAttributeValue(Xlink + "type", "simple");
        }

        return paths;
    }

    private string ExtensionOf(byte[] bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => ".png",
        [0xFF, 0xD8, ..] => ".jpg",
        [0x47, 0x49, 0x46, ..] => ".gif",
        [0x3C, ..] => ".svg",
        _ => ".bin",
    };
}
