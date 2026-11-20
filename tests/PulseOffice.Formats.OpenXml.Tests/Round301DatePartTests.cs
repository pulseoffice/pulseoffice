using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round301DatePartTests
{
    [Fact]
    public void Quarter_and_week_parts_cross_the_odf_data_style()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        TextRun[] runs = [new("x") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Format = "QQ yyyy W" } }];
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph("x") { Runs = runs })]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
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

        Assert.Equal("QQ yyyy W", Assert.IsType<ParagraphBlock>(read.Body[0]).Content.Runs.Single(run => run.Field == TextFieldKind.Date).FieldDetail?.Format);
    }

    [Fact]
    public void Quarter_and_week_parts_render_and_survive_word_pictures()
    {
        var text = new FieldClockFormatter().Format(TextFieldKind.Date, new DateTime(2026, 8, 3), "Q 'Q' WW", CultureInfo.GetCultureInfo("it-IT"));
        var pictures = new WordDatePictures();

        Assert.Equal("Q3 Q 32", text);
        Assert.Equal("'Q' yyyy 'W'", pictures.ToWord("Q yyyy W"));
    }
}

public sealed class Round301HeaderVariantTests
{
    private const string StyleNamespace = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    [Fact]
    public void A_hidden_blank_left_header_means_the_same_header_on_both_sides()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p", "body")]);
        var page = document.Styles.Page.All.First();
        page.Header.Enabled = true;
        page.Header.Default.Replace([new ParagraphBlock("h", "Head")]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        using var edited = new MemoryStream();
        stream.Position = 0;
        stream.CopyTo(edited);
        using (var archive = new ZipArchive(edited, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("styles.xml")!;
            XDocument styles;
            using (var reader = entry.Open()) styles = XDocument.Load(reader);
            var header = styles.Descendants(XName.Get("header", StyleNamespace)).First();
            header.AddAfterSelf(new XElement(XName.Get("header-left", StyleNamespace), new XAttribute(XName.Get("display", StyleNamespace), "false"), new XElement(XName.Get("p", TextNamespace))));
            entry.Delete();
            using var writer = archive.CreateEntry("styles.xml").Open();
            styles.Save(writer);
        }
        edited.Position = 0;

        var read = new OdtReader().Read(edited).Value!.Styles.Page.All.First(style => style.Header.Enabled);

        Assert.Null(read.Header.Even);
        Assert.False(read.EvenPagesDifferent);
    }
}
