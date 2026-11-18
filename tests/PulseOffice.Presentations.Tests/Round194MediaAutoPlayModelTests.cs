using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round194MediaAutoPlayModelTests
{
    private readonly MediaAutoPlay _autoPlay = new();

    private MediaElement Media(string id, bool autoPlay, bool hidden = false) => new()
    {
        Id = id,
        Bounds = new DocumentRect(Twip.Zero, Twip.Zero, Twip.FromCentimeters(1), Twip.FromCentimeters(1)),
        Kind = MediaKind.Video,
        Data = [0],
        MimeType = "video/mp4",
        AutoPlay = autoPlay,
        Hidden = hidden,
    };

    [Fact]
    public void Automatic_media_lead_the_written_sequence_and_are_absorbed_back()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(Media("a", true));
        slide.Add(Media("b", true));
        slide.AddAnimation(new AnimationEffect { Id = "x", TargetId = "a" });

        var written = _autoPlay.Animations(slide);
        Assert.Equal([AnimationTrigger.AfterPrevious, AnimationTrigger.WithPrevious, AnimationTrigger.OnClick], written.Select(effect => effect.Trigger));

        var read = new Slide("s", SlideLayout.Blank);
        read.Add(Media("a", false));
        read.Add(Media("b", false));
        foreach (var effect in written) read.AddAnimation(effect);
        _autoPlay.Absorb(read);

        Assert.All(read.Elements.OfType<MediaElement>(), media => Assert.True(media.AutoPlay));
        Assert.Equal(["x"], read.Animations.Select(effect => effect.Id));
    }

    [Fact]
    public void A_play_after_a_click_is_not_an_automatic_start()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(Media("a", false));
        slide.AddAnimation(new AnimationEffect { Id = "x", TargetId = "a" });
        slide.AddAnimation(new AnimationEffect { Id = "p", TargetId = "a", Category = AnimationCategory.Media, Preset = "play", Trigger = AnimationTrigger.AfterPrevious });

        _autoPlay.Absorb(slide);

        Assert.False(Assert.IsType<MediaElement>(slide.Find("a")).AutoPlay);
        Assert.Equal(2, slide.Animations.Count);
    }

    [Fact]
    public void The_show_starts_only_visible_automatic_media()
    {
        var slide = new Slide("s", SlideLayout.Blank);
        slide.Add(Media("a", true));
        slide.Add(Media("b", true, hidden: true));
        slide.Add(Media("c", false));

        Assert.Equal(["a"], _autoPlay.Starting(slide).Select(media => media.Id));
    }
}
