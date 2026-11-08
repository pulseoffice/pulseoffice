using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using PulseOffice.Documents;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Interchange.Text.Epub;

public sealed record EpubResource(string Path, string MediaType, ReadOnlyMemory<byte> Data);

public sealed record EpubBook(
    string Identifier, string Title, string Language, string? Author, string? Subject, string? Description,
    IReadOnlyList<string> Keywords, DateTimeOffset Modified, HtmlPage Page, IReadOnlyList<EpubResource> Resources);

public interface IEpubPackageWriter
{
    void Write(EpubBook book, Stream stream);
}

public sealed class EpubImageSink : IHtmlImageSink
{
    private readonly Dictionary<RasterImageObject, string> _paths = new(ReferenceEqualityComparer.Instance);
    private readonly List<EpubResource> _resources = [];

    public IReadOnlyList<EpubResource> Resources => _resources;

    public string Source(RasterImageObject image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_paths.TryGetValue(image, out var path)) return path;
        var extension = image.MediaType switch
        {
            "image/jpeg" => "jpg",
            "image/gif" => "gif",
            "image/svg+xml" => "svg",
            "image/webp" => "webp",
            _ => "png",
        };
        path = "images/image" + (_resources.Count + 1).ToString(CultureInfo.InvariantCulture) + "." + extension;
        _paths[image] = path;
        _resources.Add(new EpubResource(path, image.MediaType is { Length: > 0 } type ? type : "image/png", image.Data));
        return path;
    }
}

public sealed class EpubDocumentWriter(IHtmlPageRenderer pages, IHtmlFidelity fidelity, IEpubPackageWriter package) : ITextDocumentWriter
{
    public TextDocumentFormat Format => TextInterchangeFormats.Epub;

    public FidelityReport CheckFidelity(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var losses = fidelity.Check(document).Losses
            .Select(loss => loss with { Code = loss.Code.Replace("HTML", "EPUB", StringComparison.Ordinal) })
            .ToList();
        return new FidelityReport(losses);
    }

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            var images = new EpubImageSink();
            var page = pages.Render(document, HtmlFlavor.Strict, images);
            var properties = document.Properties;
            var title = properties.Title is { Length: > 0 } named ? named : page.Headings.FirstOrDefault()?.Text ?? "Document";
            var book = new EpubBook(
                "urn:uuid:" + Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                title,
                document.Language is { Length: > 0 } language ? language : "und",
                properties.Author,
                properties.Subject,
                properties.Description,
                properties.Keywords,
                DateTimeOffset.UtcNow,
                page,
                images.Resources);
            package.Write(book, stream);
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("EPUB200", $"Writing the EPUB file failed: {exception.Message}").With(exception.Message));
        }
    }
}

public sealed class EpubPackageWriter : IEpubPackageWriter
{
    private const string ContentPath = "text.xhtml";

    public void Write(EpubBook book, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Entry(zip, "mimetype", Encoding.ASCII.GetBytes("application/epub+zip"), CompressionLevel.NoCompression);
            Entry(zip, "META-INF/container.xml", Utf8(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\n" +
                "<rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles>\n</container>\n"), CompressionLevel.Optimal);
            Entry(zip, "OEBPS/content.opf", Utf8(Package(book)), CompressionLevel.Optimal);
            Entry(zip, "OEBPS/nav.xhtml", Utf8(Navigation(book)), CompressionLevel.Optimal);
            Entry(zip, "OEBPS/toc.ncx", Utf8(Ncx(book)), CompressionLevel.Optimal);
            Entry(zip, "OEBPS/" + ContentPath, Utf8(book.Page.Markup), CompressionLevel.Optimal);
            foreach (var resource in book.Resources) Entry(zip, "OEBPS/" + resource.Path, resource.Data.ToArray(), CompressionLevel.NoCompression);
        }
        buffer.Position = 0;
        buffer.CopyTo(stream);
    }

    private string Package(EpubBook book)
    {
        var metadata = new StringBuilder();
        metadata.Append("<dc:identifier id=\"uid\">").Append(Escape(book.Identifier)).Append("</dc:identifier>\n");
        metadata.Append("<dc:title>").Append(Escape(book.Title)).Append("</dc:title>\n");
        metadata.Append("<dc:language>").Append(Escape(book.Language)).Append("</dc:language>\n");
        if (book.Author is { Length: > 0 } author) metadata.Append("<dc:creator>").Append(Escape(author)).Append("</dc:creator>\n");
        if (book.Subject is { Length: > 0 } subject) metadata.Append("<dc:subject>").Append(Escape(subject)).Append("</dc:subject>\n");
        foreach (var keyword in book.Keywords.Where(keyword => keyword.Length > 0)) metadata.Append("<dc:subject>").Append(Escape(keyword)).Append("</dc:subject>\n");
        if (book.Description is { Length: > 0 } description) metadata.Append("<dc:description>").Append(Escape(description)).Append("</dc:description>\n");
        metadata.Append("<meta property=\"dcterms:modified\">").Append(book.Modified.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append("</meta>\n");
        var manifest = new StringBuilder();
        manifest.Append("<item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>\n");
        manifest.Append("<item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>\n");
        manifest.Append("<item id=\"text\" href=\"").Append(ContentPath).Append("\" media-type=\"application/xhtml+xml\"/>\n");
        for (var index = 0; index < book.Resources.Count; index++)
            manifest.Append("<item id=\"r").Append(index + 1).Append("\" href=\"").Append(Escape(book.Resources[index].Path))
                .Append("\" media-type=\"").Append(Escape(book.Resources[index].MediaType)).Append("\"/>\n");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
               "<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"uid\" xml:lang=\"" + Escape(book.Language) + "\">\n" +
               "<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n" + metadata + "</metadata>\n" +
               "<manifest>\n" + manifest + "</manifest>\n" +
               "<spine toc=\"ncx\">\n<itemref idref=\"text\"/>\n</spine>\n</package>\n";
    }

    private string Navigation(EpubBook book)
    {
        var list = new StringBuilder();
        Tree(book, list, (heading, depth) => list.Append("<li><a href=\"").Append(ContentPath).Append('#').Append(heading.Id).Append("\">").Append(Escape(heading.Text)).Append("</a>"),
            () => list.Append("<ol>\n"), () => list.Append("</ol>\n"), () => list.Append("</li>\n"));
        if (book.Page.Headings.Count == 0) list.Append("<ol>\n<li><a href=\"").Append(ContentPath).Append("\">").Append(Escape(book.Title)).Append("</a></li>\n</ol>\n");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE html>\n" +
               "<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" lang=\"" + Escape(book.Language) + "\" xml:lang=\"" + Escape(book.Language) + "\">\n" +
               "<head>\n<meta charset=\"utf-8\"/>\n<title>" + Escape(book.Title) + "</title>\n</head>\n<body>\n" +
               "<nav epub:type=\"toc\" id=\"toc\">\n<h1>" + Escape(book.Title) + "</h1>\n" + list + "</nav>\n</body>\n</html>\n";
    }

    private string Ncx(EpubBook book)
    {
        var points = new StringBuilder();
        var order = 0;
        Tree(book, points,
            (heading, depth) => points.Append("<navPoint id=\"n").Append(++order).Append("\" playOrder=\"").Append(order).Append("\"><navLabel><text>")
                .Append(Escape(heading.Text)).Append("</text></navLabel><content src=\"").Append(ContentPath).Append('#').Append(heading.Id).Append("\"/>\n"),
            () => { }, () => { }, () => points.Append("</navPoint>\n"));
        if (order == 0)
            points.Append("<navPoint id=\"n1\" playOrder=\"1\"><navLabel><text>").Append(Escape(book.Title)).Append("</text></navLabel><content src=\"").Append(ContentPath).Append("\"/></navPoint>\n");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">\n" +
               "<head><meta name=\"dtb:uid\" content=\"" + Escape(book.Identifier) + "\"/></head>\n" +
               "<docTitle><text>" + Escape(book.Title) + "</text></docTitle>\n<navMap>\n" + points + "</navMap>\n</ncx>\n";
    }

    private void Tree(EpubBook book, StringBuilder target, Action<HtmlHeading, int> open, Action openList, Action closeList, Action close)
    {
        var root = new TocNode(null, 0);
        var stack = new Stack<TocNode>();
        stack.Push(root);
        foreach (var heading in book.Page.Headings.Where(heading => heading.Text.Length > 0))
        {
            while (stack.Peek() != root && stack.Peek().Level >= heading.Level) stack.Pop();
            var node = new TocNode(heading, heading.Level);
            stack.Peek().Children.Add(node);
            stack.Push(node);
        }
        if (root.Children.Count > 0) Emit(root.Children, 1, open, openList, closeList, close);
    }

    private void Emit(List<TocNode> nodes, int depth, Action<HtmlHeading, int> open, Action openList, Action closeList, Action close)
    {
        openList();
        foreach (var node in nodes)
        {
            open(node.Heading!, depth);
            if (node.Children.Count > 0) Emit(node.Children, depth + 1, open, openList, closeList, close);
            close();
        }
        closeList();
    }

    private sealed class TocNode(HtmlHeading? heading, int level)
    {
        public HtmlHeading? Heading { get; } = heading;

        public int Level { get; } = level;

        public List<TocNode> Children { get; } = [];
    }

    private void Entry(ZipArchive zip, string name, byte[] data, CompressionLevel level)
    {
        var entry = zip.CreateEntry(name, level);
        using var output = entry.Open();
        output.Write(data);
    }

    private byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    private string Escape(string text) => SecurityElement.Escape(XmlSafe(text)) ?? string.Empty;

    private string XmlSafe(string text)
    {
        if (text.All(character => System.Xml.XmlConvert.IsXmlChar(character))) return text;
        var kept = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (System.Xml.XmlConvert.IsXmlChar(text[i])) kept.Append(text[i]);
            else if (i + 1 < text.Length && System.Xml.XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
            {
                kept.Append(text, i, 2);
                i++;
            }
        }
        return kept.ToString();
    }
}
