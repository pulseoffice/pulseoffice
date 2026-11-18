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

public sealed class Round204DocxFolioMinorQueueTests
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

    [Fact]
    public void A_section_line_number_restart_becomes_a_continuous_word_section_and_comes_back()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1) { RestartEachSection = true, Start = 2 };
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("due") { LineNumberRestart = 2 }));

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX248");
        using var stream = Write(document);
        using (var package = WordprocessingDocument.Open(stream, false))
            Assert.Equal(2, package.MainDocumentPart!.Document!.Descendants<W.SectionProperties>().Count());

        var read = Read(stream);
        Assert.DoesNotContain(read.Styles.Page.All, style => style.Name.StartsWith("Converted", StringComparison.Ordinal));
        Assert.Equal(2, read.Body.Blocks.OfType<ParagraphBlock>().Last().Content.LineNumberRestart);
    }

    [Fact]
    public void A_restart_word_cannot_express_is_declared()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1) { RestartEachPage = true };
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("due") { LineNumberRestart = 4 }));

        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX248");
    }

    [Fact]
    public void Word_mirrored_margins_leave_symmetric_page_styles_unmirrored()
    {
        var document = Document();
        var standard = document.Styles.Page.Find(document.InitialPageStyle)!;
        standard.Usage = PageUsage.Mirrored;
        standard.Margins = new Margins(new Twip(1800), new Twip(1000), new Twip(1000), new Twip(1000));
        document.Styles.Page.Add(new PageStyle("Plain") { Margins = new Margins(new Twip(1000), new Twip(1000), new Twip(1000), new Twip(1000)) });
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("due") { PageStyleName = "Plain" }));

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX037");
        var read = Read(Write(document));

        Assert.Equal(PageUsage.Mirrored, read.Styles.Page.Find(read.InitialPageStyle)!.Usage);
        var other = read.Body.Blocks.OfType<ParagraphBlock>().Last().Content.PageStyleName!;
        Assert.Equal(PageUsage.All, read.Styles.Page.Find(other)!.Usage);
    }
}
