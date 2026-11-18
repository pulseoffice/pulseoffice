using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round228LinkTipTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private TextDocument Folio()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("See site").WithRuns([
            new TextRun("See "),
            new TextRun("site") { Hyperlink = "https://example.org", HyperlinkTip = "Open the site" }]))]);
        return document;
    }

    private TextRun LinkedRun(TextDocument document) =>
        document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(block => block.Content.Runs).Single(run => run.Hyperlink is not null);

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    [Fact]
    public void A_folio_link_tip_crosses_docx_and_odt()
    {
        Assert.Equal("Open the site", LinkedRun(Through(Folio(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document))).HyperlinkTip);
        Assert.Equal("Open the site", LinkedRun(Through(Folio(), new OdtWriter(), new OdtReader())).HyperlinkTip);
    }

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[0].Add(new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(4)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("Site").WithRuns([new TextRun("Site") { Hyperlink = "https://example.org/a", HyperlinkTip = "Tip" }])]),
        });
        deck.Slides[0].AddInteraction(new InteractiveAction { TargetId = "box", Kind = InteractionKind.Hyperlink, Reference = "https://example.org/b", Tip = "Box tip" });
        return deck;
    }

    private (string? Run, string? Element) Tips(Presentation deck) =>
        (deck.Slides[0].Elements.OfType<TextElement>().SelectMany(element => element.Text.Paragraphs).SelectMany(paragraph => paragraph.Runs).Single(run => run.Hyperlink is not null).HyperlinkTip,
         Assert.Single(deck.Slides[0].Interactions).Tip);

    [Fact]
    public void Deck_link_tips_cross_pptx_and_odp()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), pptx).Succeeded);
        pptx.Position = 0;
        Assert.Equal(("Tip", "Box tip"), Tips(new PptxReader(_themes, _masters).Read(pptx).Value!));

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), odp).Succeeded);
        odp.Position = 0;
        Assert.Equal(("Tip", "Box tip"), Tips(new OdpReader(_themes, _masters).Read(odp).Value!));
    }
}
