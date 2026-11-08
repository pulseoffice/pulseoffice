using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxIndexRanges(IBlockTree tree)
{
    public void Join(TextDocument document, IReadOnlyList<(IndexEntryMark Entry, string Bookmark)> ranges, IReadOnlySet<string> referenced)
    {
        if (ranges.Count == 0) return;
        var paragraphs = tree.Paragraphs(document.Body).Concat(document.Notes.All.SelectMany(note => tree.Paragraphs(note.Content))).ToList();
        var opened = paragraphs.SelectMany(paragraph => paragraph.Marks).OfType<BookmarkMark>().Where(mark => mark.Edge != MarkEdge.End)
            .Select(mark => mark.Name).ToHashSet(StringComparer.Ordinal);
        var entries = new Dictionary<string, IndexEntryMark>(StringComparer.Ordinal);
        var replaced = new HashSet<IndexEntryMark>(ReferenceEqualityComparer.Instance);
        foreach (var (entry, bookmark) in ranges)
        {
            if (!opened.Contains(bookmark) || !entries.TryAdd(bookmark, entry)) continue;
            replaced.Add(entry);
        }
        foreach (var paragraph in paragraphs)
        {
            if (!paragraph.Marks.Any(mark => mark is BookmarkMark bookmark && entries.ContainsKey(bookmark.Name) || mark is IndexEntryMark found && replaced.Contains(found))) continue;
            var marks = new List<InlineMark>();
            foreach (var mark in paragraph.Marks)
            {
                if (mark is IndexEntryMark point && replaced.Contains(point)) continue;
                if (mark is BookmarkMark bookmark && entries.TryGetValue(bookmark.Name, out var entry))
                {
                    marks.Add(entry with { Offset = bookmark.Offset, Edge = bookmark.Edge });
                    if (!referenced.Contains(bookmark.Name)) continue;
                }
                marks.Add(mark);
            }
            paragraph.Marks = marks;
        }
    }
}
