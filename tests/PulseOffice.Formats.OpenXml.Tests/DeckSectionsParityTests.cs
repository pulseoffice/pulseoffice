using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckSectionsParityTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void Sections_cross_odp_and_then_pptx_without_loss()
    {
        var deck = Deck();
        deck.Slides[0].SectionName = "Intro";
        deck.Slides[2].SectionName = "Résumé — fin";

        var odp = ThroughOdp(deck);
        Assert.Equal(["Intro", null, "Résumé — fin"], odp.Slides.Select(slide => slide.SectionName));

        var pptx = ThroughPptx(odp);
        Assert.Equal(["Intro", null, "Résumé — fin"], pptx.Slides.Select(slide => slide.SectionName));
    }

    [Fact]
    public void Odp_with_sections_declares_no_section_loss()
    {
        var deck = Deck();
        deck.Slides[1].SectionName = "Body";
        using var stream = new MemoryStream();
        var result = new OdpWriter().Write(deck, stream);
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ODP011");
    }

    private Presentation Deck()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        for (var i = 0; i < 3; i++) deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        return deck;
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
