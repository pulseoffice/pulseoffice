using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging.Legacy;

public interface ILegacyOdfUpgrader
{
    bool IsLegacy(Stream stream);

    MemoryStream Upgrade(Stream stream);
}

public sealed class LegacyOdfUpgrader : ILegacyOdfUpgrader
{
    private const string MimeTypePath = "mimetype";
    private const string ManifestPath = "META-INF/manifest.xml";
    private const string ContentPath = "content.xml";
    private const long MaxEntryBytes = 256L * 1024 * 1024;
    private const long MaxPackageBytes = 1L << 30;

    private readonly LegacyVocabulary _vocabulary;
    private readonly ILegacyXmlTranslator _translator;
    private readonly XmlPartLoader _loader = new();

    public LegacyOdfUpgrader() : this(new LegacyVocabulary())
    {
    }

    public LegacyOdfUpgrader(LegacyVocabulary vocabulary) : this(vocabulary, new LegacyXmlTranslator(vocabulary))
    {
    }

    public LegacyOdfUpgrader(LegacyVocabulary vocabulary, ILegacyXmlTranslator translator)
    {
        _vocabulary = vocabulary;
        _translator = translator;
    }

    public bool IsLegacy(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) return false;
        var origin = stream.Position;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.GetEntry(MimeTypePath) is { } mimetype
                && Text(mimetype).StartsWith(LegacyVocabulary.LegacyMimePrefix, StringComparison.Ordinal))
                return true;
            if (archive.GetEntry(ManifestPath) is { } manifest && RootNamespace(manifest) == LegacyVocabulary.LegacyManifest)
                return true;
            return archive.GetEntry(ContentPath) is { } content && RootNamespace(content) == LegacyVocabulary.LegacyOffice;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException)
        {
            return false;
        }
        finally
        {
            stream.Position = origin;
        }
    }

    public MemoryStream Upgrade(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var source = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var manifest = source.GetEntry(ManifestPath) is { } manifestEntry ? Manifest(manifestEntry) : null;
        var encrypted = manifest is null ? [] : EncryptedPaths(manifest);

        var output = new MemoryStream();
        long expanded = 0;
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries.OrderBy(entry => entry.FullName == MimeTypePath ? 0 : 1))
            {
                if (entry.FullName.EndsWith('/')) continue;
                var bytes = entry.FullName switch
                {
                    MimeTypePath => Encoding.ASCII.GetBytes(_vocabulary.MapMimeType(Text(entry))),
                    ManifestPath when manifest is not null => Serialize(manifest),
                    _ when IsXml(entry.FullName) && !encrypted.Contains(entry.FullName) => TranslatedXml(entry),
                    _ => Bytes(entry),
                };

                expanded += bytes.LongLength;
                if (expanded > MaxPackageBytes) throw new InvalidDataException("The legacy package expands beyond the safe size limits.");
                var created = target.CreateEntry(entry.FullName, entry.FullName == MimeTypePath ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
                using var written = created.Open();
                written.Write(bytes);
            }
        }

        output.Position = 0;
        return output;
    }

    private XDocument Manifest(ZipArchiveEntry entry)
    {
        XDocument document;
        using (var input = Open(entry)) document = _loader.Load(input);
        if (!_translator.IsLegacy(document)) return document;

        var translated = _translator.Translate(document);
        var ns = OdfPackage.Namespaces.Manifest;
        translated.Root?.SetAttributeValue(ns + "version", "1.2");
        foreach (var file in translated.Descendants(ns + "file-entry"))
            if (file.Attribute(ns + "media-type") is { } mediaType)
                mediaType.Value = _vocabulary.MapMimeType(mediaType.Value);
        return translated;
    }

    private HashSet<string> EncryptedPaths(XDocument manifest)
    {
        var ns = OdfPackage.Namespaces.Manifest;
        return manifest.Descendants(ns + "file-entry")
            .Where(file => file.Element(ns + "encryption-data") is not null)
            .Select(file => (string?)file.Attribute(ns + "full-path"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private byte[] TranslatedXml(ZipArchiveEntry entry)
    {
        var original = Bytes(entry);
        try
        {
            using var input = new MemoryStream(original);
            var document = _loader.Load(input);
            return _translator.IsLegacy(document) ? Serialize(_translator.Translate(document)) : original;
        }
        catch (XmlException)
        {
            return original;
        }
    }

    private bool IsXml(string path) => path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private XNamespace? RootNamespace(ZipArchiveEntry entry)
    {
        using var input = Open(entry);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        return reader.MoveToContent() == XmlNodeType.Element ? XNamespace.Get(reader.NamespaceURI) : null;
    }

    private string Text(ZipArchiveEntry entry) => Encoding.ASCII.GetString(Bytes(entry)).Trim();

    private byte[] Serialize(XDocument document)
    {
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
            document.Save(writer);
        return output.ToArray();
    }

    private Stream Open(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntryBytes) throw new InvalidDataException("Legacy package entry is too large.");
        return entry.Open();
    }

    private byte[] Bytes(ZipArchiveEntry entry)
    {
        using var input = Open(entry);
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        if (buffer.Length > MaxEntryBytes) throw new InvalidDataException("Legacy package entry is too large.");
        return buffer.ToArray();
    }
}
