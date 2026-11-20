using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round289DateFieldStyleTests
{
    [Fact]
    public void Date_field_format_is_written_as_an_odf_data_style_and_read_back_from_it()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        TextRun[] runs = [new("05 March 2026") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Format = "dd MMMM yyyy" } }];
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph("05 March 2026") { Runs = runs })]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var reader = entry.Open()) content = XDocument.Load(reader);
            var date = content.Descendants(XName.Get("date", "urn:oasis:names:tc:opendocument:xmlns:text:1.0")).Single();
            Assert.NotNull(date.Attribute(XName.Get("data-style-name", "urn:oasis:names:tc:opendocument:xmlns:style:1.0")));
            date.Attributes().Where(attribute => attribute.Name.LocalName == "format" && attribute.Name.NamespaceName == "urn:pulseoffice:text").Remove();
            entry.Delete();
            using var writer = archive.CreateEntry("content.xml").Open();
            content.Save(writer);
        }
        stream.Position = 0;

        var read = new OdtReader().Read(stream).Value!;

        Assert.Equal("dd MMMM yyyy", Assert.IsType<ParagraphBlock>(read.Body[0]).Content.Runs.Single(run => run.Field == TextFieldKind.Date).FieldDetail?.Format);
    }
}
