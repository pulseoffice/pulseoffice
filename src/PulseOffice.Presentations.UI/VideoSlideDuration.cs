namespace PulseOffice.Presentations.UI;

public interface IVideoSlideDuration
{
    int Of(Slide slide);
}

public sealed class VideoSlideDuration : IVideoSlideDuration
{
    public const int DefaultSlideMs = 3000;

    public const int LongestSlideMs = 600_000;

    public int Of(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return slide.Transition is { Advance: AdvanceMode.AfterTime, AdvanceAfterMs: > 0 } transition
            ? Math.Min(transition.AdvanceAfterMs, LongestSlideMs)
            : DefaultSlideMs;
    }
}
