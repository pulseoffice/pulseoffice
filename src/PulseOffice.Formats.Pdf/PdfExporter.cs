using System.Globalization;
using System.Text;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public sealed record PdfExportOptions(
    string Title = "", string Author = "", string Subject = "", bool Compress = true)
{
    public string Producer { get; init; } = "PulseOffice";

    public string Creator { get; init; } = "PulseOffice";

    public string Keywords { get; init; } = "";

    public string? Language { get; init; }

    public IReadOnlyList<int>? Pages { get; init; }

    public bool Bookmarks { get; init; } = true;

    public bool Links { get; init; } = true;

    public bool Notes { get; init; } = true;

    public bool Archive { get; init; }

    public DateTimeOffset? Created { get; init; }

    public PdfImageOptions Images { get; init; } = new();

    public PdfSecurityOptions? Security { get; init; }
}

public interface IPdfExporter
{
    OperationResult<int> Write(IPrintableDocument document, Stream stream, PdfExportOptions? options = null);
}

public sealed class PdfExporter(
    IPdfFontSetFactory fonts, IPdfArchiveParts archive, IPdfNavigationWriter navigation,
    IPdfImageWriter images, IPdfEncryptionFactory encryption) : IPdfExporter
{
    public PdfExporter(IPdfFontSetFactory fonts)
        : this(fonts, new PdfArchiveParts(new SrgbProfile()), new PdfNavigationWriter(), new PdfImageWriter(), new Aes256EncryptionFactory())
    {
    }

    public OperationResult<int> Write(IPrintableDocument document, Stream stream, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);

        options ??= new PdfExportOptions();

        var selected = (options.Pages ?? Enumerable.Range(0, Math.Max(0, document.PageCount)).ToList())
            .Where(index => index >= 0 && index < document.PageCount).ToList();
        if (document.PageCount <= 0 || selected.Count == 0)
            return OperationResult<int>.Fail(Diagnostic.Error("PDF001", "The document has no pages to write."));

        try
        {
            var archiving = options.Archive;
            var file = new PdfFile(stream, archiving ? null : encryption.Create(options.Security));
            var diagnostics = new List<Diagnostic>();
            if (archiving && options.Security is { Encrypts: true })
                diagnostics.Add(Diagnostic.Warning("PDF014", "PDF/A does not allow encryption, so the file was written as PDF/A without passwords."));
            var set = fonts.Create();
            var pageSize = new DocumentSize(document.Setup.PageWidth, document.Setup.PageHeight);
            var height = pageSize.Height.ToPoints();

            var fontObjects = new Dictionary<IPdfFontResource, int>(ReferenceEqualityComparer.Instance);
            var pageObjects = new List<int>();
            var written = new Dictionary<(string Fingerprint, int Width, int Height), int>();
            var marks = new PdfDocumentMarks();
            var pagesId = file.Reserve();

            foreach (var index in selected)
            {
                var pageId = file.Reserve();
                var canvas = new PdfContentCanvas(pageSize, set);
                document.PaintPage(index, canvas);

                var contentId = file.WriteStream(PdfEncoding.ToBytes(canvas.Content), options.Compress);

                var resources = new StringBuilder("<< /Font << ");

                foreach (var font in canvas.UsedFonts)
                {
                    if (!fontObjects.TryGetValue(font, out var id)) fontObjects[font] = id = file.Reserve();
                    resources.Append(CultureInfo.InvariantCulture, $"/{font.Name} {id} 0 R ");
                }

                resources.Append(">> ");

                if (canvas.UsedAlphas.Count > 0)
                {
                    resources.Append("/ExtGState << ");

                    foreach (var (name, alpha) in canvas.UsedAlphas)
                        resources.Append(CultureInfo.InvariantCulture,
                            $"/{name} << /Type /ExtGState /ca {Math.Round(alpha, 3)} " +
                            $"/CA {Math.Round(alpha, 3)} >> ");

                    resources.Append(">> ");
                }

                if (canvas.UsedPatterns.Count > 0)
                {
                    resources.Append("/Pattern << ");

                    foreach (var (name, definition) in canvas.UsedPatterns)
                        resources.Append(CultureInfo.InvariantCulture,
                            $"/{name} {file.WriteObject(definition)} 0 R ");

                    resources.Append(">> ");
                }

                if (canvas.UsedImages.Count > 0)
                {
                    resources.Append("/XObject << ");

                    foreach (var (name, placement) in canvas.UsedImages)
                    {
                        var fingerprint = Fingerprint(placement.Image);
                        var key = options.Images.MaxResolution is null
                            ? (fingerprint, 0, 0)
                            : (fingerprint, (int)Math.Ceiling(placement.WidthPoints), (int)Math.Ceiling(placement.HeightPoints));
                        if (!written.TryGetValue(key, out var id))
                        {
                            id = images.Write(file, placement, archiving ? options.Images with { DeviceIndependentOnly = true } : options.Images, options.Compress, diagnostics);
                            written[key] = id;
                        }
                        resources.Append(CultureInfo.InvariantCulture, $"/{name} {id} 0 R ");
                    }

                    resources.Append(">> ");
                }

                resources.Append(">>");

                marks.Collect(pageId, canvas, height, options);
                var annotations = navigation.Annotations(file, marks.PageLinks(pageId), marks.PageNotes(pageId), height);
                var annots = annotations.Count == 0 ? string.Empty : " /Annots [" + string.Join(' ', annotations.Select(id => $"{id} 0 R")) + "]";

                file.WriteObject(pageId,
                    $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 " +
                    $"{Points(pageSize.Width)} {Points(pageSize.Height)}] " +
                    $"/Resources {resources} /Contents {contentId} 0 R{annots} >>");
                pageObjects.Add(pageId);
            }

            foreach (var (font, id) in fontObjects) font.Write(file, id, options.Compress);

            file.WriteObject(pagesId,
                $"<< /Type /Pages /Count {pageObjects.Count} /Kids [" +
                string.Join(' ', pageObjects.Select(id => $"{id} 0 R")) + "] >>");

            var created = options.Created ?? DateTimeOffset.Now;
            var infoId = file.WriteObject(
                "<< /Producer " + PdfFile.TextString(options.Producer) +
                " /Creator " + PdfFile.TextString(options.Creator) +
                " /Title " + PdfFile.TextString(options.Title) +
                " /Author " + PdfFile.TextString(options.Author) +
                " /Subject " + PdfFile.TextString(options.Subject) +
                " /Keywords " + PdfFile.TextString(options.Keywords) +
                " /CreationDate " + PdfFile.DateString(created) +
                " /ModDate " + PdfFile.DateString(created) + " >>");

            var catalog = new StringBuilder($"<< /Type /Catalog /Pages {pagesId} 0 R");
            if (options.Bookmarks && navigation.Outline(file, marks.Headings) is { } outline)
                catalog.Append(CultureInfo.InvariantCulture, $" /Outlines {outline} 0 R /PageMode /UseOutlines");
            if (navigation.Destinations(file, marks.Destinations) is { } names)
                catalog.Append(CultureInfo.InvariantCulture, $" /Names << /Dests {names} 0 R >>");
            if (!string.IsNullOrWhiteSpace(options.Language)) catalog.Append(" /Lang ").Append(PdfFile.TextString(options.Language));
            catalog.Append(" /ViewerPreferences << /DisplayDocTitle true >>");
            catalog.Append(CultureInfo.InvariantCulture, $" /Metadata {archive.Metadata(file, options, created)} 0 R");

            diagnostics.InsertRange(0, set.Diagnostics);
            if (file.Encrypted)
                catalog.Append(" /Extensions << /ADBE << /BaseVersion /1.7 /ExtensionLevel 8 >> >>");
            if (archiving)
            {
                catalog.Append(CultureInfo.InvariantCulture, $" /OutputIntents [{archive.OutputIntent(file)} 0 R]");
                if (fontObjects.Keys.Any(font => !font.Embedded))
                    diagnostics.Add(Diagnostic.Warning("PDF013", "Some text uses fonts that are not embedded, so the file is not PDF/A conformant."));
            }
            catalog.Append(" >>");
            var catalogId = file.WriteObject(catalog.ToString());

            file.Finish(catalogId, infoId, archive.Identifier(options, created));

            return OperationResult<int>.Ok(pageObjects.Count, diagnostics);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or InvalidDataException)
        {
            return OperationResult<int>.Fail(Diagnostic.Error("PDF100", $"Writing the PDF failed: {ex.Message}").With(ex.Message));
        }
    }

    private string Fingerprint(PageImage image)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        Span<byte> size = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(size, image.Width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(size[4..], image.Height);
        hash.AppendData(size);
        hash.AppendData(image.Rgb);
        hash.AppendData(image.Alpha is null ? [0] : [1]);
        if (image.Alpha is { } alpha) hash.AppendData(alpha);
        if (image.Source is { } source)
        {
            hash.AppendData(System.Text.Encoding.ASCII.GetBytes(source.MediaType));
            hash.AppendData(source.Data.Span);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string Points(Twip value) =>
        value.ToPoints().ToString("0.###", CultureInfo.InvariantCulture);
}
