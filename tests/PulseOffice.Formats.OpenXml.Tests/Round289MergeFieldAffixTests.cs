using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round289MergeFieldAffixTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Text_before_and_after_a_merge_field_crosses_docx_and_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        TextRun[] runs = [new("«City»") { Field = TextFieldKind.Database, FieldArgument = "City", FieldDetail = new FieldDetail { WhenTrue = "in ", WhenFalse = "," } }];
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph("«City»") { Runs = runs })]);
        using var docx = new MemoryStream();
        using var odt = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        docx.Position = 0;
        odt.Position = 0;

        (string?, string?) Affixes(TextDocument read) =>
            Assert.IsType<ParagraphBlock>(read.Body[0]).Content.Runs.Single(run => run.Field == TextFieldKind.Database).FieldDetail is { } detail ? (detail.WhenTrue, detail.WhenFalse) : (null, null);

        Assert.Equal(("in ", ","), Affixes(_composer.Reader(DocxFlavour.Document).Read(docx).Value!));
        Assert.Equal(("in ", ","), Affixes(new OdtReader().Read(odt).Value!));
    }
}
