namespace PulseOffice.Presentations;

public sealed record AnimationTimelineRow(AnimationEffect Effect, int? ClickNumber, int StartMs, int EndMs);

public sealed record AnimationTimelineView(IReadOnlyList<AnimationTimelineRow> Rows, int TotalMs)
{
    public double StartShare(AnimationTimelineRow row) => TotalMs <= 0 ? 0 : (double)row.StartMs / TotalMs;

    public double LengthShare(AnimationTimelineRow row) => TotalMs <= 0 ? 0 : (double)(row.EndMs - row.StartMs) / TotalMs;
}

public interface IAnimationTimeline
{
    AnimationTimelineView Build(IReadOnlyList<AnimationEffect> effects);
}

public sealed class AnimationTimeline(IAnimationScheduler scheduler) : IAnimationTimeline
{
    public AnimationTimelineView Build(IReadOnlyList<AnimationEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var schedule = scheduler.Schedule(effects);
        var rows = new List<AnimationTimelineRow>(schedule.Count);
        var clicks = 0;
        foreach (var scheduled in schedule)
        {
            int? number = scheduled.Effect.Trigger == AnimationTrigger.OnClick ? ++clicks : null;
            rows.Add(new AnimationTimelineRow(scheduled.Effect, number, scheduled.StartMs, scheduled.EndMs));
        }
        return new AnimationTimelineView(rows, scheduler.Duration(schedule));
    }
}

public interface IAnimationOrdering
{
    IReadOnlyList<AnimationEffect> Move(IReadOnlyList<AnimationEffect> effects, int from, int to);

    IReadOnlyList<AnimationEffect> Replace(IReadOnlyList<AnimationEffect> effects, AnimationEffect edited);

    IReadOnlyList<AnimationEffect> Remove(IReadOnlyList<AnimationEffect> effects, string id);

    AnimationEffect Rechosen(AnimationEffect effect, AnimationCategory category, string preset);
}

public sealed class AnimationOrdering : IAnimationOrdering
{
    public AnimationEffect Rechosen(AnimationEffect effect, AnimationCategory category, string preset)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(preset);
        if (effect.Category == category && effect.Preset == preset) return effect;
        return effect with { Category = category, Preset = preset, Subtype = null, MotionPath = null };
    }

    public IReadOnlyList<AnimationEffect> Move(IReadOnlyList<AnimationEffect> effects, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (from < 0 || from >= effects.Count) return effects;
        var list = effects.ToList();
        var moved = list[from];
        list.RemoveAt(from);
        list.Insert(Math.Clamp(to, 0, list.Count), moved);
        return list;
    }

    public IReadOnlyList<AnimationEffect> Replace(IReadOnlyList<AnimationEffect> effects, AnimationEffect edited)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(edited);
        return [.. effects.Select(effect => effect.Id == edited.Id ? edited : effect)];
    }

    public IReadOnlyList<AnimationEffect> Remove(IReadOnlyList<AnimationEffect> effects, string id)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return [.. effects.Where(effect => effect.Id != id)];
    }
}
