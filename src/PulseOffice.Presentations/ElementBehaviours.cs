namespace PulseOffice.Presentations;

public sealed class ElementBehaviours
{
    private readonly Slide _slide;
    private readonly List<(int Index, AnimationEffect Effect)> _animations;
    private readonly List<InteractiveAction> _interactions;
    private readonly List<SlideGroup> _groupsBefore;
    private readonly List<SlideGroup> _groupsAfter;

    public ElementBehaviours(Slide slide, IEnumerable<string> targets)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var ids = targets.ToHashSet(StringComparer.Ordinal);
        _slide = slide;
        _animations = [.. slide.Animations.Select((effect, index) => (index, effect)).Where(pair => ids.Contains(pair.effect.TargetId) || (pair.effect.TriggerShapeId is { } trigger && ids.Contains(trigger)))];
        _interactions = [.. slide.Interactions.Where(action => ids.Contains(action.TargetId))];
        _groupsBefore = [.. slide.Groups];
        _groupsAfter = Pruned(_groupsBefore, ids);
    }

    public void Detach()
    {
        foreach (var (_, effect) in _animations) _slide.RemoveAnimation(effect.Id);
        foreach (var action in _interactions) _slide.RemoveInteraction(action.TargetId);
        if (!_groupsAfter.SequenceEqual(_groupsBefore)) _slide.ReplaceGroups(_groupsAfter);
    }

    public void Attach()
    {
        foreach (var (index, effect) in _animations)
            if (_slide.Animations.All(existing => existing.Id != effect.Id)) _slide.InsertAnimation(index, effect);
        foreach (var action in _interactions) _slide.SetInteraction(action);
        if (!_groupsAfter.SequenceEqual(_groupsBefore)) _slide.ReplaceGroups(_groupsBefore);
    }

    private static List<SlideGroup> Pruned(List<SlideGroup> groups, HashSet<string> removed)
    {
        var gone = new HashSet<string>(removed, StringComparer.Ordinal);
        var current = groups;
        while (true)
        {
            var next = current
                .Where(group => !gone.Contains(group.Id))
                .Select(group => group.Members.Any(gone.Contains) ? group with { Members = [.. group.Members.Where(member => !gone.Contains(member))] } : group)
                .ToList();
            var emptied = next.Where(group => group.Members.Count == 0).Select(group => group.Id).ToList();
            if (emptied.Count == 0) return next;
            gone.UnionWith(emptied);
            current = next;
        }
    }
}
