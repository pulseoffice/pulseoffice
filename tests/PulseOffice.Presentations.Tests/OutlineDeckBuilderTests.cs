using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class OutlineDeckBuilderTests
{
    [Fact]
    public void ITitoliDiPrimoLivelloDiventanoDiapositiveEGliAltriPunti()
    {
        var theme = new ThemeCatalog().Default;
        var deck = new Presentation { Theme = theme, Master = new MasterFactory().CreateStandard(theme) };

        new OutlineDeckBuilder(new SlideFactory()).Fill(deck,
        [
            new OutlineLine(1, "Introduzione"),
            new OutlineLine(2, "Scopo"),
            new OutlineLine(3, "Dettaglio"),
            new OutlineLine(1, "Conclusioni"),
        ]);

        Assert.Equal(2, deck.Slides.Count);
        Assert.Equal("Introduzione", deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
        var body = deck.Slides[0].Placeholder(PlaceholderRole.Body)!.Text.Paragraphs;
        Assert.Equal(["Scopo", "Dettaglio"], body.Select(paragraph => paragraph.Text));
        Assert.Equal([0, 1], body.Select(paragraph => paragraph.BulletLevel));
        Assert.Equal("Conclusioni", deck.Slides[1].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
    }
}
