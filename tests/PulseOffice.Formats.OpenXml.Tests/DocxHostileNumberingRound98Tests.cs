using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxHostileNumberingRound98Tests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";

    private static IReadOnlyList<ParagraphBlock> Read(string body, string numbering)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering($"<w:numbering {Namespace}>{numbering}</w:numbering>");
        }
        stream.Position = 0;
        var read = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return [.. read.Value!.Body.Blocks.OfType<ParagraphBlock>()];
    }

    [Theory]
    [InlineData(1000, ListStyle.LevelCount - 1)]
    [InlineData(-5, 0)]
    public void A_numbering_level_outside_the_range_is_clamped(int level, int expected)
    {
        var paragraphs = Read(
            $"<w:p><w:pPr><w:numPr><w:ilvl w:val=\"{level}\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>x</w:t></w:r></w:p>",
            "<w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>");

        Assert.Equal(expected, paragraphs[0].Content.BulletLevel);
    }

    [Fact]
    public void A_negative_outline_level_is_ignored()
    {
        var paragraphs = Read("<w:p><w:pPr><w:outlineLvl w:val=\"-3\"/></w:pPr><w:r><w:t>x</w:t></w:r></w:p>", string.Empty);

        Assert.Null(paragraphs[0].Content.OutlineLevel);
    }
}
