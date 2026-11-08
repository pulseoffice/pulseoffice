namespace PulseOffice.Documents.Styles;

public interface IStyleLineage
{
    IReadOnlyList<string> PossibleParents<TStyle>(StyleCollection<TStyle> styles, string name) where TStyle : DocumentStyle;
}

public sealed class StyleLineage : IStyleLineage
{
    public IReadOnlyList<string> PossibleParents<TStyle>(StyleCollection<TStyle> styles, string name) where TStyle : DocumentStyle
    {
        var children = styles.All.Where(style => style.ParentName is not null).ToLookup(style => style.ParentName!, StringComparer.Ordinal);
        var below = new HashSet<string>(StringComparer.Ordinal) { name };
        var pending = new Queue<string>([name]);
        while (pending.TryDequeue(out var current))
            foreach (var child in children[current])
                if (below.Add(child.Name)) pending.Enqueue(child.Name);
        return [.. styles.All.Where(style => !below.Contains(style.Name)).Select(style => style.Name)];
    }
}
