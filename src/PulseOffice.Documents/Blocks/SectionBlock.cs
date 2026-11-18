using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Blocks;

public sealed record SectionColumns(int Count, Twip Gap)
{
    public const int LargestCount = 45;

    public static readonly SectionColumns Single = new(1, Twip.Zero);

    public bool Separator { get; init; }

    public bool Balanced { get; init; } = true;

    public IReadOnlyList<ColumnSpan> Spans { get; init; } = [];

    public bool Uneven => Count > 1 && Spans.Count == Count;

    public bool Equals(SectionColumns? other) =>
        other is not null && Count == other.Count && Gap == other.Gap && Separator == other.Separator && Balanced == other.Balanced
        && Spans.SequenceEqual(other.Spans);

    public override int GetHashCode() => HashCode.Combine(Count, Gap, Separator, Balanced, Spans.Count);
}

public sealed record ColumnSpan(Twip Width, Twip Gap);

public sealed class SectionBlock(string id, string name, BlockContainer content) : Block(id)
{
    public string Name { get; set; } = name;

    public BlockContainer Content { get; } = content;

    public SectionColumns Columns { get; set; } = SectionColumns.Single;

    public bool Protected { get; set; }

    public byte[]? ProtectionKey { get; set; }

    public bool Hidden { get; set; }

    public string? Condition { get; set; }

    public string? LinkedFile { get; set; }

    public Indexes.IndexDefinition? Index { get; set; }

    public bool IsIndex => Index is not null;

    public override T Accept<T>(IBlockVisitor<T> visitor) => visitor.VisitSection(this);

    public override Block Clone() => WithContent(Content.Clone(), Index);

    public SectionBlock WithContent(BlockContainer content, Indexes.IndexDefinition? index) => new(Id, Name, content)
    {
        Columns = Columns,
        Protected = Protected,
        ProtectionKey = ProtectionKey,
        Hidden = Hidden,
        Condition = Condition,
        LinkedFile = LinkedFile,
        Index = index,
    };
}
