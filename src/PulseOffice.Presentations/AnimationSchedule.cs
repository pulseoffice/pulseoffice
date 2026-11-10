namespace PulseOffice.Presentations;

public readonly record struct ScheduledEffect(AnimationEffect Effect, int StartMs, int EndMs, int TriggerMs);

public readonly record struct ElementVisual(double Opacity, double OffsetX, double OffsetY, double Scale)
{
    public static readonly ElementVisual Shown = new(1, 0, 0, 1);

    public bool IsIdentity => Opacity >= 1 && OffsetX == 0 && OffsetY == 0 && Scale == 1;
}

public interface IAnimationScheduler
{
    IReadOnlyList<ScheduledEffect> Schedule(IReadOnlyList<AnimationEffect> effects);

    IReadOnlyList<ScheduledEffect> ScheduleTriggered(IReadOnlyList<AnimationEffect> effects, string triggerShapeId);

    IReadOnlyList<(int StartMs, int EndMs)> TriggerGroups(IReadOnlyList<ScheduledEffect> schedule);

    int Duration(IReadOnlyList<ScheduledEffect> schedule);

    IReadOnlyList<(int StartMs, int EndMs)> ClickGroups(IReadOnlyList<ScheduledEffect> schedule);

    (int StartMs, int EndMs)? EntryGroup(IReadOnlyList<ScheduledEffect> schedule);

    ElementVisual VisualAt(string elementId, IReadOnlyList<ScheduledEffect> schedule, int timeMs);

    bool HasEntrance(string elementId, IReadOnlyList<ScheduledEffect> schedule);
}

public sealed class AnimationScheduler : IAnimationScheduler
{
    private const int ClickGapMs = 500;

    private static readonly ElementVisual Hidden = new(0, 0, 0, 1);

    private readonly EffectMotions _motions = new();

    public IReadOnlyList<ScheduledEffect> Schedule(IReadOnlyList<AnimationEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return Plan([.. effects.Where(effect => !effect.IsTriggered)]);
    }

    public IReadOnlyList<ScheduledEffect> ScheduleTriggered(IReadOnlyList<AnimationEffect> effects, string triggerShapeId)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return Plan([.. effects.Where(effect => effect.IsTriggered && string.Equals(effect.TriggerShapeId, triggerShapeId, StringComparison.Ordinal))]);
    }

    public IReadOnlyList<(int StartMs, int EndMs)> TriggerGroups(IReadOnlyList<ScheduledEffect> schedule) => Groups(schedule);

    private List<ScheduledEffect> Plan(IReadOnlyList<AnimationEffect> effects)
    {

        var scheduled = new List<ScheduledEffect>(effects.Count);
        var cursor = 0;
        var previousStart = 0;
        var previousEnd = 0;
        var latestEnd = 0;

        foreach (var effect in effects)
        {
            var baseline = effect.Trigger switch
            {
                AnimationTrigger.WithPrevious => previousStart,
                AnimationTrigger.AfterPrevious => previousEnd,
                _ => scheduled.Count == 0 ? cursor : Saturated((long)latestEnd + ClickGapMs),
            };

            var start = Saturated((long)baseline + Math.Max(0, effect.DelayMs));
            var end = Saturated(start + (long)Math.Max(1, effect.DurationMs) * Math.Max(1, effect.Repeat));

            scheduled.Add(new ScheduledEffect(effect, start, end, baseline));

            previousStart = start;
            previousEnd = end;
            latestEnd = Math.Max(latestEnd, end);
            cursor = end;
        }

        return scheduled;
    }

    private int Saturated(long milliseconds) => (int)Math.Min(int.MaxValue, milliseconds);

    public int Duration(IReadOnlyList<ScheduledEffect> schedule) =>
        schedule.Count == 0 ? 0 : schedule.Max(s => s.EndMs);

    public IReadOnlyList<(int StartMs, int EndMs)> ClickGroups(IReadOnlyList<ScheduledEffect> schedule)
    {
        var groups = Groups(schedule);
        return StartsAutomatically(schedule) ? groups.Skip(1).ToList() : groups;
    }

    public (int StartMs, int EndMs)? EntryGroup(IReadOnlyList<ScheduledEffect> schedule) =>
        StartsAutomatically(schedule) ? Groups(schedule)[0] : null;

    private bool StartsAutomatically(IReadOnlyList<ScheduledEffect> schedule) =>
        schedule.Count > 0 && schedule[0].Effect.Trigger != AnimationTrigger.OnClick;

    private List<(int StartMs, int EndMs)> Groups(IReadOnlyList<ScheduledEffect> schedule)
    {
        var groups = new List<(int StartMs, int EndMs)>();

        foreach (var scheduled in schedule)
        {
            var onClick = scheduled.Effect.Trigger == AnimationTrigger.OnClick;

            if (groups.Count == 0 || onClick)
                groups.Add((Math.Min(scheduled.TriggerMs, scheduled.StartMs), scheduled.EndMs));
            else
            {
                var last = groups[^1];
                groups[^1] = (Math.Min(last.StartMs, scheduled.StartMs), Math.Max(last.EndMs, scheduled.EndMs));
            }
        }

        return groups;
    }

    public bool HasEntrance(string elementId, IReadOnlyList<ScheduledEffect> schedule) =>
        schedule.Any(s => s.Effect.TargetId == elementId && s.Effect.Category == AnimationCategory.Entrance);

    public ElementVisual VisualAt(string elementId, IReadOnlyList<ScheduledEffect> schedule, int timeMs)
    {
        var mine = schedule.Where(s => s.Effect.TargetId == elementId).ToList();
        if (mine.Count == 0) return ElementVisual.Shown;

        var visual = ElementVisual.Shown;
        var appeared = false;

        foreach (var scheduled in mine.OrderBy(s => s.StartMs))
        {
            if (timeMs < scheduled.StartMs)
            {
                if (scheduled.Effect.Category == AnimationCategory.Entrance && !appeared) visual = Hidden;
                continue;
            }

            appeared |= scheduled.Effect.Category is AnimationCategory.Entrance or AnimationCategory.Exit;

            if (scheduled.Effect.Rewind && timeMs >= scheduled.EndMs)
            {
                if (scheduled.Effect.Category == AnimationCategory.Entrance) visual = Hidden;
                continue;
            }

            var progress = Progress(scheduled, timeMs);
            visual = Apply(scheduled.Effect, progress, visual);
        }

        return visual;
    }

    private double Progress(ScheduledEffect scheduled, int timeMs)
    {
        if (timeMs <= scheduled.StartMs) return 0;
        if (timeMs >= scheduled.EndMs) return 1;

        var iteration = Math.Max(1, scheduled.Effect.DurationMs);
        return (double)((timeMs - scheduled.StartMs) % iteration) / iteration;
    }

    private ElementVisual Apply(AnimationEffect effect, double progress, ElementVisual current) =>
        _motions.For(effect).Visual(progress, current);
}

internal sealed class EffectMotions
{
    private readonly IReadOnlyList<EffectMotion> _motions =
    [
        new FlyMotion(AnimationCategory.Entrance, "fly-in", entering: true, dx: 0, dy: 240),
        new FlyMotion(AnimationCategory.Entrance, "fly-in-left", entering: true, dx: -320, dy: 0),
        new ZoomInMotion(),
        new FadeMotion(AnimationCategory.Entrance, entering: true),
        new FlyMotion(AnimationCategory.Exit, "fly-out", entering: false, dx: 0, dy: 240),
        new FadeMotion(AnimationCategory.Exit, entering: false),
        new GrowMotion(),
        new PulseMotion(),
        new PathMotion(),
    ];

    private readonly EffectMotion _still = new StillMotion();

    public EffectMotion For(AnimationEffect effect) =>
        _motions.FirstOrDefault(motion => motion.Category == effect.Category && motion.Preset == effect.Preset)
        ?? _motions.FirstOrDefault(motion => motion.Category == effect.Category && motion.Preset is null)
        ?? _still;
}

internal abstract class EffectMotion
{
    public abstract AnimationCategory Category { get; }

    public virtual string? Preset => null;

    public ElementVisual Visual(double progress, ElementVisual current) => Shape(progress, Ease(progress), current);

    protected abstract ElementVisual Shape(double progress, double eased, ElementVisual current);

    private double Ease(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
}

internal sealed class FadeMotion(AnimationCategory category, bool entering) : EffectMotion
{
    public override AnimationCategory Category => category;

    protected override ElementVisual Shape(double progress, double eased, ElementVisual current) =>
        new(entering ? eased : 1 - eased, 0, 0, 1);
}

internal sealed class FlyMotion(AnimationCategory category, string preset, bool entering, double dx, double dy) : EffectMotion
{
    public override AnimationCategory Category => category;

    public override string? Preset => preset;

    protected override ElementVisual Shape(double progress, double eased, ElementVisual current) =>
        entering
            ? new(Math.Min(1, eased * 1.5), (1 - eased) * dx, (1 - eased) * dy, 1)
            : new(Math.Max(0, 1 - eased * 1.5), eased * dx, eased * dy, 1);
}

internal sealed class ZoomInMotion : EffectMotion
{
    public override AnimationCategory Category => AnimationCategory.Entrance;

    public override string? Preset => "zoom";

    protected override ElementVisual Shape(double progress, double eased, ElementVisual current) => new(eased, 0, 0, 0.4 + 0.6 * eased);
}

internal sealed class GrowMotion : EffectMotion
{
    public override AnimationCategory Category => AnimationCategory.Emphasis;

    public override string? Preset => "grow";

    protected override ElementVisual Shape(double progress, double eased, ElementVisual current) =>
        current with { Scale = 1 + 0.25 * Math.Sin(progress * Math.PI) };
}

internal sealed class PulseMotion : EffectMotion
{
    public override AnimationCategory Category => AnimationCategory.Emphasis;

    protected override ElementVisual Shape(double progress, double eased, ElementVisual current) =>
        current with { Opacity = Math.Max(current.Opacity, 1 - 0.6 * Math.Sin(progress * Math.PI)) };
}

internal sealed class PathMotion : EffectMotion
{
    public override AnimationCategory Category => AnimationCategory.MotionPath;

    protected override ElementVisual Shape(double progress, double eased, ElementVisual current) => current with { OffsetX = current.OffsetX + eased * 200 };
}

internal sealed class StillMotion : EffectMotion
{
    public override AnimationCategory Category => AnimationCategory.Entrance;

    protected override ElementVisual Shape(double progress, double eased, ElementVisual current) => current;
}
