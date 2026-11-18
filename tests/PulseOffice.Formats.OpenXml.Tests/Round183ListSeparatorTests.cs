using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round183ListSeparatorTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_level_separator_crosses_docx()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.List.Replace(new ListStyle("Dashed", [new ListLevel(NumberFormat.Arabic), new ListLevel(NumberFormat.Arabic) { DisplayLevels = 2, Separator = "-" }]));
        document.Body.Replace([new ParagraphBlock("p1", new PulseOffice.Text.Rich.TextParagraph("Item") { Runs = [new PulseOffice.Text.Rich.TextRun("Item")], ListStyleName = "Dashed", Numbered = true, BulletLevel = 1 })]);

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, false))
            Assert.Contains(package.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!.Descendants<W.LevelText>(), text => text.Val?.Value == "%1-%2.");
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.Equal("-", read.Value!.Styles.List.All.Single(list => list.Name == "Dashed").Levels[1].Separator);
    }
}
