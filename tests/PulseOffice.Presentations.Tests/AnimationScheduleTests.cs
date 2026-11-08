using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class AnimationScheduleTests
{
    private readonly IAnimationScheduler _scheduler = new AnimationScheduler();

    private static AnimationEffect Effect(string target, AnimationTrigger trigger, int dur = 500, int delay = 0,
        AnimationCategory cat = AnimationCategory.Entrance, string preset = "fade") =>
        new() { Id = target + "-a", TargetId = target, Trigger = trigger, DurationMs = dur, DelayMs = delay,
            Category = cat, Preset = preset };

    [Fact]
    public void AfterPreviousChainsSequentially()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, 400),
            Effect("b", AnimationTrigger.AfterPrevious, 300),
        ]);

        Assert.Equal(0, schedule[0].StartMs);
        Assert.Equal(400, schedule[0].EndMs);
        Assert.Equal(400, schedule[1].StartMs);
        Assert.Equal(700, schedule[1].EndMs);
    }

    [Fact]
    public void WithPreviousSharesStart()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, 400),
            Effect("b", AnimationTrigger.WithPrevious, 400),
        ]);

        Assert.Equal(schedule[0].StartMs, schedule[1].StartMs);
    }

    [Fact]
    public void DelayShiftsStart()
    {
        var schedule = _scheduler.Schedule([Effect("a", AnimationTrigger.OnClick, 400, delay: 250)]);
        Assert.Equal(250, schedule[0].StartMs);
    }

    [Fact]
    public void EntranceElementStartsInvisibleAndEndsShown()
    {
        var schedule = _scheduler.Schedule([Effect("a", AnimationTrigger.OnClick, 400)]);

        Assert.Equal(0, _scheduler.VisualAt("a", schedule, 0).Opacity, tolerance: 0.01);
        Assert.Equal(1, _scheduler.VisualAt("a", schedule, 400).Opacity, tolerance: 0.01);
    }

    [Fact]
    public void ElementWithoutAnimationIsShown()
    {
        var schedule = _scheduler.Schedule([Effect("a", AnimationTrigger.OnClick)]);
        Assert.True(_scheduler.VisualAt("b", schedule, 0).IsIdentity);
    }

    [Fact]
    public void FlyInMovesVertically()
    {
        var schedule = _scheduler.Schedule([Effect("a", AnimationTrigger.OnClick, 400, preset: "fly-in")]);
        Assert.True(_scheduler.VisualAt("a", schedule, 0).OffsetY > 100);
        Assert.Equal(0, _scheduler.VisualAt("a", schedule, 400).OffsetY, tolerance: 1);
    }

    [Fact]
    public void DurationIsLastEnd()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, 400),
            Effect("b", AnimationTrigger.AfterPrevious, 300),
        ]);
        Assert.Equal(700, _scheduler.Duration(schedule));
    }

    [Fact]
    public void ClickGroupStartsAfterTheLongestEffectOfThePreviousGroup()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, 2000),
            Effect("b", AnimationTrigger.WithPrevious, 300),
            Effect("c", AnimationTrigger.OnClick, 300),
        ]);

        Assert.True(schedule[2].StartMs >= 2000);
    }

    [Fact]
    public void RepeatLengthensTheEffect()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, 400) with { Repeat = 3 },
            Effect("b", AnimationTrigger.AfterPrevious, 300),
        ]);

        Assert.Equal(1200, schedule[0].EndMs);
        Assert.Equal(1200, schedule[1].StartMs);
    }

    [Fact]
    public void ElementThatLeavesAndReentersIsVisibleBeforeItsExit()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, 500),
            Effect("a", AnimationTrigger.OnClick, 500, cat: AnimationCategory.Exit),
            Effect("a", AnimationTrigger.OnClick, 500),
        ]);

        Assert.Equal(1, _scheduler.VisualAt("a", schedule, schedule[0].EndMs + 10).Opacity);
    }

    [Fact]
    public void TheDelayOfAClickEffectIsWaitedAfterTheClick()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, 400),
            Effect("b", AnimationTrigger.OnClick, 300, delay: 2000),
        ]);

        var groups = _scheduler.ClickGroups(schedule);

        Assert.Equal(schedule[1].StartMs - 2000, groups[1].StartMs);
        Assert.Equal(0, _scheduler.VisualAt("b", schedule, groups[1].StartMs + 1000).Opacity);
    }

    [Fact]
    public void TheDelayOfAnAutomaticFirstEffectIsWaitedWhenTheSlideAppears()
    {
        var schedule = _scheduler.Schedule([Effect("a", AnimationTrigger.AfterPrevious, 400, delay: 1500)]);

        Assert.Equal((0, 1900), _scheduler.EntryGroup(schedule));
    }
}
