using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed class AnimationSteps
{
    public IReadOnlyList<IReadOnlyList<AnimationEffect>> Of(IReadOnlyList<AnimationEffect> group)
    {
        var steps = new List<List<AnimationEffect>>();
        foreach (var effect in group)
        {
            if (steps.Count == 0 || effect.Trigger == AnimationTrigger.AfterPrevious) steps.Add([]);
            steps[^1].Add(effect);
        }
        return steps;
    }

    public long Length(IReadOnlyList<AnimationEffect> step) =>
        step.Count == 0 ? 0 : step.Max(effect => effect.DelayMs + (long)effect.DurationMs * Math.Max(1, effect.Repeat));
}
