using System.IO.Compression;
using System.Xml;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Interchange.Text.Epub;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Formats.Interchange.Text.Outline;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class TextEpubTests
{
    private static HtmlPageRenderer Pages()
    {
        var resolver = new StyleResolver();
        return new HtmlPageRenderer(new ParagraphSemantics(resolver, new StyleChain()), resolver, new DirectFormatting());
    }

    private static TextDocument Document()
    {
        var document = TextInterchange.Rich();
        document.Properties.Title = "Libro di prova";
        document.Properties.Author = "Anna";
        document.Language = "it-IT";
        return document;
    }

    private static XmlDocument Xml(string text)
    {
        var xml = new XmlDocument();
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        xml.Load(reader);
        return xml;
    }

    private static string Entry(ZipArchive zip, string name)
    {
        using var stream = zip.GetEntry(name)!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void LEpubHaMimetypeInChiaroPerPrimoEUnPacchettoValido()
    {
        var bytes = TextInterchange.Bytes(Document(), new EpubDocumentWriter(Pages(), new HtmlFidelity(), new EpubPackageWriter()));

        Assert.Equal("mimetype", System.Text.Encoding.ASCII.GetString(bytes, 30, 8));
        Assert.Equal("application/epub+zip", System.Text.Encoding.ASCII.GetString(bytes, 38, 20));
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal("mimetype", zip.Entries[0].FullName);
        Assert.Equal(zip.Entries[0].Length, zip.Entries[0].CompressedLength);
        var opf = Xml(Entry(zip, "OEBPS/content.opf"));
        var ns = new XmlNamespaceManager(opf.NameTable);
        ns.AddNamespace("o", "http://www.idpf.org/2007/opf");
        ns.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
        Assert.Equal("Libro di prova", opf.SelectSingleNode("//dc:title", ns)!.InnerText);
        Assert.Equal("it-IT", opf.SelectSingleNode("//dc:language", ns)!.InnerText);
        Assert.NotNull(opf.SelectSingleNode("//o:meta[@property='dcterms:modified']", ns));
        Assert.NotNull(opf.SelectSingleNode("//o:item[@properties='nav']", ns));
        foreach (XmlElement item in opf.SelectNodes("//o:manifest/o:item", ns)!)
            Assert.NotNull(zip.GetEntry("OEBPS/" + item.GetAttribute("href")));
    }

    [Fact]
    public void IlTestoEIlSommarioSonoXhtmlBenFormato()
    {
        var bytes = TextInterchange.Bytes(Document(), new EpubDocumentWriter(Pages(), new HtmlFidelity(), new EpubPackageWriter()));
        using var zip = new ZipArchive(new MemoryStream(bytes));

        var text = Entry(zip, "OEBPS/text.xhtml");
        var navigation = Entry(zip, "OEBPS/nav.xhtml");
        Xml(text);
        Xml(navigation);
        Xml(Entry(zip, "OEBPS/toc.ncx"));

        Assert.Contains("epub:type=\"toc\"", navigation, StringComparison.Ordinal);
        Assert.Contains("text.xhtml#h1", navigation, StringComparison.Ordinal);
        Assert.Contains("id=\"h1\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<br>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LeImmaginiDiventanoFileNelPacchetto()
    {
        var bytes = TextInterchange.Bytes(Document(), new EpubDocumentWriter(Pages(), new HtmlFidelity(), new EpubPackageWriter()));
        using var zip = new ZipArchive(new MemoryStream(bytes));

        var text = Entry(zip, "OEBPS/text.xhtml");

        Assert.Contains(zip.Entries, entry => entry.FullName.StartsWith("OEBPS/images/image1.", StringComparison.Ordinal));
        Assert.Contains("src=\"images/image1.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LXhtmlSingoloEBenFormatoConImmaginiIncluse()
    {
        var bytes = TextInterchange.Bytes(Document(), new XhtmlDocumentWriter(Pages(), new HtmlFidelity()));
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        Xml(text);
        Assert.StartsWith("<?xml", text, StringComparison.Ordinal);
        Assert.Contains("xmlns=\"http://www.w3.org/1999/xhtml\"", text, StringComparison.Ordinal);
        Assert.Contains("data:image", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CaratteriDiControlloNelTestoNonRendonoMalformatoLXhtml()
    {
        var document = Document();
        document.Properties.Title = "Titolo\u0001";
        document.Body.Replace([new PulseOffice.Documents.Blocks.ParagraphBlock("p1", "a\u0001b\u000Bc\uFFFEd \uD800 e")]);

        var bytes = TextInterchange.Bytes(document, new XhtmlDocumentWriter(Pages(), new HtmlFidelity()));

        var xml = Xml(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Contains("abcd", xml.InnerText, StringComparison.Ordinal);
    }

    [Fact]
    public void LeVociDiFedeltaDellEpubHannoIlLoroCodice()
    {
        var report = new EpubDocumentWriter(Pages(), new HtmlFidelity(), new EpubPackageWriter()).CheckFidelity(Document());

        Assert.All(report.Losses, loss => Assert.StartsWith("EPUB", loss.Code, StringComparison.Ordinal));
    }
}
