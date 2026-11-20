using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round283SoundCutTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        for (var i = 0; i < 2; i++) deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Sounds.Add(new EmbeddedSound("Media/beep.wav", "audio/wav", [1, 2, 3, 4]));
        deck.Slides[0].Transition = new SlideTransition { SoundPath = "Media/beep.wav", SoundPlaysToEnd = false };
        return deck;
    }

    [Fact]
    public void OdpKeepsTheSoundCutAtTheNextSlide()
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), buffer).Succeeded);
        var read = new OdpReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;

        Assert.False(read.Slides[0].Transition.SoundPlaysToEnd);
        Assert.True(read.Slides[1].Transition.SoundPlaysToEnd);
    }

    [Fact]
    public void PptxCutsTheSoundWithAStopSoundOnTheNextSlide()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), buffer).Succeeded);
        var read = new PptxReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;

        Assert.NotNull(read.Slides[0].Transition.SoundPath);
        Assert.True(read.Slides[1].Transition.StopPreviousSound);
    }
}
