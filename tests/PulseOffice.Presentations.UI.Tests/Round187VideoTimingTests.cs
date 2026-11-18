using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round187VideoTimingTests
{
    private readonly VideoSlideDuration _durations = new(new AnimationScheduler());

    [Fact]
    public void ASlideStaysOnScreenUntilItsTransitionAndAnimationsHaveFinished()
    {
        var slide = new Slide("s1", SlideLayout.Blank)
        {
            Transition = new SlideTransition
            {
                Kind = TransitionKind.Fade,
                DurationMs = 1000,
                Advance = AdvanceMode.AfterTime,
                AdvanceAfterMs = 500,
            },
        };
        slide.InsertAnimation(0, new AnimationEffect
        {
            Id = slide.NextAnimationId(),
            TargetId = "shape",
            Category = AnimationCategory.Entrance,
            Preset = "fade",
            Trigger = AnimationTrigger.AfterPrevious,
            DurationMs = 4000,
        });

        Assert.Equal(5000, _durations.Of(slide));
    }
}
