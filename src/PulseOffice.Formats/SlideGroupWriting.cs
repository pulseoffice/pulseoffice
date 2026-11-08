using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed class SlideGroupNodes<TNode> where TNode : class
{
    private readonly Dictionary<string, List<TNode>> _nodes = new(StringComparer.Ordinal);
    private readonly List<SlideGroup> _groups = [];

    public IReadOnlyList<SlideGroup> Groups => _groups;

    public void Remember(string elementId, TNode node)
    {
        if (!_nodes.TryGetValue(elementId, out var list)) _nodes[elementId] = list = [];
        if (!list.Contains(node)) list.Add(node);
    }

    public void Declare(IEnumerable<SlideGroup> groups) => _groups.AddRange(groups);

    public IReadOnlyList<TNode> Of(string elementId) => _nodes.TryGetValue(elementId, out var list) ? list : [];
}

public abstract class SlideGroupWriter<TNode>(ISlideGroupDecomposition decomposition) where TNode : class
{
    public void Wrap(SlideGroupNodes<TNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var built = new Dictionary<string, TNode>(StringComparer.Ordinal);
        foreach (var group in decomposition.InnermostFirst(nodes.Groups))
        {
            var members = group.Members
                .SelectMany(member => built.TryGetValue(member, out var nested) ? [nested] : nodes.Of(member))
                .Distinct(ReferenceEqualityComparer.Instance).Cast<TNode>()
                .ToList();
            if (members.Count == 0) continue;
            built[group.Id] = members.Count == 1 ? members[0] : Enclose(group, InDocumentOrder(members));
        }
    }

    protected abstract IReadOnlyList<TNode> InDocumentOrder(IReadOnlyList<TNode> members);

    protected abstract TNode Enclose(SlideGroup group, IReadOnlyList<TNode> members);
}
