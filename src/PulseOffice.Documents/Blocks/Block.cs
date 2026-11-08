namespace PulseOffice.Documents.Blocks;

public abstract class Block(string id)
{
    public string Id { get; } = id;

    public abstract T Accept<T>(IBlockVisitor<T> visitor);

    public abstract Block Clone();
}

public interface IBlockVisitor<out T>
{
    T VisitParagraph(ParagraphBlock paragraph);

    T VisitTable(TableBlock table);

    T VisitSection(SectionBlock section);
}

public sealed class BlockContainer
{
    private readonly List<Block> _blocks = [];

    public BlockContainer()
    {
    }

    public BlockContainer(IEnumerable<Block> blocks) => _blocks.AddRange(blocks);

    public IReadOnlyList<Block> Blocks => _blocks;

    public int Count => _blocks.Count;

    public Block this[int index] => _blocks[index];

    public int IndexOf(Block block) => _blocks.IndexOf(block);

    public int IndexOf(string id) => _blocks.FindIndex(block => block.Id == id);

    public void Add(Block block) => _blocks.Add(block);

    public void Insert(int index, Block block) => _blocks.Insert(index, block);

    public void InsertRange(int index, IEnumerable<Block> blocks) => _blocks.InsertRange(index, blocks);

    public void RemoveAt(int index) => _blocks.RemoveAt(index);

    public void RemoveRange(int index, int count) => _blocks.RemoveRange(index, count);

    public bool Remove(Block block) => _blocks.Remove(block);

    public void Replace(IEnumerable<Block> blocks)
    {
        var copy = blocks.ToList();
        _blocks.Clear();
        _blocks.AddRange(copy);
    }

    public BlockContainer Clone() => new(_blocks.Select(block => block.Clone()));
}
