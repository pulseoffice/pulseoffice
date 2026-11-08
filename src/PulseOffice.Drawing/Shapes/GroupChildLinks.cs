namespace PulseOffice.Drawing.Shapes;

public sealed record GroupMemberPath(IReadOnlyList<int> Indices)
{
    public bool SameAs(GroupMemberPath other) => other is not null && Indices.SequenceEqual(other.Indices);
}

public interface IGroupChildLinks
{
    GroupMemberPath? MemberAt(ShapeGroupObject group, double x, double y);

    string? Link(ShapeGroupObject group, GroupMemberPath path);

    string? EffectiveLink(ShapeGroupObject group, GroupMemberPath path);

    ShapeGroupObject WithLink(ShapeGroupObject group, GroupMemberPath path, string? link);
}

public sealed class GroupChildLinks : IGroupChildLinks
{
    private const int DeepestNesting = 64;

    public GroupMemberPath? MemberAt(ShapeGroupObject group, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!double.IsFinite(x) || !double.IsFinite(y)) return null;
        var indices = new List<int>();
        var current = group;
        while (indices.Count < DeepestNesting)
        {
            var found = -1;
            for (var index = current.Children.Count - 1; index >= 0; index--)
            {
                var child = current.Children[index];
                if (child.Width <= 0 || child.Height <= 0) continue;
                if (x < child.X || y < child.Y || x > child.X + child.Width || y > child.Y + child.Height) continue;
                found = index;
                break;
            }
            if (found < 0) break;
            indices.Add(found);
            var hit = current.Children[found];
            if (hit.Content is not ShapeGroupObject nested) break;
            x = (x - hit.X) / hit.Width;
            y = (y - hit.Y) / hit.Height;
            current = nested;
        }
        return indices.Count == 0 ? null : new GroupMemberPath(indices);
    }

    public string? Link(ShapeGroupObject group, GroupMemberPath path) => Chain(group, path) is { Count: > 0 } chain ? chain[^1].Hyperlink : null;

    public string? EffectiveLink(ShapeGroupObject group, GroupMemberPath path)
    {
        var chain = Chain(group, path);
        for (var index = chain.Count - 1; index >= 0; index--)
        {
            if (chain[index].Hyperlink is { Length: > 0 } link) return link;
        }
        return null;
    }

    public ShapeGroupObject WithLink(ShapeGroupObject group, GroupMemberPath path, string? link)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(path);
        return Rewrite(group, path.Indices, 0, link is { Length: > 0 } ? link : null) ?? group;
    }

    private ShapeGroupObject? Rewrite(ShapeGroupObject group, IReadOnlyList<int> indices, int depth, string? link)
    {
        if (depth >= indices.Count || indices[depth] < 0 || indices[depth] >= group.Children.Count) return null;
        var child = group.Children[indices[depth]];
        GroupChild changed;
        if (depth == indices.Count - 1) changed = child with { Hyperlink = link };
        else if (child.Content is ShapeGroupObject nested && Rewrite(nested, indices, depth + 1, link) is { } rewritten) changed = child with { Content = rewritten };
        else return null;
        return group with { Children = [.. group.Children.Select((existing, index) => index == indices[depth] ? changed : existing)] };
    }

    private List<GroupChild> Chain(ShapeGroupObject group, GroupMemberPath path)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(path);
        var chain = new List<GroupChild>();
        var current = group;
        foreach (var index in path.Indices)
        {
            if (current is null || index < 0 || index >= current.Children.Count) return [];
            var child = current.Children[index];
            chain.Add(child);
            current = child.Content as ShapeGroupObject;
        }
        return chain;
    }
}
