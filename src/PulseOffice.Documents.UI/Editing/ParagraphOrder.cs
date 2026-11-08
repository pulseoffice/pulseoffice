using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.UI.Editing;

public interface IParagraphOrder
{
    int Compare(TextDocument document, DocumentPosition first, DocumentPosition second);

    IReadOnlyList<ParagraphBlock> Between(TextDocument document, DocumentPosition start, DocumentPosition end);

    IReadOnlyList<ParagraphBlock> StoryOf(TextDocument document, string paragraphId);

    bool Contains(TextDocument document, string paragraphId);

    int IndexOf(TextDocument document, string paragraphId);

    IReadOnlyDictionary<string, int> Indexes(TextDocument document);
}

public sealed class ParagraphOrder(IStoryCatalog stories, IBlockTree tree) : IParagraphOrder
{
    public int Compare(TextDocument document, DocumentPosition first, DocumentPosition second)
    {
        var order = IndexOf(document, first.ParagraphId).CompareTo(IndexOf(document, second.ParagraphId));
        return order != 0 ? order : first.Offset.CompareTo(second.Offset);
    }

    public IReadOnlyList<ParagraphBlock> Between(TextDocument document, DocumentPosition start, DocumentPosition end)
    {
        var all = stories.Paragraphs(document).ToList();
        var from = all.FindIndex(paragraph => paragraph.Id == start.ParagraphId);
        var to = all.FindIndex(paragraph => paragraph.Id == end.ParagraphId);
        if (from < 0 || to < 0 || to < from) return [];
        return all.GetRange(from, to - from + 1);
    }

    public IReadOnlyList<ParagraphBlock> StoryOf(TextDocument document, string paragraphId) =>
        [.. tree.Paragraphs(stories.Find(document, paragraphId).Story)];

    public bool Contains(TextDocument document, string paragraphId) =>
        stories.Paragraphs(document).Any(paragraph => paragraph.Id == paragraphId);

    public int IndexOf(TextDocument document, string paragraphId)
    {
        var index = 0;
        foreach (var paragraph in stories.Paragraphs(document))
        {
            if (paragraph.Id == paragraphId) return index;
            index++;
        }
        return -1;
    }

    public IReadOnlyDictionary<string, int> Indexes(TextDocument document)
    {
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;
        foreach (var paragraph in stories.Paragraphs(document)) indexes.TryAdd(paragraph.Id, index++);
        return indexes;
    }
}
