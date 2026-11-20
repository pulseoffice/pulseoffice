using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round291DateFieldStyleTests
{
    private const string NumberNamespace = "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0";

    private MemoryStream Written(string picture, string? language)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create()) { Language = language };
        TextRun[] runs = [new("x") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Format = picture } }];
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph("x") { Runs = runs })]);
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        return stream;
    }

    private XDocument Part(Stream stream, string name)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = archive.GetEntry(name)!.Open();
        return XDocument.Load(reader);
    }

    [Fact]
    public void Date_separators_are_written_as_the_document_language_shows_them()
    {
        using var stream = Written("dd/MM/yyyy", "de-DE");

        var style = Part(stream, "styles.xml").Descendants(XName.Get("date-style", NumberNamespace)).Single(date => date.Attribute(XName.Get("name", "urn:oasis:names:tc:opendocument:xmlns:style:1.0"))?.Value.StartsWith("PtF", StringComparison.Ordinal) == true);
        var texts = style.Elements(XName.Get("text", NumberNamespace)).Select(text => text.Value).ToList();

        Assert.Equal([".", "."], texts);
    }

    [Fact]
    public void An_era_part_crosses_the_odf_data_style()
    {
        using var stream = Written("gg yyyy", null);
        using var stripped = new MemoryStream();
        stream.Position = 0;
        stream.CopyTo(stripped);
        using (var archive = new ZipArchive(stripped, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var reader = entry.Open()) content = XDocument.Load(reader);
            content.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "format" && attribute.Name.NamespaceName == "urn:pulseoffice:text").Remove();
            entry.Delete();
            using var writer = archive.CreateEntry("content.xml").Open();
            content.Save(writer);
        }
        stripped.Position = 0;

        var read = new OdtReader().Read(stripped).Value!;

        Assert.Equal("gg yyyy", Assert.IsType<ParagraphBlock>(read.Body[0]).Content.Runs.Single(run => run.Field == TextFieldKind.Date).FieldDetail?.Format);
    }
}
