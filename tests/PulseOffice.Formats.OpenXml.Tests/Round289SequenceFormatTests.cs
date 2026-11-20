using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round289SequenceFormatTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Document()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        TextRun[] runs =
        [
            new("Figure "),
            new("ii") { Field = TextFieldKind.Sequence, FieldArgument = "Figure", FieldDetail = new FieldDetail { Value = SequenceFormats.LowerRoman } },
        ];
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph("Figure ii") { Runs = runs })]);
        return document;
    }

    private string? Shape(TextDocument document) =>
        Assert.IsType<ParagraphBlock>(document.Body[0]).Content.Runs.Single(run => run.Field == TextFieldKind.Sequence).FieldDetail?.Value;

    [Fact]
    public void Sequence_numbering_format_crosses_docx()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Document(), stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(SequenceFormats.LowerRoman, Shape(_composer.Reader(DocxFlavour.Document).Read(stream).Value!));
    }

    [Fact]
    public void Sequence_numbering_format_crosses_odt()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Document(), stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(SequenceFormats.LowerRoman, Shape(new OdtReader().Read(stream).Value!));
    }

    [Fact]
    public void Sequence_numerals_follow_the_format()
    {
        var numerals = new SequenceNumerals();

        Assert.Equal(["4", "iv", "IV", "d", "AA"], [numerals.Format(4, null), numerals.Format(4, "i"), numerals.Format(4, "I"), numerals.Format(4, "a"), numerals.Format(27, "A")]);
    }

    [Fact]
    public void Page_number_numeral_format_crosses_docx_and_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        TextRun[] runs = [new("i") { Field = TextFieldKind.PageNumber, FieldDetail = new FieldDetail { Value = SequenceFormats.LowerRoman } }];
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph("i") { Runs = runs })]);
        using var docx = new MemoryStream();
        using var odt = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        docx.Position = 0;
        odt.Position = 0;

        string? Page(TextDocument read) => Assert.IsType<ParagraphBlock>(read.Body[0]).Content.Runs.Single(run => run.Field == TextFieldKind.PageNumber).FieldDetail?.Value;

        Assert.Equal(SequenceFormats.LowerRoman, Page(_composer.Reader(DocxFlavour.Document).Read(docx).Value!));
        Assert.Equal(SequenceFormats.LowerRoman, Page(new OdtReader().Read(odt).Value!));
    }
}
