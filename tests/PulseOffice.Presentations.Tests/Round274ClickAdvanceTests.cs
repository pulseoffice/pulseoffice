using PulseOffice.Text.Rich;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round274ClickAdvanceTests
{
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);

    private PresentationController Show(AdvanceMode advance, bool animated = false)
    {
        var deck = new Presentation { Master = _master, Theme = new ThemeCatalog().Default };
        var slide = new SlideFactory().Create(deck.NextId(), _master, SlideLayout.TitleAndContent);
        slide.Transition = new SlideTransition { Advance = advance, AdvanceAfterMs = 1000 };
        if (animated)
            slide.AddAnimation(new AnimationEffect { Id = "a", TargetId = slide.Placeholder(PlaceholderRole.Title)!.Id });
        deck.AddExisting(slide);
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), _master, SlideLayout.TitleAndContent));
        return new PresentationController(deck, new AnimationScheduler());
    }

    [Theory]
    [InlineData(AdvanceMode.OnClick, true)]
    [InlineData(AdvanceMode.OnClickOrAfterTime, true)]
    [InlineData(AdvanceMode.AfterTime, false)]
    [InlineData(AdvanceMode.TriggerOnly, false)]
    public void ClickChangesTheSlideOnlyWhenTheTransitionAllowsIt(AdvanceMode advance, bool expected) =>
        Assert.Equal(expected, Show(advance).ClickAdvances(manualShow: false));

    [Fact]
    public void ClickStillRevealsPendingAnimationSteps() =>
        Assert.True(Show(AdvanceMode.TriggerOnly, animated: true).ClickAdvances(manualShow: false));

    [Fact]
    public void ManualShowIgnoresTheTransitionAdvance() =>
        Assert.True(Show(AdvanceMode.AfterTime).ClickAdvances(manualShow: true));

    [Theory]
    [InlineData(AdvanceMode.OnClick, AdvanceMode.OnClickOrAfterTime)]
    [InlineData(AdvanceMode.TriggerOnly, AdvanceMode.AfterTime)]
    [InlineData(AdvanceMode.AfterTime, AdvanceMode.AfterTime)]
    public void RehearsalKeepsTheClickChoice(AdvanceMode before, AdvanceMode after)
    {
        var rehearsed = new SlideTransition { Advance = before }.WithRehearsedTime(4200);

        Assert.Equal(after, rehearsed.Advance);
        Assert.Equal(4200, rehearsed.AdvanceAfterMs);
    }
}
