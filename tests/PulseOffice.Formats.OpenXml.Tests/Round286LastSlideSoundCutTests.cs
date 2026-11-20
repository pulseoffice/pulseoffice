using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round286LastSlideSoundCutTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void PptxKeepsTheSoundCutOfTheLastSlide()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Sounds.Add(new EmbeddedSound("Media/beep.wav", "audio/wav", [1, 2, 3, 4]));
        deck.Slides[0].Transition = new SlideTransition { SoundPath = "Media/beep.wav", SoundPlaysToEnd = false };
        using var buffer = new MemoryStream();

        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        var read = new PptxReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;

        Assert.NotNull(read.Slides[0].Transition.SoundPath);
        Assert.False(read.Slides[0].Transition.SoundPlaysToEnd);
    }
}
