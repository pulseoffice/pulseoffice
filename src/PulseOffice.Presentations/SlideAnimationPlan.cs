namespace PulseOffice.Presentations;

public sealed class SlideAnimationPlan
{
    private readonly IAnimationScheduler _scheduler;
    private readonly Dictionary<string, IReadOnlyList<ScheduledEffect>> _triggered = new(StringComparer.Ordinal);

    public SlideAnimationPlan(Slide slide, IAnimationScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
        Slide = slide;
        Count = slide.Animations.Count;
        Main = scheduler.Schedule(slide.Animations);
        foreach (var trigger in slide.Animations.Where(effect => effect.IsTriggered).Select(effect => effect.TriggerShapeId!).Distinct(StringComparer.Ordinal))
            _triggered[trigger] = scheduler.ScheduleTriggered(slide.Animations, trigger);
    }

    public Slide Slide { get; }

    public int Count { get; }

    public IReadOnlyList<ScheduledEffect> Main { get; }

    public IReadOnlyCollection<string> Triggers => _triggered.Keys;

    public bool NeedsVisuals => _triggered.Count > 0 || Main.Any(scheduled => scheduled.Effect.Category == AnimationCategory.Entrance);

    public IReadOnlyList<(int StartMs, int EndMs)> TriggerGroups(string triggerShapeId) =>
        _triggered.TryGetValue(triggerShapeId, out var schedule) ? _scheduler.TriggerGroups(schedule) : [];

    public ElementVisual VisualAt(string elementId, int mainTimeMs, Func<string, int> triggerTimeMs)
    {
        ArgumentNullException.ThrowIfNull(triggerTimeMs);
        var inMain = Main.Any(scheduled => scheduled.Effect.TargetId == elementId);
        foreach (var (trigger, schedule) in _triggered)
        {
            if (schedule.All(scheduled => scheduled.Effect.TargetId != elementId)) continue;
            var time = triggerTimeMs(trigger);
            if (time >= 0 || !inMain) return _scheduler.VisualAt(elementId, schedule, time);
        }
        return _scheduler.VisualAt(elementId, Main, mainTimeMs);
    }
}
