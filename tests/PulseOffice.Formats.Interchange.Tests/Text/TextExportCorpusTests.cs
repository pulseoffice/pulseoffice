using System.IO.Compression;
using System.Xml;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Interchange.Text.Epub;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Formats.Interchange.Text.Outline;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class TextExportCorpusTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa/extras/odfexport/data";

    [Fact]
    public void IDocumentiEsternoSiEsportanoInEpubEHtmlBenFormati()
    {
        Assert.SkipUnless(Directory.Exists(Corpus), "Reference corpus not available.");
        var resolver = new StyleResolver();
        var pages = new HtmlPageRenderer(new ParagraphSemantics(resolver, new StyleChain()), resolver, new DirectFormatting());
        var epub = new EpubDocumentWriter(pages, new HtmlFidelity(), new EpubPackageWriter());
        var html = new HtmlDocumentWriter(pages, new HtmlFidelity());
        var failures = new List<string>();
        var exported = 0;
        var files = Directory.EnumerateFiles(Corpus, "*.odt").Order(StringComparer.Ordinal).ToList();
        foreach (var file in files)
        {
            var read = new OdtReader().Read(new MemoryStream(File.ReadAllBytes(file)));
            if (!read.Succeeded) continue;
            try
            {
                using var book = new MemoryStream();
                if (!epub.Write(read.Value!, book).Succeeded) { failures.Add($"{Path.GetFileName(file)}: epub write failed"); continue; }
                book.Position = 0;
                using var zip = new ZipArchive(book);
                if (zip.Entries[0].FullName != "mimetype") failures.Add($"{Path.GetFileName(file)}: mimetype is not first");
                foreach (var entry in zip.Entries.Where(entry => entry.FullName.EndsWith(".xhtml", StringComparison.Ordinal) || entry.FullName.EndsWith(".opf", StringComparison.Ordinal)))
                {
                    using var reader = XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
                    while (reader.Read()) { }
                }
                using var page = new MemoryStream();
                if (!html.Write(read.Value!, page).Succeeded) failures.Add($"{Path.GetFileName(file)}: html write failed");
                exported++;
            }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                failures.Add($"{Path.GetFileName(file)}: {ex.GetType().Name} {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(exported >= 100, $"only {exported} of {files.Count} exported");
    }
}
