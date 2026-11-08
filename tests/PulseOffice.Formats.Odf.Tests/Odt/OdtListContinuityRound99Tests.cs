using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtListContinuityRound99Tests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    [Fact]
    public void A_header_list_neither_continues_nor_restarts_the_body_list()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([Item("b1", "Body one"), Item("b2", "Body two")]);
        var page = document.Styles.Page.Find(document.InitialPageStyle)!;
        page.Header.Enabled = true;
        page.Header.Default.Add(Item("h1", "Header item"));

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var styles = archive.GetEntry("styles.xml")!.Open();
            var headerLists = XDocument.Load(styles).Descendants(Text + "list").ToList();
            Assert.NotEmpty(headerLists);
            Assert.All(headerLists, list => Assert.Null(list.Attribute(Text + "continue-numbering")));
        }

        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;
        var first = read.Body.Blocks.OfType<ParagraphBlock>().First();
        Assert.Null(first.Content.ListRestart);
    }

    [Fact]
    public void A_body_list_interrupted_by_a_paragraph_continues_across_the_round_trip()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([Item("b1", "One"), new ParagraphBlock("p", "Between"), Item("b2", "Two")]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;
        Assert.All(read.Body.Blocks.OfType<ParagraphBlock>(), paragraph => Assert.Null(paragraph.Content.ListRestart));
    }

    [Fact]
    public void Legal_numbering_crosses_the_odt_round_trip()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.List.Add(new ListStyle("Legal", [new ListLevel(NumberFormat.UpperRoman), new ListLevel(NumberFormat.LowerLetter) { DisplayLevels = 2, Legal = true }]));
        document.Body.Replace([new ParagraphBlock("b1", new TextParagraph("x") { Runs = [new TextRun("x")], ListStyleName = "Legal", Numbered = true, BulletLevel = 1 })]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;
        var levels = read.Styles.List.Find("Legal")!.Levels;
        Assert.False(levels[0].Legal);
        Assert.True(levels[1].Legal);
    }

    private static ParagraphBlock Item(string id, string text) =>
        new(id, new TextParagraph(text) { Runs = [new TextRun(text)], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true });
}
