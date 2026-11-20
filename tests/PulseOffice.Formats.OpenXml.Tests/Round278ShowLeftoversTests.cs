using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round278ShowLeftoversTests
{
    private const string P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        for (var i = 0; i < 2; i++) deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
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

    [Fact]
    public void KioskRestartAndLoopPauseSurviveBothFormats()
    {
        var deck = Deck();
        deck.Setup = new ShowSetup { Kind = ShowKind.Kiosk, KioskRestartMs = 45000, LoopPauseMs = 3000 };

        foreach (var read in new[] { ViaPptx(deck), ViaOdp(deck) })
        {
            Assert.Equal(45000, read.Setup.KioskRestartMs);
            Assert.Equal(3000, read.Setup.LoopPauseMs);
        }
    }

    [Fact]
    public void DefaultsStayDefaultsInBothFormats()
    {
        var deck = Deck();
        deck.Setup = new ShowSetup { Kind = ShowKind.Kiosk };

        foreach (var read in new[] { ViaPptx(deck), ViaOdp(deck) })
        {
            Assert.Equal(ShowSetup.DefaultKioskRestartMs, read.Setup.KioskRestartMs);
            Assert.Equal(0, read.Setup.LoopPauseMs);
        }
    }

    [Fact]
    public void StopPreviousSoundSurvivesBothFormats()
    {
        var deck = Deck();
        deck.Slides[1].Transition = new SlideTransition { StopPreviousSound = true };

        foreach (var read in new[] { ViaPptx(deck), ViaOdp(deck) })
            Assert.True(read.Slides[1].Transition.StopPreviousSound);
    }

    [Fact]
    public void PlayFullFalseIsNotReadAsLoopingTheSound()
    {
        var deck = Deck();
        deck.Sounds.Add(new EmbeddedSound("Media/beep.wav", "audio/wav", [1, 2, 3, 4]));
        deck.Slides[1].Transition = new SlideTransition { SoundPath = "Media/beep.wav", LoopSoundUntilNext = true };
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        var bytes = buffer.ToArray();

        Assert.True(new OdpReader(_themes, _masters).Read(new MemoryStream(bytes)).Value!.Slides[1].Transition.LoopSoundUntilNext);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());
        var content = reader.ReadToEnd();
        Assert.DoesNotContain("play-full=\"false\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BackwardsIterationSurvivesBothCodecs()
    {
        var behaviours = new AnimationBehaviours { IterateType = "by-word", IterateInterval = 0.1, IterateBackwards = true };

        var iterate = new PptxAnimationBehaviourCodec().Iterate(behaviours)!;
        var head = XElement.Parse($"<p:cTn xmlns:p=\"{P}\" id=\"5\"/>");
        head.Add(iterate);
        Assert.True(new PptxAnimationBehaviourCodec().Read(head).IterateBackwards);

        var odf = new OdfAnimationBehaviourCodec();
        Assert.True(odf.Read(odf.Write(behaviours, "shape1")).IterateBackwards);
        Assert.False(odf.Read(odf.Write(behaviours with { IterateBackwards = false }, "shape1")).IterateBackwards);
    }
}
