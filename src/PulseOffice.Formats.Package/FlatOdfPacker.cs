using System.IO.Compression;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging;

public sealed class FlatOdfPacker
{
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;

    private readonly IReadOnlyList<(string Path, IReadOnlyList<string> Sections)> _parts =
    [
        ("meta.xml", ["meta"]),
        ("settings.xml", ["settings"]),
        ("styles.xml", ["font-face-decls", "styles", "automatic-styles", "master-styles"]),
        ("content.xml", ["scripts", "font-face-decls", "automatic-styles", "body"]),
    ];

    private readonly XmlPartLoader _loader = new();

    private readonly IReadOnlyList<string> _order =
        ["meta", "settings", "scripts", "font-face-decls", "styles", "automatic-styles", "master-styles", "body"];

    public MemoryStream Pack(Stream package)
    {
        ArgumentNullException.ThrowIfNull(package);
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        var mimeType = Text(archive, "mimetype") ?? string.Empty;
        var documents = _parts.ToDictionary(part => part.Path, part => Load(archive, part.Path));
        if (documents["content.xml"] is not { Root: { } contentRoot })
            throw new InvalidDataException("The package has no content.xml.");

        RenameClashingStyles(documents["styles.xml"]?.Root, contentRoot);

        var root = new XElement(Office + "document",
            new XAttribute(Office + "mimetype", mimeType),
            new XAttribute(Office + "version", (string?)contentRoot.Attribute(Office + "version") ?? "1.3"));
        var sections = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var (path, names) in _parts)
        {
            if (documents[path]?.Root is not { } source) continue;
            foreach (var attribute in source.Attributes().Where(attribute => attribute.IsNamespaceDeclaration && root.Attribute(attribute.Name) is null))
                root.Add(new XAttribute(attribute));
            foreach (var name in names)
            {
                if (source.Element(Office + name) is not { } section) continue;
                if (sections.TryGetValue(name, out var existing))
                {
                    var known = existing.Elements().Select(element => element.Attribute(Style + "name")?.Value).OfType<string>().ToHashSet(StringComparer.Ordinal);
                    existing.Add(section.Elements().Where(element => element.Attribute(Style + "name")?.Value is not { } named || !known.Contains(named)).Select(element => new XElement(element)));
                }
                else
                {
                    sections[name] = new XElement(section);
                }
            }
        }

        foreach (var name in _order)
            if (sections.TryGetValue(name, out var section)) root.Add(section);

        EmbedPictures(root, archive);

        var output = new MemoryStream();
        new XDocument(new XDeclaration("1.0", "UTF-8", null), root).Save(output, SaveOptions.DisableFormatting);
        output.Position = 0;
        return output;
    }

    private void RenameClashingStyles(XElement? stylesRoot, XElement contentRoot)
    {
        var automatic = stylesRoot?.Element(Office + "automatic-styles");
        var contentNames = contentRoot.Element(Office + "automatic-styles")?.Elements()
            .Select(element => element.Attribute(Style + "name")?.Value).OfType<string>().ToHashSet(StringComparer.Ordinal) ?? [];
        if (automatic is null || stylesRoot is null) return;

        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in automatic.Elements())
        {
            if (style.Attribute(Style + "name") is not { } name || !contentNames.Contains(name.Value)) continue;
            var renamed = name.Value + "_s";
            renames[name.Value] = renamed;
            name.Value = renamed;
        }

        if (renames.Count == 0) return;
        foreach (var attribute in stylesRoot.Descendants().SelectMany(element => element.Attributes()))
            if (attribute.Name.LocalName.EndsWith("name", StringComparison.Ordinal) && attribute.Name != Style + "name"
                && renames.TryGetValue(attribute.Value, out var replacement))
                attribute.Value = replacement;
    }

    private void EmbedPictures(XElement root, ZipArchive archive)
    {
        foreach (var element in root.Descendants().Where(element => element.Attribute(Xlink + "href") is not null).ToList())
        {
            var href = element.Attribute(Xlink + "href")!.Value;
            href = href.StartsWith("./", StringComparison.Ordinal) ? href[2..] : href.TrimStart('/');
            if (href.Contains("://", StringComparison.Ordinal)) continue;
            if (archive.GetEntry(href.TrimEnd('/') + "/content.xml") is not null)
            {
                element.Attributes().Where(attribute => attribute.Name.Namespace == Xlink).Remove();
                element.Add(EmbeddedDocument(archive, href.TrimEnd('/') + "/"));
                continue;
            }

            if (archive.GetEntry(href) is not { } entry || entry.FullName.EndsWith('/')) continue;
            using var input = entry.Open();
            using var buffer = new MemoryStream();
            input.CopyTo(buffer);
            element.Attribute(Xlink + "href")!.Remove();
            element.Attributes().Where(attribute => attribute.Name.Namespace == Xlink).Remove();
            element.Add(new XElement(Office + "binary-data", Convert.ToBase64String(buffer.ToArray())));
        }
    }

    private XElement EmbeddedDocument(ZipArchive archive, string folder)
    {
        var nested = new XElement(Office + "document");
        var sections = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var path in new[] { "styles.xml", "content.xml" })
        {
            if (Load(archive, folder + path)?.Root is not { } source) continue;
            foreach (var attribute in source.Attributes().Where(attribute => attribute.IsNamespaceDeclaration && nested.Attribute(attribute.Name) is null))
                nested.Add(new XAttribute(attribute));
            foreach (var section in source.Elements())
            {
                if (sections.TryGetValue(section.Name.LocalName, out var existing)) existing.Add(section.Elements().Select(child => new XElement(child)));
                else sections[section.Name.LocalName] = new XElement(section);
            }
        }

        foreach (var name in _order)
            if (sections.TryGetValue(name, out var section)) nested.Add(section);
        nested.SetAttributeValue(Office + "mimetype", MediaTypeOf(archive, folder, nested));
        nested.SetAttributeValue(Office + "version", "1.3");
        return nested;
    }

    private string MediaTypeOf(ZipArchive archive, string folder, XElement nested)
    {
        var manifest = Load(archive, "META-INF/manifest.xml");
        var declared = manifest?.Root?.Elements(OdfPackage.Namespaces.Manifest + "file-entry")
            .FirstOrDefault(entry => (string?)entry.Attribute(OdfPackage.Namespaces.Manifest + "full-path") is { } path && path.TrimEnd('/') == folder.TrimEnd('/'))
            ?.Attribute(OdfPackage.Namespaces.Manifest + "media-type")?.Value;
        if (!string.IsNullOrEmpty(declared)) return declared;
        var body = nested.Element(Office + "body")?.Elements().FirstOrDefault()?.Name.LocalName;
        return body == "chart" ? OdfPackage.MimeTypes.Chart : "application/vnd.oasis.opendocument.formula";
    }

    private XDocument? Load(ZipArchive archive, string path)
    {
        if (archive.GetEntry(path) is not { } entry) return null;
        using var input = entry.Open();
        return _loader.Load(input);
    }

    private string? Text(ZipArchive archive, string path)
    {
        if (archive.GetEntry(path) is not { } entry) return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd().Trim();
    }
}
