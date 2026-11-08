using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class MasterBackgroundColorTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var master = _masters.CreateStandard(_themes.Default) with { BackgroundColor = 0xFF729FCFu };
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleOnly));
        return deck;
    }

    [Fact]
    public void LoSfondoEsplicitoDelloSchemaAttraversaOdpEPptx()
    {
        var odp = ThroughOdp(Deck());
        var pptx = ThroughPptx(Deck());
        var crossed = ThroughPptx(ThroughOdp(Deck()));

        foreach (var deck in new[] { odp, pptx, crossed })
        {
            Assert.Equal(0xFF729FCFu, deck.Master.BackgroundColor);
            Assert.Equal(0xFF729FCFu, deck.Master.BackgroundOf(deck.Theme, SlideLayout.TitleOnly));
            Assert.Null(deck.Slides[0].Background);
        }
    }

    private Presentation ThroughOdp(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return new OdpReader(_themes, _masters).Read(stream).Value!;
    }

    private Presentation ThroughPptx(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return new PptxReader(_themes, _masters).Read(stream).Value!;
    }
}
