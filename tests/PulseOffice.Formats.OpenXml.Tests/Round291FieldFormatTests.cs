using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round291FieldFormatTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private (TextRun Docx, TextRun Odt) Crossed(TextRun field)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var paragraph = new TextParagraph("target " + field.Text) { Runs = [new("target "), field] };
        var block = new ParagraphBlock("p", paragraph);
        block.State = new ParagraphState(paragraph, [new BookmarkMark(0, "mark", MarkEdge.Start), new BookmarkMark(6, "mark", MarkEdge.End)]);
        document.Body.Replace([block]);
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
    [InlineData(ReferenceFormats.NumberNoContext)]
    [InlineData(ReferenceFormats.NumberFullContext)]
    public void Reference_number_contexts_cross_both_formats(string format)
    {
        var (docx, odt) = Crossed(new TextRun("1") { Field = TextFieldKind.Bookmark, FieldArgument = "mark", FieldDetail = new FieldDetail { Format = format } });

        Assert.Equal((format, format), (docx.FieldDetail?.Format, odt.FieldDetail?.Format));
    }

    [Fact]
    public void Chapter_field_level_crosses_both_formats()
    {
        var (docx, odt) = Crossed(new TextRun("x") { Field = TextFieldKind.Chapter, FieldDetail = new FieldDetail { Value = "2" } });

        Assert.Equal(("2", "2"), (docx.FieldDetail?.Value, odt.FieldDetail?.Value));
    }

    [Fact]
    public void Merge_field_case_and_number_picture_cross_both_formats()
    {
        var detail = new FieldDetail { Format = MergeCases.Upper, Value = "#,##0.00" };
        var (docx, odt) = Crossed(new TextRun("«Total»") { Field = TextFieldKind.Database, FieldArgument = "Total", FieldDetail = detail });

        Assert.Equal((detail, detail), (docx.FieldDetail, odt.FieldDetail));
    }
}
