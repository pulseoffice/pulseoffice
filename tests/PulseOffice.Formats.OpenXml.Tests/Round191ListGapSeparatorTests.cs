using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round191ListGapSeparatorTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Separators_between_each_pair_cross_docx()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.List.Replace(new ListStyle("Mixed",
        [
            new ListLevel(NumberFormat.Arabic),
            new ListLevel(NumberFormat.Arabic),
            new ListLevel(NumberFormat.Arabic) { DisplayLevels = 3, Separator = ".", Separators = [".", "-"], Suffix = string.Empty },
        ]));
        document.Body.Replace([new ParagraphBlock("p1", new PulseOffice.Text.Rich.TextParagraph("Item") { Runs = [new PulseOffice.Text.Rich.TextRun("Item")], ListStyleName = "Mixed", Numbered = true, BulletLevel = 2 })]);

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, false))
            Assert.Contains(package.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!.Descendants<W.LevelText>(), text => text.Val?.Value == "%1.%2-%3");
        stream.Position = 0;
        var level = _composer.Reader(DocxFlavour.Document).Read(stream).Value!.Styles.List.All.Single(list => list.Name == "Mixed").Levels[2];

        Assert.Equal(["." , "-"], level.Separators);
        Assert.Equal(".", level.Separator);
    }
}
