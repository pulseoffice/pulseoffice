using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Document.Embedding;

public enum EmbeddedObjectKind
{
    Chart,
    Image,
    Shape,
    ShapeGroup,
    Table,
    SheetRange,
    TextFrame,
    Formula,
    Media,
    FormControl,
}

public enum EmbedMode
{
    Snapshot,
    Linked,
    Embedded,
}

public abstract record EmbeddedObject
{
    public required string Id { get; init; }
    public abstract EmbeddedObjectKind Kind { get; }

    public string? Name { get; init; }

    public string? Title { get; init; }
    public string? Description { get; init; }

    public DocumentSize NaturalSize { get; init; } = new(Twip.FromCentimeters(12), Twip.FromCentimeters(8));

    public EmbedMode Mode { get; init; } = EmbedMode.Embedded;

    public ObjectSource? Source { get; init; }

    public abstract IReadOnlyCollection<ModuleKind> EditableIn { get; }
}

public sealed record ObjectSource(
    string DocumentPath,
    string Reference,
    DateTimeOffset? LastRefresh = null)
{
    public bool IsInternal => string.IsNullOrEmpty(DocumentPath);
}
