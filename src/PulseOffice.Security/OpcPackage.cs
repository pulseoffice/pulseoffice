using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Security;

public interface IOpcParts : IPackageParts
{
    string? ContentTypeOf(string name);
}

public sealed class OpcPackage : IOpcParts
{
    public const string ContentTypesPart = "[Content_Types].xml";

    public static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly List<string> _order = [];
    private readonly Dictionary<string, byte[]> _content = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Names => [.. _order.Where(name => name != ContentTypesPart)];

    public IReadOnlyList<string> Entries => _order;

    public bool Contains(string name) => _content.ContainsKey(name);

    public byte[] Read(string name) =>
        _content.TryGetValue(name, out var bytes)
            ? bytes
            : throw new InvalidOperationException($"The package has no part named {name}.");

    public void Write(string name, byte[] content)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(content);

        if (!_content.ContainsKey(name)) _order.Add(name);
        _content[name] = content;
    }

    public void Remove(string name)
    {
        if (_content.Remove(name)) _order.Remove(name);
    }

    public XElement ReadXml(string name)
    {
        using var reader = XmlReader.Create(
            new MemoryStream(Read(name)),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        return XElement.Load(reader, LoadOptions.PreserveWhitespace);
    }

    public void WriteXml(string name, XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        }))
        {
            new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(writer);
        }

        Write(name, buffer.ToArray());
    }

    public string? ContentTypeOf(string name)
    {
        if (!Contains(ContentTypesPart)) return null;

        var types = ReadXml(ContentTypesPart);
        var partName = "/" + name;

        var overridden = types.Elements(ContentTypes + "Override")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("PartName"), partName, StringComparison.OrdinalIgnoreCase));
        if (overridden is not null) return (string?)overridden.Attribute("ContentType");

        var extension = Path.GetExtension(name).TrimStart('.');

        return (string?)types.Elements(ContentTypes + "Default")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("ContentType");
    }

    public string RelationshipsOf(string part)
    {
        var folder = Path.GetDirectoryName(part)?.Replace('\\', '/') ?? string.Empty;
        var file = Path.GetFileName(part);

        return folder.Length == 0 ? $"_rels/{file}.rels" : $"{folder}/_rels/{file}.rels";
    }

    public string Resolve(string source, string target)
    {
        if (target.StartsWith('/')) return target[1..];

        var folder = Path.GetDirectoryName(source)?.Replace('\\', '/') ?? string.Empty;
        var segments = folder.Length == 0 ? new List<string>() : [.. folder.Split('/')];

        foreach (var segment in target.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
            }
            else if (segment is not ("." or ""))
            {
                segments.Add(segment);
            }
        }

        return Uri.UnescapeDataString(string.Join('/', segments));
    }
}

public interface IOpcPackageStore
{
    OpcPackage Load(string path);

    void Save(OpcPackage package, string path);
}

public sealed class OpcPackageStore : IOpcPackageStore
{
    private const long MaximumPart = 512L * 1024 * 1024;

    public OpcPackage Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var package = new OpcPackage();

        using var archive = ZipFile.OpenRead(path);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue;

            if (entry.Length > MaximumPart)
                throw new InvalidDataException($"The part {entry.FullName} is too large to sign or verify.");

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            package.Write(entry.FullName, buffer.ToArray());
        }

        return package;
    }

    public void Save(OpcPackage package, string path)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var temporary = path + ".signing";

        try
        {
            using (var archive = new ZipArchive(File.Create(temporary), ZipArchiveMode.Create))
            {
                var ordered = package.Entries
                    .OrderBy(name => name == OpcPackage.ContentTypesPart ? 0 : 1)
                    .ToList();

                foreach (var name in ordered)
                {
                    using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                    stream.Write(package.Read(name));
                }
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
