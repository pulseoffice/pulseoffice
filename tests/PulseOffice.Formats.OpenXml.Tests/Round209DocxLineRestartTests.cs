using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round209DocxLineRestartTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Document() => new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();

    private MemoryStream Write(TextDocument document)
    {
        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private TextDocument Read(MemoryStream stream)
    {
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    private bool Declared(TextDocument document) =>
        _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Any(loss => loss.Code == "DOCX248");

    [Fact]
    public void A_restart_with_another_value_in_continuous_numbering_becomes_a_section_start()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1);
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("due") { LineNumberRestart = 7 }));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("tre")));

        Assert.False(Declared(document));
        using var stream = Write(document);
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var types = package.MainDocumentPart!.Document!.Descendants<W.LineNumberType>().ToList();
            Assert.Equal(2, types.Count);
            Assert.Equal(W.LineNumberRestartValues.Continuous, types[0].Restart!.Value);
            Assert.Equal(W.LineNumberRestartValues.NewSection, types[1].Restart!.Value);
            Assert.Equal(6, types[1].Start!.Value);
        }

        var read = Read(stream);
        Assert.False(read.LineNumbering!.RestartEachSection);
        Assert.Equal(1, read.LineNumbering.Start);
        var paragraphs = read.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(7, paragraphs[^2].Content.LineNumberRestart);
        Assert.Null(paragraphs[^1].Content.LineNumberRestart);
    }

    [Fact]
    public void A_restart_on_a_page_style_change_keeps_its_value()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1) { RestartEachSection = true, Start = 3 };
        document.Styles.Page.Add(new PageStyle("Wide") { Margins = new Margins(new Twip(500), new Twip(1000), new Twip(500), new Twip(1000)) });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("due") { PageStyleName = "Wide", LineNumberRestart = 10 }));

        Assert.False(Declared(document));
        var read = Read(Write(document));

        Assert.Equal(3, read.LineNumbering!.Start);
        Assert.Equal(10, read.Body.Blocks.OfType<ParagraphBlock>().Last().Content.LineNumberRestart);
    }

    [Fact]
    public void A_restart_opening_a_column_section_is_written_on_that_section()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1);
        var lead = new ParagraphBlock(document.NextId("p"), new TextParagraph("uno") { LineNumberRestart = 5 });
        document.Body.Add(new SectionBlock(document.NextId("s"), "Columns", new BlockContainer([lead])) { Columns = new SectionColumns(2, new Twip(400)) });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("dopo")));

        Assert.False(Declared(document));
        var read = Read(Write(document));

        var section = read.Body.Blocks.OfType<SectionBlock>().Single();
        Assert.Equal(5, section.Content.Blocks.OfType<ParagraphBlock>().First().Content.LineNumberRestart);
        Assert.Null(read.Body.Blocks.OfType<ParagraphBlock>().Last().Content.LineNumberRestart);
    }

    [Fact]
    public void A_restart_inside_a_table_is_still_declared()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1);
        var cell = new TableCell(new BlockContainer([new ParagraphBlock(document.NextId("p"), new TextParagraph("cella") { LineNumberRestart = 4 })]));
        document.Body.Add(new TableBlock(document.NextId("t"), [new TableRow([cell])], [Twip.FromCentimeters(5)]));

        Assert.True(Declared(document));
    }
}
