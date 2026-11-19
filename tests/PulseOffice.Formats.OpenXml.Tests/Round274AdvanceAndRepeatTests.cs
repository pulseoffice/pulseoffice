using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round274AdvanceAndRepeatTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        return deck;
    }

    private byte[] Pptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private string SlideXml(byte[] pptx)
    {
        using var zip = new ZipArchive(new MemoryStream(pptx));
        using var reader = new StreamReader(zip.GetEntry("ppt/slides/slide1.xml")!.Open());
        return reader.ReadToEnd();
    }

    private Presentation ViaPptx(Presentation deck) => new PptxReader(_themes, _masters).Read(new MemoryStream(Pptx(deck))).Value!;

    private Presentation ViaOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return new OdpReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;
    }

    [Fact]
    public void TriggerOnlyAdvanceIsWrittenWithoutClickOrTime()
    {
        var deck = Deck();
        deck.Slides[0].Transition = new SlideTransition { Kind = TransitionKind.Fade, Advance = AdvanceMode.TriggerOnly };

        var xml = SlideXml(Pptx(deck));

        Assert.Contains("advClick=\"0\"", xml);
        Assert.DoesNotContain("advTm", xml);
    }

    [Theory]
    [InlineData(TransitionKind.Fade)]
    [InlineData(TransitionKind.None)]
    public void TriggerOnlyAdvanceSurvivesBothFormats(TransitionKind kind)
    {
        var deck = Deck();
        deck.Slides[0].Transition = new SlideTransition { Kind = kind, Advance = AdvanceMode.TriggerOnly };

        Assert.Equal(AdvanceMode.TriggerOnly, ViaPptx(deck).Slides[0].Transition.Advance);
        Assert.Equal(AdvanceMode.TriggerOnly, ViaOdp(deck).Slides[0].Transition.Advance);
    }

    [Theory]
    [InlineData(AnimationRepeatEnd.NextClick)]
    [InlineData(AnimationRepeatEnd.EndOfSlide)]
    public void IndefiniteRepeatSurvivesBothFormats(AnimationRepeatEnd end)
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        slide.AddAnimation(new AnimationEffect
        {
            Id = "a1", TargetId = slide.InZOrder().First().Id, Category = AnimationCategory.Emphasis, Preset = "pulse", RepeatUntil = end,
        });

        Assert.Equal(end, ViaPptx(deck).Slides[0].Animations.Single().RepeatUntil);
        Assert.Equal(end, ViaOdp(deck).Slides[0].Animations.Single().RepeatUntil);
    }

    [Fact]
    public void RepeatUntilNextClickIsWrittenAsIndefiniteWithAnEndCondition()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        slide.AddAnimation(new AnimationEffect
        {
            Id = "a1", TargetId = slide.InZOrder().First().Id, Category = AnimationCategory.Emphasis, Preset = "pulse",
            RepeatUntil = AnimationRepeatEnd.NextClick,
        });

        var xml = SlideXml(Pptx(deck));

        Assert.Contains("repeatCount=\"indefinite\"", xml);
        Assert.Contains("evt=\"onNext\"", xml);
    }
}
