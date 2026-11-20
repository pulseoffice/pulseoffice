using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round278ShowLeftoversTests
{
    private readonly AnimationScheduler _scheduler = new();

    private AnimationEffect Pulse(string id, AnimationRepeatEnd until, AnimationTrigger trigger = AnimationTrigger.OnClick) => new()
    {
        Id = id,
        TargetId = "shape",
        Category = AnimationCategory.Emphasis,
        Trigger = trigger,
        DurationMs = 1000,
        RepeatUntil = until,
    };

    [Fact]
    public void RepeatUntilEndOfSlideKeepsLoopingAfterItsFirstCycle()
    {
        var schedule = _scheduler.Schedule([Pulse("a", AnimationRepeatEnd.EndOfSlide)]);
        var end = schedule[0].EndMs;

        Assert.True(_scheduler.LoopsAt(schedule, end));
        Assert.Equal(_scheduler.VisualAt("shape", schedule, end - 500), _scheduler.VisualAt("shape", schedule, end, loopElapsedMs: 500));
        Assert.NotEqual(_scheduler.VisualAt("shape", schedule, end), _scheduler.VisualAt("shape", schedule, end, loopElapsedMs: 500));
    }

    [Fact]
    public void RepeatUntilNextClickStopsAtTheNextClick()
    {
        var schedule = _scheduler.Schedule([Pulse("a", AnimationRepeatEnd.NextClick), Pulse("b", AnimationRepeatEnd.Count) with { TargetId = "other" }]);

        Assert.True(_scheduler.LoopsAt(schedule, schedule[0].EndMs));
        Assert.False(_scheduler.LoopsAt(schedule, schedule[1].TriggerMs));
    }

    [Fact]
    public void CountedRepeatsDoNotLoop()
    {
        var schedule = _scheduler.Schedule([Pulse("a", AnimationRepeatEnd.Count)]);

        Assert.False(_scheduler.LoopsAt(schedule, schedule[0].EndMs));
    }

    private Presentation Deck(int count)
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var deck = new Presentation { Master = master, Theme = new ThemeCatalog().Default };
        for (var i = 0; i < count; i++) deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));
        return deck;
    }

    [Fact]
    public void FromCurrentOnAHiddenSlideShowsIt()
    {
        var deck = Deck(3);
        deck.Slides[1].Hidden = true;
        var playlists = new ShowPlaylist();

        var start = playlists.FromCurrent(deck, playlists.Main(deck), 1, new ShowStart());

        Assert.Same(deck.Slides[1], start.Playlist[start.Position]);
        Assert.Equal(3, start.Playlist.Count);
    }

    [Fact]
    public void FromTheBeginningStillSkipsAHiddenFirstSlide()
    {
        var deck = Deck(2);
        deck.Slides[0].Hidden = true;
        var playlists = new ShowPlaylist();

        var start = playlists.FromCurrent(deck, playlists.Main(deck), -1, new ShowStart());

        Assert.Single(start.Playlist);
        Assert.Same(deck.Slides[1], start.Playlist[start.Position]);
    }

    [Fact]
    public void EffectSoundsOfAStepAreListed()
    {
        var deck = Deck(1);
        deck.Slides[0].AddAnimation(Pulse("a", AnimationRepeatEnd.Count) with { SoundPath = "Media/ding.wav" });
        var controller = new PresentationController(deck, _scheduler);

        Assert.Equal(["Media/ding.wav"], controller.SoundsIn((0, 2000)));
    }
}
