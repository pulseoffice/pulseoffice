using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocumentsSilentLossAuditTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static readonly string[] NewRules = ["DOCX035", "DOCX036", "DOCX037", "DOCX038", "DOCX039", "DOCX040", "DOCX041"];

    private TextDocument NewDocument()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("Testo") { StyleName = DefaultStyleNames.Standard }));
        return document;
    }

    private TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    private IReadOnlyList<string> Losses(TextDocument document) =>
        [.. _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Select(loss => loss.Code)];

    [Fact]
    public void LInterlineaProporzionaleDegliStiliAttraversaDocx()
    {
        var document = NewDocument();
        document.Styles.Paragraph.Replace(new ParagraphStyle("Arioso") { ParentName = DefaultStyleNames.Standard, Paragraph = new ParagraphProperties { LineSpacing = 1.5 } });

        var style = RoundTrip(document).Styles.Paragraph.Find("Arioso")!;

        Assert.Equal(1.5, style.Paragraph.LineHeight?.Value ?? style.Paragraph.LineSpacing);
    }

    [Fact]
    public void UnDocumentoPredefinitoNonDichiaraPerditeNuove()
    {
        Assert.Empty(Losses(NewDocument()).Intersect(NewRules));
    }

    [Fact]
    public void LeProprietaSenzaEquivalenteInWordSonoDichiarate()
    {
        var document = NewDocument();
        document.Styles.Paragraph.Replace(new ParagraphStyle("Capitolo")
        {
            ParentName = DefaultStyleNames.Standard,
            Paragraph = new ParagraphProperties { PageNumberRestart = 1, Hyphenation = ParagraphHyphenation.On with { MinimumWordLength = 8 } },
        });
        document.Styles.Page.Find(DefaultStyleNames.Standard)!.Usage = PageUsage.Mirrored;
        document.Styles.Page.Find(DefaultStyleNames.Standard)!.Columns = new SectionColumns(2, PulseOffice.Foundation.Units.Twip.FromCentimeters(1)) { Balanced = false };
        var list = document.Styles.List.All.First();
        list.Levels = [.. list.Levels.Select(level => level with { CharacterStyleName = "Numerazione" })];
        document.Styles.Frame.Replace(new FrameStyle("Riquadro mio"));
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph("Capolettera") { StyleName = DefaultStyleNames.Standard, DropCap = new DropCap(3, 0) { WholeWord = true } }));

        Assert.Equal(NewRules, Losses(document).Intersect(NewRules).Order());
    }
}
