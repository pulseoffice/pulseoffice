using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Rich.Bidi;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckBidiParityTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Sample()
    {
        var theme = _themes.Find("Sand")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Standard };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        var body = slide.Placeholder(PlaceholderRole.Body)!;
        slide.Replace(body with
        {
            Text = body.Text with
            {
                Paragraphs =
                [
                    new TextParagraph("שלום") { Direction = TextDirection.RightToLeft, Align = TextAlign.Left, MarginLeft = 20, MarginRight = 4 },
                    new TextParagraph("hello") { Direction = TextDirection.LeftToRight, Align = TextAlign.Right },
                ],
            },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private IReadOnlyList<TextParagraph> Body(Presentation deck) => deck.Slides[0].Placeholder(PlaceholderRole.Body)!.Text.Paragraphs;

    private Presentation ViaOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        new OdpWriter().Write(deck, buffer);
        buffer.Position = 0;
        return new OdpReader(_themes, _masters).Read(buffer).Value!;
    }

    private Presentation ViaPptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        new PptxWriter().Write(deck, buffer);
        buffer.Position = 0;
        return new PptxReader(_themes, _masters).Read(buffer).Value!;
    }

    private string Describe(IReadOnlyList<TextParagraph> paragraphs) =>
        string.Join("|", paragraphs.Select(paragraph => $"{paragraph.Text}:{paragraph.Direction}:{paragraph.Align}:{paragraph.MarginLeft}:{paragraph.MarginRight}"));

    [Fact]
    public void ShapeParagraphDirectionsCrossOdpAndPptxAlike()
    {
        var expected = Describe(Body(Sample()));

        Assert.Equal(expected, Describe(Body(ViaOdp(Sample()))));
        Assert.Equal(expected, Describe(Body(ViaPptx(Sample()))));
    }
}
