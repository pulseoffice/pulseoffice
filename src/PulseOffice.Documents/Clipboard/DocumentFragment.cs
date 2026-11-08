using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Clipboard;

public sealed class DocumentFragment(
    BlockContainer blocks,
    StyleSheet styles,
    IReadOnlyList<Note> notes,
    IReadOnlyList<Comment> comments,
    IReadOnlyList<AnchoredObject> objects,
    IReadOnlyList<TrackedChange>? changes = null)
{
    public BlockContainer Blocks { get; } = blocks;

    public StyleSheet Styles { get; } = styles;

    public IReadOnlyList<Note> Notes { get; } = notes;

    public IReadOnlyList<Comment> Comments { get; } = comments;

    public IReadOnlyList<AnchoredObject> Objects { get; } = objects;

    public IReadOnlyList<TrackedChange> Changes { get; } = changes ?? [];

    public bool IsEmpty => Blocks.Count == 0;
}

public sealed class FragmentReferences
{
    public HashSet<string> ParagraphStyles { get; } = new(StringComparer.Ordinal);

    public HashSet<string> CharacterStyles { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ListStyles { get; } = new(StringComparer.Ordinal);

    public HashSet<string> PageStyles { get; } = new(StringComparer.Ordinal);

    public HashSet<string> FrameStyles { get; } = new(StringComparer.Ordinal);

    public HashSet<string> TableStyles { get; } = new(StringComparer.Ordinal);

    public List<string> Notes { get; } = [];

    public List<string> Comments { get; } = [];

    public List<string> Objects { get; } = [];

    public List<string> Changes { get; } = [];

    public List<string> Paragraphs { get; } = [];
}
