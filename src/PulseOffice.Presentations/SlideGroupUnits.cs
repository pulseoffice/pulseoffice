namespace PulseOffice.Presentations;

public interface ISlideGroupUnits
{
    SlideGroup? UnitGroup(Slide slide, string elementId, string? openGroupId);

    IReadOnlyList<SlideElement> Unit(Slide slide, SlideElement picked, string? openGroupId);

    IReadOnlyList<string> Leaves(Slide slide, SlideGroup group);

    SlideGroup? Matching(Slide slide, IReadOnlyList<SlideElement> selection);
}

public sealed class SlideGroupUnits : ISlideGroupUnits
{
    public SlideGroup? UnitGroup(Slide slide, string elementId, string? openGroupId)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var parents = Parents(slide);
        var chain = new List<SlideGroup>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { elementId };
        var current = elementId;
        while (parents.TryGetValue(current, out var parent) && visited.Add(parent.Id))
        {
            chain.Add(parent);
            current = parent.Id;
        }
        if (chain.Count == 0) return null;
        if (openGroupId is null) return chain[^1];
        var open = chain.FindIndex(group => string.Equals(group.Id, openGroupId, StringComparison.Ordinal));
        if (open < 0) return chain[^1];
        return open == 0 ? null : chain[open - 1];
    }

    public IReadOnlyList<SlideElement> Unit(Slide slide, SlideElement picked, string? openGroupId)
    {
        ArgumentNullException.ThrowIfNull(picked);
        if (UnitGroup(slide, picked.Id, openGroupId) is not { } group) return [picked];
        var members = Leaves(slide, group).Select(slide.Find).OfType<SlideElement>().ToList();
        return members.Count > 1 && members.Any(member => member.Id == picked.Id) ? members : [picked];
    }

    public IReadOnlyList<string> Leaves(Slide slide, SlideGroup group)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(group);
        var byId = slide.Groups.GroupBy(candidate => candidate.Id, StringComparer.Ordinal).ToDictionary(set => set.Key, set => set.First(), StringComparer.Ordinal);
        var leaves = new List<string>();
        Collect(group, byId, new HashSet<string>(StringComparer.Ordinal), leaves);
        return leaves;
    }

    public SlideGroup? Matching(Slide slide, IReadOnlyList<SlideElement> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Count < 2) return null;
        var ids = selection.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        return slide.Groups.FirstOrDefault(group =>
        {
            var leaves = Leaves(slide, group).Where(id => slide.Find(id) is not null).ToHashSet(StringComparer.Ordinal);
            return leaves.SetEquals(ids);
        });
    }

    private void Collect(SlideGroup group, Dictionary<string, SlideGroup> byId, HashSet<string> visited, List<string> leaves)
    {
        if (!visited.Add(group.Id)) return;
        foreach (var member in group.Members)
        {
            if (byId.TryGetValue(member, out var nested)) Collect(nested, byId, visited, leaves);
            else leaves.Add(member);
        }
    }

    private Dictionary<string, SlideGroup> Parents(Slide slide)
    {
        var parents = new Dictionary<string, SlideGroup>(StringComparer.Ordinal);
        foreach (var group in slide.Groups)
            foreach (var member in group.Members)
                parents.TryAdd(member, group);
        return parents;
    }
}
