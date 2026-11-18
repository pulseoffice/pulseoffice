using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round173MediaEditingTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(MediaElement media)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(media);
        deck.AddExisting(slide);
        return deck;
    }

    private MediaElement Video() => new()
    {
        Id = "v1",
        Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
        Kind = MediaKind.Video,
        Data = [0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32],
        MimeType = "video/mp4",
    };

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

    private MediaElement Only(Presentation deck) => Assert.Single(deck.Slides[0].Elements.OfType<MediaElement>());

    [Fact]
    public void Trim_and_fade_cross_both_formats_and_back()
    {
        var deck = Deck(Video() with
        {
            TrimStart = TimeSpan.FromMilliseconds(1500),
            TrimEnd = TimeSpan.FromSeconds(2),
            FadeIn = TimeSpan.FromMilliseconds(250),
            FadeOut = TimeSpan.FromSeconds(1),
        });

        foreach (var again in new[] { ThroughPptx(deck), ThroughOdp(deck), ThroughPptx(ThroughOdp(deck)), ThroughOdp(ThroughPptx(deck)) })
        {
            var media = Only(again);
            Assert.Equal(TimeSpan.FromMilliseconds(1500), media.TrimStart);
            Assert.Equal(TimeSpan.FromSeconds(2), media.TrimEnd);
            Assert.Equal(TimeSpan.FromMilliseconds(250), media.FadeIn);
            Assert.Equal(TimeSpan.FromSeconds(1), media.FadeOut);
        }
    }

    [Fact]
    public void Untrimmed_media_stays_untrimmed()
    {
        var media = Only(ThroughOdp(ThroughPptx(Deck(Video()))));

        Assert.False(media.IsTrimmed);
        Assert.False(media.IsFaded);
    }

    [Fact]
    public void Odp_keeps_a_volume_between_whole_decibels()
    {
        var media = Only(ThroughOdp(Deck(Video() with { Volume = 0.37 })));

        Assert.Equal(0.37, media.Volume, 6);
    }
}
