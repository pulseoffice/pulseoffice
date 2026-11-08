using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Fields;

public sealed record OutlineEntry(ParagraphBlock Paragraph, int Level)
{
    public string Text => Paragraph.Text;
}

public interface IDocumentOutline
{
    int? LevelOf(TextDocument document, ParagraphBlock paragraph);

    IReadOnlyList<OutlineEntry> Headings(TextDocument document, int maximumLevel = 10);
}

public sealed class DocumentOutline(IBlockTree tree, IStyleResolver resolver) : IDocumentOutline
{
    public int? LevelOf(TextDocument document, ParagraphBlock paragraph)
    {
        if (paragraph.Content.OutlineLevel is { } direct) return direct > 0 ? direct : null;
        return resolver.ResolveParagraphStyle(document.Styles, paragraph.StyleName).OutlineLevel is { } level and > 0 ? level : null;
    }

    public IReadOnlyList<OutlineEntry> Headings(TextDocument document, int maximumLevel = 10) =>
        [.. tree.Paragraphs(document.Body)
            .Select(paragraph => (paragraph, level: LevelOf(document, paragraph)))
            .Where(entry => entry.level is { } level && level <= maximumLevel && entry.paragraph.Text.Trim().Length > 0)
            .Select(entry => new OutlineEntry(entry.paragraph, entry.level!.Value))];
}

public sealed record BookmarkEntry(string Name, string ParagraphId, int Offset, string EndParagraphId, int EndOffset)
{
    public bool IsPoint => ParagraphId == EndParagraphId && Offset == EndOffset;
}

public interface IBookmarkCatalog
{
    IReadOnlyList<BookmarkEntry> Bookmarks(TextDocument document);

    BookmarkEntry? Find(TextDocument document, string name);

    IReadOnlyList<BookmarkEntry> SequenceTargets(TextDocument document);

    string TextOf(TextDocument document, BookmarkEntry bookmark);
}

public sealed class BookmarkCatalog(Editing.IStoryCatalog stories) : IBookmarkCatalog
{
    public IReadOnlyList<BookmarkEntry> Bookmarks(TextDocument document)
    {
        var open = new Dictionary<string, (string Paragraph, int Offset)>(StringComparer.Ordinal);
        var found = new List<BookmarkEntry>();
        foreach (var paragraph in stories.Paragraphs(document))
        {
            foreach (var mark in paragraph.Marks.OfType<BookmarkMark>().OrderBy(mark => mark.Offset))
            {
                switch (mark.Edge)
                {
                    case MarkEdge.Point:
                        found.Add(new BookmarkEntry(mark.Name, paragraph.Id, mark.Offset, paragraph.Id, mark.Offset));
                        break;
                    case MarkEdge.Start:
                        open[mark.Name] = (paragraph.Id, mark.Offset);
                        break;
                    case MarkEdge.End when open.Remove(mark.Name, out var start):
                        found.Add(new BookmarkEntry(mark.Name, start.Paragraph, start.Offset, paragraph.Id, mark.Offset));
                        break;
                }
            }
        }
        found.AddRange(open.Select(pair => new BookmarkEntry(pair.Key, pair.Value.Paragraph, pair.Value.Offset, pair.Value.Paragraph, pair.Value.Offset)));
        return found;
    }

    public BookmarkEntry? Find(TextDocument document, string name) =>
        Bookmarks(document).FirstOrDefault(bookmark => string.Equals(bookmark.Name, name, StringComparison.Ordinal))
        ?? SequenceTargets(document).FirstOrDefault(target => string.Equals(target.Name, name, StringComparison.Ordinal));

    public IReadOnlyList<BookmarkEntry> SequenceTargets(TextDocument document)
    {
        var found = new List<BookmarkEntry>();
        foreach (var paragraph in stories.Paragraphs(document))
        {
            var offset = 0;
            foreach (var run in paragraph.Content.Runs)
            {
                if (run.Field == PulseOffice.Text.Rich.TextFieldKind.Sequence && run.FieldDetail?.Format is { Length: > 0 } name)
                    found.Add(new BookmarkEntry(name, paragraph.Id, offset, paragraph.Id, offset));
                offset += run.Text.Length;
            }
        }
        return found;
    }

    public string TextOf(TextDocument document, BookmarkEntry bookmark)
    {
        var first = stories.Find(document, bookmark.ParagraphId).Paragraph;
        if (bookmark.IsPoint) return first.Text;
        if (bookmark.ParagraphId == bookmark.EndParagraphId)
            return first.Text[Math.Min(bookmark.Offset, first.Length)..Math.Min(bookmark.EndOffset, first.Length)];
        var last = stories.Find(document, bookmark.EndParagraphId).Paragraph;
        var ordered = stories.Paragraphs(document).ToList();
        var from = ordered.FindIndex(paragraph => ReferenceEquals(paragraph, first));
        var to = ordered.FindIndex(paragraph => ReferenceEquals(paragraph, last));
        var middle = from >= 0 && to > from + 1 ? ordered[(from + 1)..to].Select(paragraph => paragraph.Text) : [];
        return string.Join(" ", [first.Text[Math.Min(bookmark.Offset, first.Length)..], .. middle, last.Text[..Math.Min(bookmark.EndOffset, last.Length)]]);
    }
}
