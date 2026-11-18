namespace PulseOffice.Presentations.UI;

public sealed record SlideVideoShot(int SlideIndex, int DurationMs, int AnimationTimeMs, double? TransitionProgress);

public interface ISlideVideoTimeline
{
    IReadOnlyList<SlideVideoShot> Shots(IReadOnlyList<Slide> slides, Func<Slide, int> durationOf);

    int AnimatedMs(Slide slide);
}

public sealed class SlideVideoTimeline(IAnimationScheduler scheduler) : ISlideVideoTimeline
{
    public const int FrameMs = 66;

    public const int MostShotsPerPhase = 300;

    public const int ShortestTransitionMs = 120;

    public int AnimatedMs(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return slide.Animations.Count == 0 ? 0 : Math.Max(0, scheduler.Duration(scheduler.Schedule(slide.Animations)));
    }

    public IReadOnlyList<SlideVideoShot> Shots(IReadOnlyList<Slide> slides, Func<Slide, int> durationOf)
    {
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentNullException.ThrowIfNull(durationOf);
        var shots = new List<SlideVideoShot>();
        for (var index = 0; index < slides.Count; index++)
        {
            var slide = slides[index];
            var used = 0;
            if (index > 0 && !slide.Transition.IsNone)
            {
                var span = Math.Max(ShortestTransitionMs, slide.Transition.DurationMs);
                foreach (var (offset, length) in Steps(span))
                    shots.Add(new SlideVideoShot(index, length, -1, offset / (double)span));
                used += span;
            }

            var animated = AnimatedMs(slide);
            if (animated > 0)
            {
                foreach (var (offset, length) in Steps(animated))
                    shots.Add(new SlideVideoShot(index, length, offset, null));
                used += animated;
            }

            shots.Add(new SlideVideoShot(index, Math.Max(FrameMs, durationOf(slide) - used), animated, null));
        }

        return shots;
    }

    private static IEnumerable<(int Offset, int Length)> Steps(int span)
    {
        var step = Math.Max(FrameMs, (span + MostShotsPerPhase - 1) / MostShotsPerPhase);
        for (var offset = 0; offset < span; offset += step)
            yield return (offset, Math.Min(step, span - offset));
    }
}
