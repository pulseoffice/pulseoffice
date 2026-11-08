using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging;

public sealed class OdfPackage : IDisposable
{
    public static class Namespaces
    {
        public static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        public static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        public static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        public static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        public static readonly XNamespace Number = "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0";
        public static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
        public static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        public static readonly XNamespace Meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
        public static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
        public static readonly XNamespace Calcext = "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0";

        public static readonly XNamespace Of = "urn:oasis:names:tc:opendocument:xmlns:of:1.2";

        public static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";

        public static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        public static readonly XNamespace Dr3d = "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0";

        public static readonly XNamespace DrawExt = "http://openoffice.org/2010/draw";

        public static readonly XNamespace Presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";

        public static readonly XNamespace Smil = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";

        public static readonly XNamespace Anim = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";

        public static readonly XNamespace Form = "urn:oasis:names:tc:opendocument:xmlns:form:1.0";

        public static readonly XNamespace Ooo = "http://openoffice.org/2004/office";

        public static readonly XNamespace VersionList = "http://openoffice.org/2001/versions-list";

        public static readonly XNamespace Script = "urn:oasis:names:tc:opendocument:xmlns:script:1.0";

        public static readonly XNamespace Dom = "http://www.w3.org/2001/xml-events";

        public static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";

        public static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

        public static readonly XNamespace Chart = "urn:oasis:names:tc:opendocument:xmlns:chart:1.0";

        public static readonly XNamespace Loext = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";
    }

    public static class MimeTypes
    {
        public const string Spreadsheet = "application/vnd.oasis.opendocument.spreadsheet";
        public const string Text = "application/vnd.oasis.opendocument.text";
        public const string Presentation = "application/vnd.oasis.opendocument.presentation";
        public const string SpreadsheetTemplate = "application/vnd.oasis.opendocument.spreadsheet-template";
        public const string PresentationTemplate = "application/vnd.oasis.opendocument.presentation-template";
        public const string Chart = "application/vnd.oasis.opendocument.chart";
        public const string Graphics = "application/vnd.oasis.opendocument.graphics";
    }

    public const long LargestEntryBytes = 512L << 20;

    private readonly ZipArchive _archive;
    private readonly XmlCharacterFilter _characters = new();
    private readonly XmlPartLoader _loader = new();
    private readonly bool _writing;
    private readonly long _largestEntry;

    private OdfPackage(ZipArchive archive, bool writing, long largestEntry = LargestEntryBytes)
    {
        _archive = archive;
        _writing = writing;
        _largestEntry = largestEntry;
    }

    public static OdfPackage OpenRead(Stream stream) => OpenRead(stream, LargestEntryBytes);

    public static OdfPackage OpenRead(Stream stream, long largestEntry) =>
        new(new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true), writing: false, largestEntry);

    public static OdfPackage Create(Stream stream, string mimeType)
    {
        var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var package = new OdfPackage(archive, writing: true);

        var entry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(entry.Open(), Encoding.ASCII))
            writer.Write(mimeType);

        return package;
    }

    public string? ReadMimeType()
    {
        var entry = _archive.GetEntry("mimetype");
        if (entry is null) return null;

        using var reader = new StreamReader(Open(entry), Encoding.ASCII);
        var buffer = new char[256];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read).Trim();
    }

    public Stream? OpenEntry(string path) => _archive.GetEntry(path) is { } entry ? Open(entry) : null;

    public XDocument? ReadXml(string path)
    {
        var entry = _archive.GetEntry(path);
        if (entry is null) return null;

        using var stream = Open(entry);
        return _loader.Load(stream);
    }

    public void WriteXml(string path, XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_writing) throw new InvalidOperationException("The package is open read-only.");

        _characters.Clean(document);
        var entry = _archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        document.Save(stream, SaveOptions.DisableFormatting);
    }

    public XmlCharacterFilter Characters => _characters;

    public void WriteXml(string path, Action<XmlWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (!_writing) throw new InvalidOperationException("The package is open read-only.");

        var entry = _archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
        write(writer);
    }

    public byte[]? ReadBytes(string path)
    {
        var entry = _archive.GetEntry(path);
        if (entry is null) return null;

        using var stream = Open(entry);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private Stream Open(ZipArchiveEntry entry)
    {
        if (entry.Length > _largestEntry) throw new InvalidDataException("A package entry expands beyond the safe size limit.");
        return new BoundedReadStream(entry.Open(), _largestEntry);
    }

    public void WriteBytes(string path, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!_writing) throw new InvalidOperationException("The package is open read-only.");

        var entry = _archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    public void WriteManifest(string mimeType, IEnumerable<string> paths) =>
    WriteManifest(mimeType, paths.Select(path => (path, "text/xml")));

    public void WriteManifest(string mimeType, IEnumerable<(string Path, string MediaType)> entries)
    {
        var manifest = Namespaces.Manifest;

        var root = new XElement(manifest + "manifest",
            new XAttribute(XNamespace.Xmlns + "manifest", manifest.NamespaceName),
            new XAttribute(manifest + "version", "1.3"),
            new XElement(manifest + "file-entry",
                new XAttribute(manifest + "full-path", "/"),
                new XAttribute(manifest + "version", "1.3"),
                new XAttribute(manifest + "media-type", mimeType)));

        foreach (var (path, mediaType) in entries)
            root.Add(new XElement(manifest + "file-entry",
                new XAttribute(manifest + "full-path", path),
                new XAttribute(manifest + "media-type", mediaType)));

        WriteXml("META-INF/manifest.xml", new XDocument(root));
    }

    public void Dispose() => _archive.Dispose();
}
