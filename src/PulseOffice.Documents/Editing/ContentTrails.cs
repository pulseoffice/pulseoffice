using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public sealed record TrailStep(BlockContainer Container, int Index)
{
    public Block Block => Container[Index];
}

public sealed record ContentTrail(ParagraphBlock Paragraph, BlockContainer Story, IReadOnlyList<TrailStep> Steps, IReadOnlyList<int> Order)
{
    public int Depth => Steps.Count;
}

public sealed record TrailPoint(ContentTrail Trail, int Offset)
{
    public int CompareTo(TrailPoint other)
    {
        var count = Math.Min(Trail.Order.Count, other.Trail.Order.Count);
        for (var i = 0; i < count; i++)
        {
            var difference = Trail.Order[i].CompareTo(other.Trail.Order[i]);
            if (difference != 0) return difference;
        }
        var depth = Trail.Order.Count.CompareTo(other.Trail.Order.Count);
        return depth != 0 ? depth : Offset.CompareTo(other.Offset);
    }
}

public sealed record TrailBound(TrailPoint Point, int Level)
{
    public int Index => Point.Trail.Steps[Level].Index;

    public bool IsLeaf => Level == Point.Trail.Depth - 1;

    public TrailBound Inner => IsLeaf ? this : new TrailBound(Point, Level + 1);

    public BlockContainer Container => Point.Trail.Steps[Level].Container;
}

public interface IContentTrails
{
    ContentTrail Find(TextDocument document, string paragraphId);

    TrailPoint Point(TextDocument document, DocumentPosition position);

    int CommonDepth(ContentTrail first, ContentTrail second);
}

public sealed class ContentTrails(IStoryCatalog stories) : IContentTrails
{
    private readonly NestedContainers _nested = new();

    public ContentTrail Find(TextDocument document, string paragraphId)
    {
        foreach (var story in stories.Stories(document))
        {
            var found = Search(story, paragraphId, [], []);
            if (found is not null) return found with { Story = story };
        }
        throw new KeyNotFoundException($"Paragraph '{paragraphId}' not found.");
    }

    public TrailPoint Point(TextDocument document, DocumentPosition position) => new(Find(document, position.ParagraphId), position.Offset);

    public int CommonDepth(ContentTrail first, ContentTrail second)
    {
        var depth = 0;
        var count = Math.Min(first.Depth, second.Depth);
        while (depth < count && ReferenceEquals(first.Steps[depth].Container, second.Steps[depth].Container))
        {
            depth++;
        }
        return depth;
    }

    private ContentTrail? Search(BlockContainer container, string paragraphId, IReadOnlyList<TrailStep> steps, IReadOnlyList<int> order)
    {
        for (var index = 0; index < container.Count; index++)
        {
            var block = container[index];
            List<TrailStep> here = [.. steps, new TrailStep(container, index)];
            List<int> path = [.. order, index];
            if (block is ParagraphBlock paragraph && paragraph.Id == paragraphId)
            {
                return new ContentTrail(paragraph, container, here, path);
            }
            foreach (var nested in block.Accept(_nested))
            {
                var found = Search(nested.Container, paragraphId, here, [.. path, .. nested.Steps]);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
