namespace PulseOffice.Presentations;

public enum AdvanceResult
{
    RevealedStep,
    ChangedSlide,
    Finished,
    None,
}

public sealed class PresentationController
{
    private readonly IReadOnlyList<Slide> _slides;
    private readonly IAnimationScheduler _scheduler;
    private readonly int[] _stepCounts;

    public PresentationController(Presentation deck, IAnimationScheduler scheduler, int startIndex = 0)
        : this(deck?.Slides ?? throw new ArgumentNullException(nameof(deck)), scheduler, startIndex)
    {
    }

    public PresentationController(IReadOnlyList<Slide> slides, IAnimationScheduler scheduler, int startIndex = 0, bool ignoreSteps = false)
    {
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentNullException.ThrowIfNull(scheduler);

        _slides = slides;
        _scheduler = scheduler;
        _stepCounts = ignoreSteps ? [.. slides.Select(_ => 0)] : [.. slides.Select(StepsOf)];

        SlideIndex = Math.Clamp(startIndex, 0, Math.Max(0, slides.Count - 1));
        StepIndex = -1;
    }

    public int SlideIndex { get; private set; }

    public int StepIndex { get; private set; }

    public Slide? Current => _slides.Count > 0 ? _slides[SlideIndex] : null;

    public int CurrentStepCount() => _slides.Count == 0 ? 0 : _stepCounts[SlideIndex];

    public bool AtStart => SlideIndex == 0 && StepIndex <= -1;

    public bool IsEmpty => _slides.Count == 0;

    public bool AtEnd => _slides.Count == 0 || (SlideIndex >= _slides.Count - 1
        && StepIndex >= _stepCounts[SlideIndex] - 1);

    public AdvanceResult Advance()
    {
        if (_slides.Count == 0) return AdvanceResult.None;

        if (StepIndex < _stepCounts[SlideIndex] - 1)
        {
            StepIndex++;
            return AdvanceResult.RevealedStep;
        }

        if (SlideIndex < _slides.Count - 1)
        {
            SlideIndex++;
            StepIndex = -1;
            return AdvanceResult.ChangedSlide;
        }

        return AdvanceResult.Finished;
    }

    public AdvanceResult Back()
    {
        if (_slides.Count == 0) return AdvanceResult.None;

        if (StepIndex > -1)
        {
            StepIndex--;
            return AdvanceResult.RevealedStep;
        }

        if (SlideIndex > 0)
        {
            SlideIndex--;
            StepIndex = _stepCounts[SlideIndex] - 1;
            return AdvanceResult.ChangedSlide;
        }

        return AdvanceResult.None;
    }

    public void GoTo(int slideIndex)
    {
        if (_slides.Count == 0) return;

        SlideIndex = Math.Clamp(slideIndex, 0, _slides.Count - 1);
        StepIndex = -1;
    }

    public (int StartMs, int EndMs)? CurrentStepWindow()
    {
        if (Current is not { } slide || StepIndex < 0) return null;

        var groups = _scheduler.ClickGroups(_scheduler.Schedule(slide.Animations));
        return StepIndex < groups.Count ? groups[StepIndex] : null;
    }

    public (int StartMs, int EndMs)? EntryWindow()
    {
        if (Current is not { } slide || StepIndex >= 0) return null;
        return _scheduler.EntryGroup(_scheduler.Schedule(slide.Animations));
    }

    public int FrozenTimeMs()
    {
        if (Current is not { } slide) return -1;

        var schedule = _scheduler.Schedule(slide.Animations);
        if (StepIndex < 0) return _scheduler.EntryGroup(schedule)?.EndMs ?? -1;

        var groups = _scheduler.ClickGroups(schedule);
        return StepIndex < groups.Count ? groups[StepIndex].EndMs : -1;
    }

    private int StepsOf(Slide slide)
    {
        var groups = _scheduler.ClickGroups(_scheduler.Schedule(slide.Animations));
        return groups.Count;
    }
}
