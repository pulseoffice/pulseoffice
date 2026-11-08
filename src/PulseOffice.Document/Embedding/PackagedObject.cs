using PulseOffice.Foundation.Modules;

namespace PulseOffice.Document.Embedding;

public enum PackagedObjectKind { File, Media }

public sealed record PackagedObject : EmbeddedObject
{
    public override EmbeddedObjectKind Kind =>
        Packaged == PackagedObjectKind.Media ? EmbeddedObjectKind.Media : EmbeddedObjectKind.Table;

    public PackagedObjectKind Packaged { get; init; } = PackagedObjectKind.File;

    public required string FileName { get; init; }

    public required string MediaType { get; init; }

    public required ReadOnlyMemory<byte> Data { get; init; }

    public ReadOnlyMemory<byte>? Replacement { get; init; }

    public bool HasReplacement => Replacement is { Length: > 0 };

    public string? LinkedPath { get; init; }

    public bool IsLinked => LinkedPath is { Length: > 0 };

    public long Size => Data.Length;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];
}
