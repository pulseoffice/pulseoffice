using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round215OdtListSuppressionTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

    [Fact]
    public void A_paragraph_that_switches_off_its_style_list_is_written_outside_a_list_with_an_empty_list_style_and_read_back()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("Listed") { ParentName = DefaultStyleNames.Standard, ListStyleName = DefaultStyleNames.List1 });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("off") { StyleName = "Listed", ListSuppressed = true }));

        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        XDocument content;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var entry = zip.GetEntry("content.xml")!.Open())
            content = XDocument.Load(entry);
        var paragraph = content.Descendants(Text + "p").Single(element => element.Value == "off");
        Assert.DoesNotContain(paragraph.Ancestors(), ancestor => ancestor.Name == Text + "list");
        var automatic = content.Descendants(Style + "style").Single(style => (string?)style.Attribute(Style + "name") == (string?)paragraph.Attribute(Text + "style-name"));
        Assert.Equal(string.Empty, (string?)automatic.Attribute(Style + "list-style-name"));

        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;
        var block = read.Body.Blocks.OfType<ParagraphBlock>().Single(item => item.Text == "off");
        Assert.True(block.Content.ListSuppressed);
        Assert.Equal("Listed", block.Content.StyleName);
    }
}
