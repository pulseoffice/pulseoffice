using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Blocks;

public sealed record SectionColumns(int Count, Twip Gap)
{
    public const int LargestCount = 45;

    public static readonly SectionColumns Single = new(1, Twip.Zero);

    public bool Separator { get; init; }

    public ColumnSeparatorLine SeparatorLine { get; init; } = ColumnSeparatorLine.Default;

    public bool Balanced { get; init; } = true;

    public bool BalancedBeforeSection { get; init; }

    public IReadOnlyList<ColumnSpan> Spans { get; init; } = [];

    public bool Uneven => Count > 1 && Spans.Count == Count;

    public bool Equals(SectionColumns? other) =>
        other is not null && Count == other.Count && Gap == other.Gap && Separator == other.Separator && SeparatorLine == other.SeparatorLine && Balanced == other.Balanced && BalancedBeforeSection == other.BalancedBeforeSection
        && Spans.SequenceEqual(other.Spans);

    public override int GetHashCode() => HashCode.Combine(Count, Gap, Separator, Balanced, Spans.Count);
}

public sealed record ColumnSpan(Twip Width, Twip Gap);

public enum ColumnSeparatorAlignment
{
    Top,
    Middle,
    Bottom,
}

public sealed record ColumnSeparatorLine(PulseOffice.Text.Rich.BorderLineStyle Style, double Width, uint Color)
{
    public const int FullHeight = 100;

    public static readonly ColumnSeparatorLine Default = new(PulseOffice.Text.Rich.BorderLineStyle.Solid, 0.5, 0xFF000000);

    public int HeightPercent { get; init; } = FullHeight;

    public ColumnSeparatorAlignment Alignment { get; init; }
}

public sealed class SectionBlock(string id, string name, BlockContainer content) : Block(id)
{
    public string Name { get; set; } = name;

    public BlockContainer Content { get; } = content;

    public SectionColumns Columns { get; set; } = SectionColumns.Single;

    public bool StartsInNextColumn { get; set; }

    public bool Protected { get; set; }

    public byte[]? ProtectionKey { get; set; }

    public bool Hidden { get; set; }

    public bool? CollectsEndnotes { get; set; }

    public string? Condition { get; set; }

    public string? LinkedFile { get; set; }

    public Indexes.IndexDefinition? Index { get; set; }

    public bool IsIndex => Index is not null;

    public override T Accept<T>(IBlockVisitor<T> visitor) => visitor.VisitSection(this);

    public override Block Clone() => WithContent(Content.Clone(), Index);

    public SectionBlock WithContent(BlockContainer content, Indexes.IndexDefinition? index) => new(Id, Name, content)
    {
        Columns = Columns,
        StartsInNextColumn = StartsInNextColumn,
        Protected = Protected,
        ProtectionKey = ProtectionKey,
        Hidden = Hidden,
        CollectsEndnotes = CollectsEndnotes,
        Condition = Condition,
        LinkedFile = LinkedFile,
        Index = index,
    };
}
