using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpAnimationRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Sample()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);

        slide.AddAnimation(new AnimationEffect
        {
            Id = "a1", TargetId = slide.Placeholder(PlaceholderRole.Title)!.Id,
            Category = AnimationCategory.Entrance, Preset = "fade",
            Trigger = AnimationTrigger.OnClick, DurationMs = 500,
        });
        slide.AddAnimation(new AnimationEffect
        {
            Id = "a2", TargetId = slide.Placeholder(PlaceholderRole.Body)!.Id,
            Category = AnimationCategory.Entrance, Preset = "fly-in",
            Trigger = AnimationTrigger.AfterPrevious, DurationMs = 700, DelayMs = 200, Repeat = 3,
        });

        deck.AddExisting(slide);
        return deck;
    }

    private Presentation ViaOdp(Presentation deck)
    {
        using var b = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, b).Succeeded);
        b.Position = 0;
        var r = new OdpReader(_themes, _masters).Read(b);
        Assert.True(r.Succeeded);
        return r.Value!;
    }

    [Fact]
    public void EffectsSurviveWithTargetsCategoryPresetTrigger()
    {
        var slide = ViaOdp(Sample()).Slides[0];

        Assert.Equal(2, slide.Animations.Count);
        Assert.Equal(slide.Placeholder(PlaceholderRole.Title)!.Id, slide.Animations[0].TargetId);
        Assert.Equal(slide.Placeholder(PlaceholderRole.Body)!.Id, slide.Animations[1].TargetId);

        Assert.Equal(AnimationCategory.Entrance, slide.Animations[0].Category);
        Assert.Equal("fade", slide.Animations[0].Preset);
        Assert.Equal(AnimationTrigger.OnClick, slide.Animations[0].Trigger);

        Assert.Equal("fly-in", slide.Animations[1].Preset);
        Assert.Equal(AnimationTrigger.AfterPrevious, slide.Animations[1].Trigger);
    }

    [Fact]
    public void DurationAndDelaySurvive()
    {
        var slide = ViaOdp(Sample()).Slides[0];
        Assert.Equal(500, slide.Animations[0].DurationMs);
        Assert.Equal(700, slide.Animations[1].DurationMs);
        Assert.Equal(200, slide.Animations[1].DelayMs);
    }

    [Fact]
    public void TimingAndRepeatSurvive()
    {
        var effect = ViaOdp(Sample()).Slides[0].Animations[1];

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

        var back = ViaOdp(deck).Slides[0].Animations.Single(a => a.Category == AnimationCategory.MotionPath);

        Assert.Equal("M 0 0 L 0.25 0 E", back.MotionPath);
        Assert.Equal("Media/ding.wav", back.SoundPath);
    }

    [Fact]
    public void TransitionDurationIsReadFromTheTimingNodeLikeOtherSuitesWriteIt()
    {
        var deck = Sample();
        deck.Slides[0].Transition = new SlideTransition { Kind = TransitionKind.Push, Direction = TransitionDirection.Up, DurationMs = 700 };

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        var package = buffer.ToArray();
        using var output = new MemoryStream();
        using (var source = new System.IO.Compression.ZipArchive(new MemoryStream(package)))
        using (var target = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName).Open();
                if (entry.FullName != "content.xml") { from.CopyTo(to); continue; }
                var content = System.Xml.Linq.XDocument.Load(from);
                foreach (var properties in content.Descendants(OdfPackage.Namespaces.Style + "drawing-page-properties"))
                    properties.Attribute(OdfPackage.Namespaces.Smil + "dur")?.Remove();
                content.Save(to);
            }

        output.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(output).Value!;

        Assert.Equal(700, back.Slides[0].Transition.DurationMs);
    }
}
