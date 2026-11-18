using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round179FolioListTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_tenth_level_item_is_written_on_word_s_last_level_and_declared()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([
            new ParagraphBlock("a", new TextParagraph("Deep") { Runs = [new TextRun("Deep")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, BulletLevel = 9, ListRestart = 3 }),
        ]);

        Assert.Contains("DOCX243", _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Select(loss => loss.Code));

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using var package = WordprocessingDocument.Open(stream, false);
        var main = package.MainDocumentPart!;
        Assert.All(main.Document!.Descendants<W.NumberingLevelReference>(), level => Assert.InRange(level.Val!.Value, 0, 8));
        Assert.All(main.NumberingDefinitionsPart!.Numbering!.Descendants<W.LevelOverride>(), over => Assert.InRange(over.LevelIndex!.Value, 0, 8));
    }

    [Fact]
    public void A_ninth_level_item_is_not_declared()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([
            new ParagraphBlock("a", new TextParagraph("Deep") { Runs = [new TextRun("Deep")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, BulletLevel = 8 }),
        ]);

        Assert.DoesNotContain("DOCX243", _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Select(loss => loss.Code));
    }

    [Theory]
    [InlineData("hex", true)]
    [InlineData("koreanCounting", true)]
    [InlineData("decimalZero", false)]
    [InlineData("lowerRoman", false)]
    public void Word_number_formats_without_a_model_format_are_declared(string format, bool declared)
    {
        var numbering = $"<w:numbering {Namespace}><w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:numFmt w:val=\"{format}\"/><w:lvlText w:val=\"%1.\"/></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num></w:numbering>";
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(numbering));
        Assert.NotNull(read.Value);
        Assert.Equal(declared, read.Diagnostics.Any(diagnostic => diagnostic.Code == "DOCX244"));
    }

    private static MemoryStream Docx(string numbering)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr><w:r><w:t>item</w:t></w:r></w:p></w:body></w:document>");
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(numbering);
        }

        stream.Position = 0;
        return stream;
    }
}
