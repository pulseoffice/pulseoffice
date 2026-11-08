using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class AnimationRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Sample()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);

        var title = slide.Placeholder(PlaceholderRole.Title)!;
        var body = slide.Placeholder(PlaceholderRole.Body)!;

        slide.AddAnimation(new AnimationEffect
        {
            Id = "a1", TargetId = title.Id, Category = AnimationCategory.Entrance,
            Preset = "fade", Trigger = AnimationTrigger.OnClick, DurationMs = 500,
        });
        slide.AddAnimation(new AnimationEffect
        {
            Id = "a2", TargetId = body.Id, Category = AnimationCategory.Entrance,
            Preset = "fly-in", Trigger = AnimationTrigger.AfterPrevious, DurationMs = 700, DelayMs = 200, Repeat = 3,
        });

        deck.AddExisting(slide);
        return deck;
    }

    private Presentation ViaPptx(Presentation deck)
    {
        using var b = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, b).Succeeded);
        b.Position = 0;
        var r = new PptxReader(_themes, _masters).Read(b);
        Assert.True(r.Succeeded);
        return r.Value!;
    }

    [Fact]
    public void EffectCountAndTargetsSurvive()
    {
        var back = ViaPptx(Sample());
        var slide = back.Slides[0];

        Assert.Equal(2, slide.Animations.Count);

        var titleId = slide.Placeholder(PlaceholderRole.Title)!.Id;
        var bodyId = slide.Placeholder(PlaceholderRole.Body)!.Id;

        Assert.Equal(titleId, slide.Animations[0].TargetId);
        Assert.Equal(bodyId, slide.Animations[1].TargetId);
    }

    [Fact]
    public void CategoryPresetTriggerSurvive()
    {
        var slide = ViaPptx(Sample()).Slides[0];

        Assert.Equal(AnimationCategory.Entrance, slide.Animations[0].Category);
        Assert.Equal("fade", slide.Animations[0].Preset);
        Assert.Equal(AnimationTrigger.OnClick, slide.Animations[0].Trigger);

        Assert.Equal("fly-in", slide.Animations[1].Preset);
        Assert.Equal(AnimationTrigger.AfterPrevious, slide.Animations[1].Trigger);
    }

    [Fact]
    public void DurationAndDelaySurvive()
    {
        var slide = ViaPptx(Sample()).Slides[0];

        Assert.Equal(500, slide.Animations[0].DurationMs);
        Assert.Equal(700, slide.Animations[1].DurationMs);
        Assert.Equal(200, slide.Animations[1].DelayMs);
    }

    [Fact]
    public void TimingAndRepeatSurvive()
    {
        var effect = ViaPptx(Sample()).Slides[0].Animations[1];

        Assert.Equal(700, effect.DurationMs);
        Assert.Equal(200, effect.DelayMs);
        Assert.Equal(3, effect.Repeat);
    }

    [Fact]
    public void MotionPathSurvives()
    {
        var deck = Sample();
        var slide = deck.Slides[0];
        var first = slide.Animations[0];
        slide.RemoveAnimation(first.Id);
        slide.InsertAnimation(0, first with { Category = AnimationCategory.MotionPath, Preset = "line", MotionPath = "M 0 0 L 0.25 0 E", SoundPath = "Media/ding.wav" });

        var back = ViaPptx(deck).Slides[0].Animations.Single(a => a.Category == AnimationCategory.MotionPath);

        Assert.Equal("M 0 0 L 0.25 0 E", back.MotionPath);
    }
}
