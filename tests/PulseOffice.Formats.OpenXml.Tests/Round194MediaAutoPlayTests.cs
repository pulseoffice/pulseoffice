using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round194MediaAutoPlayTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(bool autoPlay)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[0].Add(new MediaElement
        {
            Id = "song",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(3), Twip.FromCentimeters(3)),
            Kind = MediaKind.Audio,
            Data = [1, 2, 3],
            MimeType = "audio/mpeg",
            AutoPlay = autoPlay,
        });
        deck.Slides[0].AddAnimation(new AnimationEffect { Id = "fade", TargetId = "song", Preset = "fade" });
        return deck;
    }

    private Presentation ThroughPptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new PptxReader(_themes, _masters).Read(buffer).Value!;
    }

    private Presentation ThroughOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new OdpReader(_themes, _masters).Read(buffer).Value!;
    }

    private MediaElement Song(Presentation deck) => deck.Slides[0].Elements.OfType<MediaElement>().Single();

    [Fact]
    public void Automatic_start_crosses_pptx_and_odp_without_adding_an_animation()
    {
        var back = ThroughPptx(ThroughOdp(ThroughPptx(Deck(autoPlay: true))));

        Assert.True(Song(back).AutoPlay);
        var effect = Assert.Single(back.Slides[0].Animations);
        Assert.Equal(AnimationTrigger.OnClick, effect.Trigger);
        Assert.Equal(AnimationCategory.Entrance, effect.Category);
    }

    [Fact]
    public void Pptx_stores_automatic_start_as_a_leading_media_play_command()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(autoPlay: true), buffer).Succeeded);
        buffer.Position = 0;
        using var package = PresentationDocument.Open(buffer, false);
        var timing = package.PresentationPart!.SlideParts.Single().Slide!.OuterXml;

        Assert.Contains("presetClass=\"mediacall\"", timing);
        Assert.Contains("playFrom(0.0)", timing);
        Assert.True(timing.IndexOf("mediacall", StringComparison.Ordinal) < timing.IndexOf("presetClass=\"entr\"", StringComparison.Ordinal));
    }

    [Fact]
    public void A_medium_started_on_click_stays_manual()
    {
        Assert.False(Song(ThroughOdp(ThroughPptx(Deck(autoPlay: false)))).AutoPlay);
    }
}
