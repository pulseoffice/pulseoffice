using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Presentations;

public sealed record SlideGroup(string Id, IReadOnlyList<string> Members)
{
    public string? Name { get; init; }

    public double RotationDegrees { get; init; }
}

public sealed record SlideGroupLayout(IReadOnlyList<ObjectElement> Leaves, IReadOnlyList<SlideGroup> Groups);

public interface ISlideGroupDecomposition
{
    SlideGroupLayout Decompose(ObjectElement element);

    IReadOnlyList<SlideGroup> InnermostFirst(IEnumerable<SlideGroup> groups);
}

public sealed class SlideGroupDecomposition : ISlideGroupDecomposition
{
    public SlideGroupLayout Decompose(ObjectElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var leaves = new List<ObjectElement>();
        var groups = new List<SlideGroup>();
        Collect(element, leaves, groups);
        return new SlideGroupLayout(leaves, groups);
    }

    public IReadOnlyList<SlideGroup> InnermostFirst(IEnumerable<SlideGroup> groups)
    {
        var byId = new Dictionary<string, SlideGroup>(StringComparer.Ordinal);
        foreach (var group in groups) byId.TryAdd(group.Id, group);
        var ordered = new List<SlideGroup>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in byId.Values) Visit(group, byId, visited, ordered);
        return ordered;
    }

    private void Visit(SlideGroup group, Dictionary<string, SlideGroup> byId, HashSet<string> visited, List<SlideGroup> ordered)
    {
        if (!visited.Add(group.Id)) return;
        foreach (var member in group.Members)
            if (byId.TryGetValue(member, out var nested)) Visit(nested, byId, visited, ordered);
        ordered.Add(group);
    }

    private string Collect(ObjectElement element, List<ObjectElement> leaves, List<SlideGroup> groups)
    {
        if (element.Content is not ShapeGroupObject)
        {
            leaves.Add(element);
            return element.Id;
        }
        var members = DeckGrouping.Explode(element).Select(piece => Collect(piece, leaves, groups)).ToList();
        groups.Add(new SlideGroup(element.Id, members) { Name = element.Content.Name });
        return element.Id;
    }
}
