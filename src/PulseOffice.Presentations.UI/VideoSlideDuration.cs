namespace PulseOffice.Presentations.UI;

public interface IVideoSlideDuration
{
    int Of(Slide slide);
}

public sealed class VideoSlideDuration(IAnimationScheduler scheduler) : IVideoSlideDuration
{
    public const int DefaultSlideMs = 3000;

    public const int LongestSlideMs = 600_000;

    public int Of(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var shown = slide.Transition is { AdvancesAfterTime: true, AdvanceAfterMs: > 0 } transition
            ? transition.AdvanceAfterMs
            : DefaultSlideMs;
        var entering = slide.Transition.IsNone ? 0 : Math.Max(0, slide.Transition.DurationMs);
        var animated = scheduler.Duration(scheduler.Schedule(slide.Animations));
        return (int)Math.Min((long)Math.Max(shown, (long)entering + animated), LongestSlideMs);
    }
}
