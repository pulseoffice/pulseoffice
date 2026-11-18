using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round198FolioPageQueueTests
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
    public void Columns_of_different_widths_cross_docx_as_word_columns()
    {
        var document = Document();
        var style = document.Styles.Page.Find(document.InitialPageStyle)!;
        style.Margins = Margins.Uniform(new Twip(1000));
        style.Columns = new SectionColumns(2, new Twip(720)) { Spans = [new ColumnSpan(new Twip(1), new Twip(720)), new ColumnSpan(new Twip(3), Twip.Zero)] };

        using var stream = Write(document);
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var columns = package.MainDocumentPart!.Document!.Descendants<W.Column>().Select(column => int.Parse(column.Width!.Value!)).ToList();
            Assert.Equal(2, columns.Count);
            Assert.InRange(columns[1], 3 * columns[0] - 3, 3 * columns[0] + 3);
        }
        var reread = Read(stream);
        var read = reread.Styles.Page.Find(reread.InitialPageStyle)!;

        Assert.True(read.Columns.Uneven);
        Assert.Equal(720, read.Columns.Spans[0].Gap.Value);
    }

    [Fact]
    public void Line_numbering_restarted_per_section_with_a_start_value_crosses_docx()
    {
        var document = Document();
        document.LineNumbering = new LineNumbering(1) { RestartEachSection = true, Start = 5 };

        using var stream = Write(document);
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var type = package.MainDocumentPart!.Document!.Descendants<W.LineNumberType>().First();
            Assert.Equal(W.LineNumberRestartValues.NewSection, type.Restart!.Value);
            Assert.Equal((short)4, type.Start!.Value);
        }
        var read = Read(stream).LineNumbering!;

        Assert.True(read.RestartEachSection);
        Assert.False(read.RestartEachPage);
        Assert.Equal(5, read.Start);
    }

    [Fact]
    public void A_top_gutter_crosses_docx_as_word_gutter_at_top()
    {
        var document = Document();
        var style = document.Styles.Page.Find(document.InitialPageStyle)!;
        style.Gutter = new Twip(567);
        style.GutterAtTop = true;

        using var stream = Write(document);
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            Assert.Equal(567u, package.MainDocumentPart!.Document!.Descendants<W.PageMargin>().First().Gutter!.Value);
            Assert.NotNull(package.MainDocumentPart.DocumentSettingsPart!.Settings!.GetFirstChild<W.GutterAtTop>());
        }
        var reread = Read(stream);
        var read = reread.Styles.Page.Find(reread.InitialPageStyle)!;

        Assert.Equal(567, read.Gutter.Value);
        Assert.True(read.GutterAtTop);
        Assert.Equal(style.Margins, read.Margins);
    }

    [Fact]
    public void Hiragana_page_numbers_become_full_width_katakana_and_are_declared()
    {
        var document = Document();
        document.Styles.Page.Find(document.InitialPageStyle)!.NumberFormat = NumberFormat.Hiragana;

        Assert.Contains("DOCX247", _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Select(loss => loss.Code));
        using var stream = Write(document);
        using var package = WordprocessingDocument.Open(stream, false);
        Assert.Equal(W.NumberFormatValues.AiueoFullWidth, package.MainDocumentPart!.Document!.Descendants<W.PageNumberType>().Single().Format!.Value);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1500, false)]
    public void Mirrored_margins_beside_a_symmetric_page_style_still_reach_word(int otherLeft, bool written)
    {
        var document = Document();
        document.Styles.Page.Find(document.InitialPageStyle)!.Usage = PageUsage.Mirrored;
        document.Styles.Page.Add(new PageStyle("Plain") { Margins = new Margins(new Twip(otherLeft), new Twip(1000), new Twip(1000), new Twip(1000)) });
        var paragraph = (PulseOffice.Documents.Blocks.ParagraphBlock)document.Body.Blocks[0];
        paragraph.Content = paragraph.Content with { PageStyleName = "Plain" };

        Assert.Equal(!written, _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Any(loss => loss.Code == "DOCX037"));
        using var stream = Write(document);
        using var package = WordprocessingDocument.Open(stream, false);
        Assert.Equal(written, package.MainDocumentPart!.DocumentSettingsPart!.Settings!.GetFirstChild<W.MirrorMargins>() is not null);
    }
}
