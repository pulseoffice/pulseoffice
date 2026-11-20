using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round305FieldTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private (TextRun Docx, TextRun Odt) Crossed(TextRun field)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph(field.Text) { Runs = [field] })]);
        using var docx = new MemoryStream();
        using var odt = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        docx.Position = 0;
        odt.Position = 0;
        TextRun Field(TextDocument read) => Assert.IsType<ParagraphBlock>(read.Body[0]).Content.Runs.Single(run => run.Field == field.Field);
        return (Field(_composer.Reader(DocxFlavour.Document).Read(docx).Value!), Field(new OdtReader().Read(odt).Value!));
    }

    [Theory]
    [InlineData(TextFieldKind.SectionNumber)]
    [InlineData(TextFieldKind.SectionPageCount)]
    public void Section_fields_cross_both_formats_with_their_numerals(TextFieldKind kind)
    {
        var (docx, odt) = Crossed(new TextRun("2") { Field = kind, FieldDetail = new FieldDetail { Value = SequenceFormats.LowerRoman } });

        Assert.Equal((SequenceFormats.LowerRoman, SequenceFormats.LowerRoman), (docx.FieldDetail?.Value, odt.FieldDetail?.Value));
    }

    [Fact]
    public void Word_section_keywords_are_live_fields()
    {
        var fields = new DocxFieldInstructions(new PreservedFieldTable(), new WordDatePictures());

        Assert.Equal(TextFieldKind.SectionPageCount, fields.Parse(" SECTIONPAGES ")!.Kind);
        Assert.Equal(TextFieldKind.SectionNumber, fields.Parse(" SECTION \\* MERGEFORMAT ")!.Kind);
        Assert.Equal(" SECTIONPAGES ", fields.Instruction(new TextRun("3") { Field = TextFieldKind.SectionPageCount }, new Dictionary<string, string>()));
    }

    [Fact]
    public void Quarter_and_week_parts_are_quoted_for_word_and_restored_from_the_field_data()
    {
        var fields = new DocxFieldInstructions(new PreservedFieldTable(), new WordDatePictures());
        var run = new TextRun("x") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Format = "QQ yyyy W" } };

        Assert.Equal(" DATE \\@ \"'QQ' yyyy 'W'\" ", fields.Instruction(run, new Dictionary<string, string>()));
        Assert.Equal("QQ yyyy W", Crossed(run).Docx.FieldDetail?.Format);
    }

    [Fact]
    public void An_unquoted_q_in_a_word_picture_stays_a_letter()
    {
        Assert.Equal("\\Q yyyy", new WordDatePictures().FromWord("Q yyyy"));
    }
}
