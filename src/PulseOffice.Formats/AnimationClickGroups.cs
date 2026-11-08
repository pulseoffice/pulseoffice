using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed class AnimationClickGroups
{
    public IReadOnlyList<IReadOnlyList<AnimationEffect>> Of(IReadOnlyList<AnimationEffect> effects)
    {
        var groups = new List<List<AnimationEffect>>();
        foreach (var effect in effects)
        {
            if (groups.Count == 0 || effect.Trigger == AnimationTrigger.OnClick) groups.Add([]);
            groups[^1].Add(effect);
        }
        return groups;
    }
}
