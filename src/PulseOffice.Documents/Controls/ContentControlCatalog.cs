using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;

namespace PulseOffice.Documents.Controls;

public sealed record ContentControlSpan(string Id, ContentControl Control, DocumentPosition Start, DocumentPosition End, int StartOrder, int EndOrder)
{
    public bool InOneParagraph => Start.ParagraphId == End.ParagraphId;

    public bool IsEmpty => InOneParagraph && Start.Offset == End.Offset;
}

public interface IContentControlCatalog
{
    IReadOnlyList<ContentControlSpan> All(TextDocument document);

    ContentControlSpan? Find(TextDocument document, string controlId);

    ContentControlSpan? At(TextDocument document, DocumentPosition position);

    int Order(TextDocument document, string paragraphId);

    void Join(TextDocument document);
}

public interface ILiveContentControlCatalog : IContentControlCatalog;

public sealed class ContentControlCatalog(IStoryCatalog stories, IBlockTree tree) : ILiveContentControlCatalog
{
    private sealed record Opening(ContentControlMark Mark, string ParagraphId, int Order);

    public IReadOnlyList<ContentControlSpan> All(TextDocument document)
    {
        var spans = new List<ContentControlSpan>();
        var order = 0;
        foreach (var story in stories.Stories(document))
        {
            var open = new Dictionary<string, Opening>(StringComparer.Ordinal);
            ParagraphBlock? last = null;
            var lastOrder = 0;
            foreach (var paragraph in tree.Paragraphs(story))
            {
                order++;
                last = paragraph;
                lastOrder = order;
                foreach (var mark in paragraph.Marks.OfType<ContentControlMark>())
                {
                    if (mark.Edge == MarkEdge.Start)
                    {
                        open.TryAdd(mark.ControlId, new Opening(mark, paragraph.Id, order));
                        continue;
                    }
                    if (mark.Edge != MarkEdge.End || !open.Remove(mark.ControlId, out var start)) continue;
                    spans.Add(new ContentControlSpan(mark.ControlId, start.Mark.Control ?? new ContentControl(),
                        new DocumentPosition(start.ParagraphId, start.Mark.Offset), new DocumentPosition(paragraph.Id, mark.Offset), start.Order, order));
                }
            }
            if (last is null) continue;
            foreach (var (id, start) in open)
                spans.Add(new ContentControlSpan(id, start.Mark.Control ?? new ContentControl(),
                    new DocumentPosition(start.ParagraphId, start.Mark.Offset), new DocumentPosition(last.Id, last.Length), start.Order, lastOrder));
        }
        return spans;
    }

    public ContentControlSpan? Find(TextDocument document, string controlId) => All(document).FirstOrDefault(span => span.Id == controlId);

    public ContentControlSpan? At(TextDocument document, DocumentPosition position)
    {
        var spans = All(document);
        if (spans.Count == 0) return null;
        var order = Order(document, position.ParagraphId);
        return spans
            .Where(span => (span.StartOrder, span.Start.Offset).CompareTo((order, position.Offset)) <= 0 && (order, position.Offset).CompareTo((span.EndOrder, span.End.Offset)) <= 0)
            .OrderByDescending(span => (span.StartOrder, span.Start.Offset))
            .FirstOrDefault();
    }

    public int Order(TextDocument document, string paragraphId)
    {
        var order = 0;
        foreach (var paragraph in stories.Paragraphs(document))
        {
            order++;
            if (paragraph.Id == paragraphId) return order;
        }
        return -1;
    }

    public void Join(TextDocument document)
    {
        var segments = stories.Paragraphs(document)
            .SelectMany(paragraph => paragraph.Marks.OfType<ContentControlMark>().Select(mark => (Paragraph: paragraph, Mark: mark)))
            .GroupBy(entry => entry.Mark.ControlId, StringComparer.Ordinal)
            .Where(group => group.Count(entry => entry.Mark.Edge == MarkEdge.Start) > 1)
            .ToList();
        foreach (var group in segments)
        {
            var entries = group.ToList();
            var first = entries.First(entry => entry.Mark.Edge == MarkEdge.Start);
            var final = entries.LastOrDefault(entry => entry.Mark.Edge == MarkEdge.End);
            foreach (var entry in entries.Where(entry => !ReferenceEquals(entry.Mark, first.Mark) && !ReferenceEquals(entry.Mark, final.Mark)))
                entry.Paragraph.Marks = [.. entry.Paragraph.Marks.Where(mark => !ReferenceEquals(mark, entry.Mark))];
        }
        if (segments.Count > 0) document.Raise();
    }
}
