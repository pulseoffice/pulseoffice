namespace PulseOffice.Documents.Objects;

public interface IFrameChains
{
    string Reference(AnchoredObject item);

    AnchoredObject? Next(TextDocument document, AnchoredObject item);

    AnchoredObject? Previous(TextDocument document, AnchoredObject item);

    AnchoredObject Head(TextDocument document, AnchoredObject item);

    IReadOnlyList<AnchoredObject> Chain(TextDocument document, AnchoredObject item);

    bool Chained(TextDocument document, AnchoredObject item);

    bool CanLink(TextDocument document, AnchoredObject from, AnchoredObject to);
}

public sealed class FrameChains : IFrameChains
{
    public string Reference(AnchoredObject item) => item.Name ?? item.Id;

    public AnchoredObject? Next(TextDocument document, AnchoredObject item) =>
        item.Content is TextBoxContent { NextFrameName: { } next }
            ? document.Objects.All.FirstOrDefault(other => other.Content is TextBoxContent && !ReferenceEquals(other, item) && Reference(other) == next)
            : null;

    public AnchoredObject? Previous(TextDocument document, AnchoredObject item)
    {
        var name = Reference(item);
        return document.Objects.All.FirstOrDefault(other => !ReferenceEquals(other, item) && other.Content is TextBoxContent { NextFrameName: { } next } && next == name);
    }

    public AnchoredObject Head(TextDocument document, AnchoredObject item)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { item.Id };
        var current = item;
        while (Previous(document, current) is { } previous && seen.Add(previous.Id)) current = previous;
        return current;
    }

    public IReadOnlyList<AnchoredObject> Chain(TextDocument document, AnchoredObject item)
    {
        var head = Head(document, item);
        var chain = new List<AnchoredObject> { head };
        var seen = new HashSet<string>(StringComparer.Ordinal) { head.Id };
        var current = head;
        while (Next(document, current) is { } next && seen.Add(next.Id))
        {
            chain.Add(next);
            current = next;
        }
        return chain;
    }

    public bool Chained(TextDocument document, AnchoredObject item) =>
        item.Content is TextBoxContent && (Next(document, item) is not null || Previous(document, item) is not null);

    public bool CanLink(TextDocument document, AnchoredObject from, AnchoredObject to) =>
        !ReferenceEquals(from, to)
        && from.Content is TextBoxContent { NextFrameName: null }
        && to.Content is TextBoxContent target
        && Previous(document, to) is null
        && target.Content.Blocks.All(block => block is Blocks.ParagraphBlock { Text.Length: 0 })
        && Chain(document, to).All(member => !ReferenceEquals(member, from));
}
