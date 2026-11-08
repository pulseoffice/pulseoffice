using PulseOffice.Document.Embedding;

namespace PulseOffice.Formats;

public readonly record struct AltText(string? Title, string? Description)
{
    public bool IsEmpty => Title is null && Description is null;

    public static AltText Of(EmbeddedObject content) =>
        new(content.Title is { Length: > 0 } t ? t : null,
            content.Description is { Length: > 0 } d ? d : null);

    public EmbeddedObject ApplyTo(EmbeddedObject content) =>
        IsEmpty ? content : content with
        {
            Title = Title ?? content.Title,
            Description = Description ?? content.Description,
        };
}

public abstract class AltTextMapper<TNode> where TNode : class
{
    public TElement Write<TElement>(TElement node, EmbeddedObject content) where TElement : TNode
    {
        var text = AltText.Of(content);
        if (!text.IsEmpty && Target(node) is { } target) Store(target, text);
        return node;
    }

    public EmbeddedObject Read(EmbeddedObject content, TNode node) =>
        Target(node) is { } target ? Load(target).ApplyTo(content) : content;

    public void WriteByName(TNode root, Func<string, EmbeddedObject?> lookup)
    {
        foreach (var (name, target) in Named(root).ToList())
            if (lookup(name) is { } content && AltText.Of(content) is { IsEmpty: false } text)
                Store(target, text);
    }

    public IReadOnlyDictionary<string, AltText> ReadByName(TNode root)
    {
        var result = new Dictionary<string, AltText>(StringComparer.Ordinal);
        foreach (var (name, target) in Named(root))
            if (Load(target) is { IsEmpty: false } text) result.TryAdd(name, text);
        return result;
    }

    protected abstract TNode? Target(TNode node);

    protected abstract IEnumerable<(string Name, TNode Target)> Named(TNode root);

    protected abstract void Store(TNode target, AltText text);

    protected abstract AltText Load(TNode target);
}
