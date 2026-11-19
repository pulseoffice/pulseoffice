using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round274ShowSetupTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(ShowSetup setup)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        for (var i = 0; i < 3; i++) deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        deck.Slides[1].Name = "Middle";
        deck.AddCustomShow(new CustomShow("Short", [deck.Slides[0].Id, deck.Slides[2].Id]));
        deck.Setup = setup;
        return deck;
    }

    private Presentation ViaPptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return new PptxReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;
    }

    private Presentation ViaOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return new OdpReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;
    }

    [Theory]
    [InlineData(ShowKind.Kiosk)]
    [InlineData(ShowKind.Window)]
    public void ShowKindAndCustomShowSurviveBothFormats(ShowKind kind)
    {
        var deck = Deck(new ShowSetup { Kind = kind, CustomShowName = "Short" });

        foreach (var read in new[] { ViaPptx(deck), ViaOdp(deck) })
        {
            Assert.Equal(kind, read.Setup.Kind);
            Assert.Equal("Short", read.Setup.CustomShowName);
            Assert.False(read.Setup.Loop);
            Assert.True(read.Setup.Loops == (kind == ShowKind.Kiosk));
        }
    }

    [Fact]
    public void RangeEndAndPenColourSurviveOdp()
    {
        var read = ViaOdp(Deck(new ShowSetup { FromSlide = 2, ToSlide = 3, PenColor = "#2563EB" })).Setup;

        Assert.Equal(2, read.FromSlide);
        Assert.Equal(3, read.ToSlide);
        Assert.Equal("#2563EB", read.PenColor);
    }

    [Fact]
    public void CustomShowSetupChoosesThePlaylist()
    {
        var deck = Deck(new ShowSetup { CustomShowName = "Short" });

        Assert.Equal([deck.Slides[0], deck.Slides[2]], new ShowPlaylist().Main(deck));
    }
}
