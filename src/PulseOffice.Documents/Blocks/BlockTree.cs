namespace PulseOffice.Documents.Blocks;

public sealed record BlockPath(IReadOnlyList<int> Steps)
{
    public override string ToString() => string.Join('/', Steps);

    public bool Equals(BlockPath? other) => other is not null && Steps.SequenceEqual(other.Steps);

    public override int GetHashCode() => Steps.Aggregate(17, (hash, step) => hash * 31 + step);
}

public sealed record BlockLocation(BlockContainer Container, int Index, BlockPath Path)
{
    public Block Block => Container[Index];
}

public sealed record NestedContainer(BlockContainer Container, IReadOnlyList<int> Steps);

public interface IBlockTree
{
    IEnumerable<ParagraphBlock> Paragraphs(BlockContainer root);

    IEnumerable<Block> Blocks(BlockContainer root);

    BlockLocation? Locate(BlockContainer root, string blockId);

    Block? Resolve(BlockContainer root, BlockPath path);
}

public sealed class BlockTree : IBlockTree
{
    private readonly NestedContainers _nested = new();

    public IEnumerable<ParagraphBlock> Paragraphs(BlockContainer root) => Blocks(root).OfType<ParagraphBlock>();

    public IEnumerable<Block> Blocks(BlockContainer root)
    {
        foreach (var block in root.Blocks)
        {
            yield return block;
            foreach (var nested in block.Accept(_nested))
            {
                foreach (var inner in Blocks(nested.Container)) yield return inner;
            }
        }
    }

    public BlockLocation? Locate(BlockContainer root, string blockId) => Locate(root, blockId, []);

    public Block? Resolve(BlockContainer root, BlockPath path)
    {
        var container = root;
        var steps = path.Steps;
        var position = 0;
        while (position < steps.Count)
        {
            var index = steps[position++];
            if (index < 0 || index >= container.Count) return null;
            var block = container[index];
            if (position == steps.Count) return block;
            var match = block.Accept(_nested).FirstOrDefault(nested => Prefix(nested.Steps, steps, position));
            if (match is null) return null;
            position += match.Steps.Count;
            container = match.Container;
        }
        return null;
    }

    private BlockLocation? Locate(BlockContainer container, string blockId, IReadOnlyList<int> prefix)
    {
        for (var index = 0; index < container.Count; index++)
        {
            var block = container[index];
            List<int> here = [.. prefix, index];
            if (block.Id == blockId) return new BlockLocation(container, index, new BlockPath(here));
            foreach (var nested in block.Accept(_nested))
            {
                var found = Locate(nested.Container, blockId, [.. here, .. nested.Steps]);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private bool Prefix(IReadOnlyList<int> candidate, IReadOnlyList<int> steps, int from) =>
        from + candidate.Count <= steps.Count && candidate.Select((step, i) => steps[from + i] == step).All(equal => equal);
}

public sealed class NestedContainers : IBlockVisitor<IReadOnlyList<NestedContainer>>
{
    public IReadOnlyList<NestedContainer> VisitParagraph(ParagraphBlock paragraph) => [];

    public IReadOnlyList<NestedContainer> VisitSection(SectionBlock section) => [new(section.Content, [])];

    public IReadOnlyList<NestedContainer> VisitTable(TableBlock table) =>
        [.. table.Rows.SelectMany((row, r) => row.Cells.Select((cell, c) => new NestedContainer(cell.Content, [r, c])))];
}
