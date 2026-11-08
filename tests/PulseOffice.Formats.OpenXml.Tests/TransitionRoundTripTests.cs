using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class TransitionRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(SlideTransition transition)
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleSlide);
        slide.Transition = transition;
        deck.AddExisting(slide);
        return deck;
    }

    private Presentation ViaOdp(Presentation deck)
    {
        using var b = new MemoryStream();
        new OdpWriter().Write(deck, b); b.Position = 0;
        return new OdpReader(_themes, _masters).Read(b).Value!;
    }

    private Presentation ViaPptx(Presentation deck)
    {
        using var b = new MemoryStream();
        new PptxWriter().Write(deck, b); b.Position = 0;
        return new PptxReader(_themes, _masters).Read(b).Value!;
    }

    [Theory]
    [InlineData(TransitionKind.Fade)]
    [InlineData(TransitionKind.Push)]
    [InlineData(TransitionKind.Wipe)]
    [InlineData(TransitionKind.Cover)]
    [InlineData(TransitionKind.Uncover)]
    [InlineData(TransitionKind.Dissolve)]
    public void KindSurvivesBothFormats(TransitionKind kind)
    {
        var deck = Deck(new SlideTransition { Kind = kind, DurationMs = 700 });

        Assert.Equal(kind, ViaOdp(deck).Slides[0].Transition.Kind);
        Assert.Equal(kind, ViaPptx(deck).Slides[0].Transition.Kind);
    }

    [Fact]
    public void DirectionSurvivesForPush()
    {
        var deck = Deck(new SlideTransition { Kind = TransitionKind.Push, Direction = TransitionDirection.Up });

        Assert.Equal(TransitionDirection.Up, ViaOdp(deck).Slides[0].Transition.Direction);
        Assert.Equal(TransitionDirection.Up, ViaPptx(deck).Slides[0].Transition.Direction);
    }

    [Fact]
    public void AutoAdvanceSurvivesBothFormats()
    {
        var deck = Deck(new SlideTransition
        {
            Kind = TransitionKind.Fade,
            Advance = AdvanceMode.AfterTime,
            AdvanceAfterMs = 3000,
        });

        foreach (var back in new[] { ViaOdp(deck), ViaPptx(deck) })
        {
            Assert.Equal(AdvanceMode.AfterTime, back.Slides[0].Transition.Advance);
            Assert.Equal(3000, back.Slides[0].Transition.AdvanceAfterMs);
        }
    }

    [Fact]
    public void AutoAdvanceWithoutEffectSurvivesBothFormats()
    {
        var deck = Deck(SlideTransition.None with { Advance = AdvanceMode.AfterTime, AdvanceAfterMs = 5120 });

        foreach (var back in new[] { ViaOdp(deck), ViaPptx(deck), ViaPptx(ViaOdp(deck)), ViaOdp(ViaPptx(deck)) })
        {
            Assert.Equal(TransitionKind.None, back.Slides[0].Transition.Kind);
            Assert.Equal(AdvanceMode.AfterTime, back.Slides[0].Transition.Advance);
            Assert.Equal(5120, back.Slides[0].Transition.AdvanceAfterMs);
        }
    }

    [Fact]
    public void NoTransitionStaysNone()
    {
        var deck = Deck(SlideTransition.None);

        Assert.True(ViaOdp(deck).Slides[0].Transition.IsNone);
        Assert.True(ViaPptx(deck).Slides[0].Transition.IsNone);
    }
}
