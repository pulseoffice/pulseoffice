using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Navigation;

public enum NavigationCategory
{
    Headings,
    Tables,
    Frames,
    Images,
    Objects,
    Bookmarks,
    Sections,
    Hyperlinks,
    References,
    Indexes,
    Comments,
    Fields,
    Footnotes,
    Endnotes,
}

public sealed record NavigationTarget(string? ParagraphId, int Offset, string? ObjectId = null);

public sealed record NavigationEntry(NavigationCategory Category, string Id, string Title, int Depth, NavigationTarget Target);

public interface IDocumentNavigation
{
    IReadOnlyList<NavigationEntry> Entries(TextDocument document, NavigationCategory category);

    NavigationTarget? Resolve(TextDocument document, string link);
}

public sealed class DocumentNavigation(
    IBlockTree tree,
    IDocumentOutline outline,
    IBookmarkCatalog bookmarks,
    IDocumentReadingOrder order,
    IStoryCatalog stories) : IDocumentNavigation
{
    private const int TitleLength = 60;

    public IReadOnlyList<NavigationEntry> Entries(TextDocument document, NavigationCategory category) => category switch
    {
        NavigationCategory.Headings => [.. outline.Headings(document).Select(heading =>
            new NavigationEntry(category, heading.Paragraph.Id, Short(heading.Text), heading.Level - 1, new NavigationTarget(heading.Paragraph.Id, 0)))],
        NavigationCategory.Tables => [.. tree.Blocks(document.Body).OfType<TableBlock>().Select((table, i) =>
            new NavigationEntry(category, table.Id, table.Name ?? "Table" + (i + 1), 0, new NavigationTarget(FirstParagraph(table), 0)))],
        NavigationCategory.Frames => Objects(document, category, item => item.Content is TextBoxContent),
        NavigationCategory.Images => Objects(document, category, item => item.Content is EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject }),
        NavigationCategory.Objects => Objects(document, category, item => item.Content is EmbeddedContent { Value: not PulseOffice.Drawing.Assets.RasterImageObject }),
        NavigationCategory.Bookmarks => [.. bookmarks.Bookmarks(document).Where(bookmark => !bookmark.Name.StartsWith('_'))
            .Select(bookmark => new NavigationEntry(category, bookmark.Name, bookmark.Name, 0, new NavigationTarget(bookmark.ParagraphId, bookmark.Offset)))],
        NavigationCategory.Sections => [.. tree.Blocks(document.Body).OfType<SectionBlock>().Where(section => !section.IsIndex)
            .Select(section => new NavigationEntry(category, section.Id, section.Name, 0, new NavigationTarget(FirstParagraph(section), 0)))],
        NavigationCategory.Indexes => [.. tree.Blocks(document.Body).OfType<SectionBlock>().Where(section => section.IsIndex)
            .Select(section => new NavigationEntry(category, section.Id, section.Index!.Title is { Length: > 0 } title ? title : section.Name, 0, new NavigationTarget(FirstParagraph(section), 0)))],
        NavigationCategory.Hyperlinks => Runs(document, category, run => run.Hyperlink is not null && run.Field == TextFieldKind.None, run => run.Text),
        NavigationCategory.References => Runs(document, category, run => run.Field is TextFieldKind.Bookmark or TextFieldKind.Reference or TextFieldKind.NoteReference, run => run.FieldArgument ?? run.Text),
        NavigationCategory.Fields => Runs(document, category, run => run.Field is not (TextFieldKind.None or TextFieldKind.Bookmark or TextFieldKind.Reference or TextFieldKind.NoteReference), run => run.Field + ": " + run.Text),
        NavigationCategory.Comments => Comments(document),
        NavigationCategory.Footnotes => Notes(document, NoteKind.Footnote, category),
        _ => Notes(document, NoteKind.Endnote, category),
    };

    public NavigationTarget? Resolve(TextDocument document, string link)
    {
        if (!link.StartsWith('#')) return null;
        var name = link[1..];
        var separator = name.LastIndexOf('|');
        if (separator >= 0)
        {
            var kind = name[(separator + 1)..];
            var text = name[..separator];
            if (kind == "outline" && outline.Headings(document).FirstOrDefault(heading => heading.Text == text) is { } heading)
                return new NavigationTarget(heading.Paragraph.Id, 0);
            if (kind == "table" && tree.Blocks(document.Body).OfType<TableBlock>().FirstOrDefault(table => table.Name == text) is { } table)
                return new NavigationTarget(FirstParagraph(table), 0);
            if (kind is "frame" or "graphic" or "ole" && document.Objects.All.FirstOrDefault(item => item.Name == text) is { } item)
                return new NavigationTarget(null, 0, item.Id);
            if (kind == "region" && tree.Blocks(document.Body).OfType<SectionBlock>().FirstOrDefault(section => section.Name == text) is { } section)
                return new NavigationTarget(FirstParagraph(section), 0);
            name = text;
        }
        return bookmarks.Find(document, name) is { } bookmark ? new NavigationTarget(bookmark.ParagraphId, bookmark.Offset) : null;
    }

    private string Short(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= TitleLength ? flat : flat[..TitleLength] + "…";
    }

    private string? FirstParagraph(Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Id,
        TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => tree.Paragraphs(cell.Content)).FirstOrDefault()?.Id,
        SectionBlock section => tree.Paragraphs(section.Content).FirstOrDefault()?.Id,
        _ => null,
    };

    private IReadOnlyList<NavigationEntry> Objects(TextDocument document, NavigationCategory category, Func<AnchoredObject, bool> filter) =>
        [.. document.Objects.All.Where(filter).Select(item => new NavigationEntry(category, item.Id, item.Name ?? item.Id, 0, new NavigationTarget(null, 0, item.Id)))];

    private IReadOnlyList<NavigationEntry> Runs(TextDocument document, NavigationCategory category, Func<TextRun, bool> filter, Func<TextRun, string> title)
    {
        var found = new List<NavigationEntry>();
        foreach (var paragraph in order.Paragraphs(document))
        {
            var offset = 0;
            foreach (var run in paragraph.Content.Runs)
            {
                if (filter(run))
                    found.Add(new NavigationEntry(category, paragraph.Id + ":" + offset, Short(title(run)), 0, new NavigationTarget(paragraph.Id, offset)));
                offset += run.Text.Length;
            }
        }
        return found;
    }

    private IReadOnlyList<NavigationEntry> Comments(TextDocument document)
    {
        var anchors = new Dictionary<string, NavigationTarget>(StringComparer.Ordinal);
        foreach (var paragraph in stories.Paragraphs(document))
            foreach (var mark in paragraph.Marks.OfType<CommentMark>().Where(mark => mark.Edge != MarkEdge.End))
                anchors.TryAdd(mark.CommentId, new NavigationTarget(paragraph.Id, mark.Offset));
        return [.. document.Comments.All.Where(comment => comment.ParentId is null).Select(comment => new NavigationEntry(NavigationCategory.Comments, comment.Id,
            Short(comment.Author + ": " + string.Join(' ', tree.Paragraphs(comment.Content).Select(paragraph => paragraph.Text))), 0,
            anchors.GetValueOrDefault(comment.Id, new NavigationTarget(null, 0))))];
    }

    private IReadOnlyList<NavigationEntry> Notes(TextDocument document, NoteKind kind, NavigationCategory category)
    {
        var anchors = new Dictionary<string, NavigationTarget>(StringComparer.Ordinal);
        foreach (var paragraph in order.Paragraphs(document))
            foreach (var mark in paragraph.Marks.OfType<NoteMark>())
                anchors.TryAdd(mark.NoteId, new NavigationTarget(paragraph.Id, mark.Offset));
        return [.. document.Notes.OfKind(kind).Select(note => new NavigationEntry(category, note.Id,
            Short(string.Join(' ', tree.Paragraphs(note.Content).Select(paragraph => paragraph.Text))), 0,
            anchors.GetValueOrDefault(note.Id, new NavigationTarget(null, 0))))];
    }
}
