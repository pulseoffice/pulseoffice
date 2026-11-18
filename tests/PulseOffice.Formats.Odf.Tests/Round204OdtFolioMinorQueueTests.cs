using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round204OdtFolioMinorQueueTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    private TextDocument Document() => new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();

    private MemoryStream Write(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private TextDocument Read(MemoryStream stream)
    {
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }

    private List<string> LineNumberAttributes(MemoryStream stream)
    {
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var content = zip.GetEntry("content.xml")!.Open();
        return XDocument.Load(content).Descendants().Attributes(Text + "line-number").Select(attribute => attribute.Value).ToList();
    }

    [Fact]
    public void The_line_numbering_start_and_section_restarts_are_written_as_standard_paragraph_restarts()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1) { RestartEachSection = true, Start = 5 };
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new PulseOffice.Text.Rich.TextParagraph("due") { PageStyleName = DefaultStyleNames.Standard }));

        using var stream = Write(document);

        Assert.Equal(["5", "5"], LineNumberAttributes(stream));
        var read = Read(stream);
        Assert.Equal(5, read.LineNumbering!.Start);
        Assert.True(read.LineNumbering.RestartEachSection);
        Assert.All(read.Body.Blocks.OfType<ParagraphBlock>(), paragraph => Assert.Null(paragraph.Content.LineNumberRestart));
    }

    [Fact]
    public void A_paragraph_line_number_restart_crosses_odt()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1);
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new PulseOffice.Text.Rich.TextParagraph("due") { LineNumberRestart = 9 }));

        using var stream = Write(document);

        Assert.Equal(["9"], LineNumberAttributes(stream));
        Assert.Equal(9, Read(stream).Body.Blocks.OfType<ParagraphBlock>().Last().Content.LineNumberRestart);
    }
}
