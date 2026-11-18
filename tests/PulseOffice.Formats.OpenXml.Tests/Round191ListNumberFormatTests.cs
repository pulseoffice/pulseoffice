using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round191ListNumberFormatTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Theory]
    [InlineData(NumberFormat.DecimalZero)]
    [InlineData(NumberFormat.OrdinalText)]
    [InlineData(NumberFormat.Hebrew)]
    [InlineData(NumberFormat.Hiragana)]
    [InlineData(NumberFormat.ChineseCounting)]
    [InlineData(NumberFormat.RussianUpper)]
    public void Word_number_formats_cross_docx(NumberFormat format)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.List.Replace(new ListStyle("Wide", [new ListLevel(format)]));
        document.Body.Replace([new ParagraphBlock("p1", new PulseOffice.Text.Rich.TextParagraph("Item") { Runs = [new PulseOffice.Text.Rich.TextRun("Item")], ListStyleName = "Wide", Numbered = true })]);

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.Equal(format, read.Value!.Styles.List.All.Single(list => list.Name == "Wide").Levels[0].Format);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX244");
    }
}
