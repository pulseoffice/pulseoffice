using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxListThreadRound101Tests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void An_instance_with_a_start_override_is_its_own_list_and_the_first_list_resumes_after_it()
    {
        var read = Read(Package(Paragraph("A one", 1) + Paragraph("A two", 1) + Paragraph("B one", 2) + Paragraph("A three", 1)));
        var items = Items(read);
        Assert.Equal(["A one", "A two", "B one", "A three"], items.Select(item => item.Text));
        Assert.Null(items[0].ListId);
        Assert.NotNull(items[2].ListId);
        Assert.Null(items[3].ListId);
        Assert.Null(items[3].ListRestart);

        var expected = items.Select(item => (item.ListId, item.ListRestart)).ToList();
        for (var round = 0; round < 2; round++)
        {
            read = Read(Write(read));
            Assert.Equal(expected, Items(read).Select(item => (item.ListId, item.ListRestart)));
        }
    }

    [Fact]
    public void A_restarted_list_that_nothing_resumes_stays_a_plain_restart()
    {
        var items = Items(Read(Package(Paragraph("A one", 1) + Paragraph("B one", 2) + Paragraph("B two", 2))));
        Assert.All(items, item => Assert.Null(item.ListId));
        Assert.Equal([null, 1, null], items.Select(item => item.ListRestart));
    }

    [Fact]
    public void Model_threads_cross_the_docx_round_trip()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([Item("a1", "A1", null), Item("b1", "B1", "list1"), Item("a2", "A2", null), Item("b2", "B2", "list1")]);
        var read = Read(Write(document));
        Assert.Equal([null, "list1", null, "list1"], Items(read).Select(item => item.ListId));
        Assert.All(Items(read), item => Assert.Null(item.ListRestart));
    }

    [Fact]
    public void A_list_continues_across_a_section_through_the_docx_round_trip()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var section = new SectionBlock("s1", "Section1", new BlockContainer());
        section.Content.Add(Item("a2", "A2", null));
        document.Body.Replace([Item("a1", "A1", null), section, Item("a3", "A3", null)]);
        var read = Read(Write(document));
        var items = new BlockTree().Paragraphs(read.Body).Select(paragraph => paragraph.Content).Where(content => content.ListStyleName is not null).ToList();
        Assert.Equal(["A1", "A2", "A3"], items.Select(item => item.Text));
        Assert.All(items, item => Assert.Null(item.ListRestart));
        Assert.All(items, item => Assert.Null(item.ListId));
        Assert.Single(items.Select(item => item.ListStyleName).Distinct());
    }

    [Fact]
    public void A_word_section_break_inside_a_list_does_not_restart_it()
    {
        const string Break = "<w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr><w:sectPr><w:type w:val=\"nextPage\"/><w:pgSz w:w=\"16838\" w:h=\"11906\" w:orient=\"landscape\"/></w:sectPr></w:pPr><w:r><w:t>A two</w:t></w:r></w:p>";
        var read = Read(Package(Paragraph("A one", 1) + Break + Paragraph("A three", 1) + "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/></w:sectPr>"));
        var items = Items(read);
        Assert.Equal(["A one", "A two", "A three"], items.Select(item => item.Text));
        Assert.All(items, item => Assert.Null(item.ListRestart));
        Assert.All(items, item => Assert.Null(item.ListId));
        Assert.Single(items.Select(item => item.ListStyleName).Distinct());

        var again = Items(Read(Write(read)));
        Assert.Equal(["A one", "A two", "A three"], again.Select(item => item.Text));
        Assert.All(again, item => Assert.Null(item.ListRestart));
        Assert.All(again, item => Assert.Null(item.ListId));
        Assert.Single(again.Select(item => item.ListStyleName).Distinct());
    }

    [Fact]
    public void Custom_index_level_styles_cross_the_round_trip()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("Mine") { ParentName = DefaultStyleNames.Standard });
        document.Styles.Paragraph.Add(new ParagraphStyle("Top") { ParentName = DefaultStyleNames.Standard });
        var styles = PulseOffice.Documents.Indexes.IndexLevelStyles.Defaults.WithEntry(2, "Mine").WithHeading("Top");
        document.Body.Replace([
            new ParagraphBlock("h", new TextParagraph("Chapter") { Runs = [new TextRun("Chapter")], StyleName = DefaultStyleNames.HeadingLevel(1), OutlineLevel = 1 }),
            new SectionBlock("toc", "Contents", new BlockContainer([new ParagraphBlock("t", new TextParagraph("Contents") { Runs = [new TextRun("Contents")], StyleName = "Top" }), new ParagraphBlock("e", new TextParagraph("Chapter") { Runs = [new TextRun("Chapter")], StyleName = DefaultStyleNames.ContentsLevel(1) })]))
            {
                Index = new PulseOffice.Documents.Indexes.IndexDefinition(PulseOffice.Documents.Indexes.IndexKind.Contents) { Title = "Contents", LevelStyles = styles },
            }]);
        var read = RoundTrip(document);
        var index = new BlockTree().Blocks(read.Body).OfType<SectionBlock>().Single(section => section.IsIndex).Index!;
        Assert.Equal(styles, index.LevelStyles);
    }

    private TextDocument RoundTrip(TextDocument document) => Read(Write(document));

    private static ParagraphBlock Item(string id, string text, string? thread) =>
        new(id, new TextParagraph(text) { Runs = [new TextRun(text)], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = thread });

    private static List<TextParagraph> Items(TextDocument document) =>
        [.. new BlockTree().Paragraphs(document.Body).Select(paragraph => paragraph.Content).Where(content => content.ListStyleName is not null)];

    private TextDocument Read(Stream stream) => _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

    private MemoryStream Write(TextDocument document)
    {
        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        return written;
    }

    private static string Paragraph(string text, int num) =>
        $"<w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"{num}\"/></w:numPr></w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

    private static MemoryStream Package(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(
                $"<w:numbering {Namespace}><w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/></w:lvl></w:abstractNum>"
                + "<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>"
                + "<w:num w:numId=\"2\"><w:abstractNumId w:val=\"0\"/><w:lvlOverride w:ilvl=\"0\"><w:startOverride w:val=\"1\"/></w:lvlOverride></w:num></w:numbering>");
        }
        stream.Position = 0;
        return stream;
    }
}
