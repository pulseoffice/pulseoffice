using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round137TriggerRuntimeTests
{
    private readonly IAnimationScheduler _scheduler = new AnimationScheduler();
    private readonly ISlideFactory _slides = new SlideFactory();
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);

    private (Presentation Deck, string Button, string Picture) Deck()
    {
        var deck = new Presentation { Master = _master, Theme = new ThemeCatalog().Default };
        var slide = _slides.Create(deck.NextId(), _master, SlideLayout.TitleAndContent);
        var ids = slide.InZOrder().Select(element => element.Id).ToList();
        slide.AddAnimation(new AnimationEffect { Id = "t1", TargetId = ids[1], TriggerShapeId = ids[0], DurationMs = 400 });
        deck.AddExisting(slide);
        deck.AddExisting(_slides.Create(deck.NextId(), _master, SlideLayout.TitleAndContent));
        return (deck, ids[0], ids[1]);
    }

    [Fact]
    public void TriggeredEffectsAreNotClickStepsOfTheMainSequence()
    {
        var (deck, _, _) = Deck();
        var controller = new PresentationController(deck, _scheduler);
        Assert.Equal(0, controller.CurrentStepCount());
        Assert.Equal(AdvanceResult.ChangedSlide, controller.Advance());
    }

    [Fact]
    public void ClickingTheTriggerShapePlaysItsSequence()
    {
        var (deck, button, picture) = Deck();
        var controller = new PresentationController(deck, _scheduler);
        Assert.True(controller.IsTrigger(button));
        Assert.False(controller.IsTrigger(picture));
        Assert.Equal(0, controller.VisualAt(picture, controller.FrozenTimeMs()).Opacity);
        var window = controller.Trigger(button);
        Assert.NotNull(window);
        Assert.Equal(1, controller.VisualAt(picture, controller.FrozenTimeMs()).Opacity);
        Assert.Equal(0, controller.SlideIndex);
    }

    [Fact]
    public void TriggerStateResetsWhenTheSlideChanges()
    {
        var (deck, button, picture) = Deck();
        var controller = new PresentationController(deck, _scheduler);
        controller.Trigger(button);
        controller.Advance();
        controller.Back();
        Assert.Equal(0, controller.VisualAt(picture, controller.FrozenTimeMs()).Opacity);
    }

    [Fact]
    public void WithoutAnimationsTriggersDoNothing()
    {
        var (deck, button, _) = Deck();
        var controller = new PresentationController(deck.Slides, _scheduler, ignoreSteps: true);
        Assert.Null(controller.Trigger(button));
    }

    [Fact]
    public void RewoundEntranceHidesAgainWhenDone()
    {
        var effect = new AnimationEffect { Id = "e", TargetId = "x", DurationMs = 100, Rewind = true };
        var schedule = _scheduler.Schedule([effect]);
        Assert.Equal(0, _scheduler.VisualAt("x", schedule, 200).Opacity);
        var held = _scheduler.Schedule([effect with { Rewind = false }]);
        Assert.Equal(1, _scheduler.VisualAt("x", held, 200).Opacity);
    }

    [Fact]
    public void TimelineKeepsTriggeredRowsInListOrderWithoutClickNumbers()
    {
        var effects = new[]
        {
            new AnimationEffect { Id = "a", TargetId = "x" },
            new AnimationEffect { Id = "b", TargetId = "y", TriggerShapeId = "x" },
            new AnimationEffect { Id = "c", TargetId = "z" },
        };
        var view = new AnimationTimeline(_scheduler).Build(effects);
        Assert.Equal(["a", "b", "c"], view.Rows.Select(row => row.Effect.Id));
        Assert.Equal(1, view.Rows[0].ClickNumber);
        Assert.Null(view.Rows[1].ClickNumber);
        Assert.Equal(2, view.Rows[2].ClickNumber);
    }

    [Fact]
    public void DeletingTheTriggerShapeTakesItsSequenceAlong()
    {
        var (deck, button, _) = Deck();
        var slide = deck.Slides[0];
        new ElementBehaviours(slide, [button]).Detach();
        Assert.Empty(slide.Animations);
    }
}
