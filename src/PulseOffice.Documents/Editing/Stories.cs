using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Editing;

public sealed record ParagraphLocation(ParagraphBlock Paragraph, BlockLocation Location, BlockContainer Story);

public enum StoryRole
{
    Body,
    Note,
    Comment,
    Object,
    HeaderFooter,
}

public interface IStoryCatalog
{
    IEnumerable<BlockContainer> Stories(TextDocument document);

    ParagraphLocation Find(TextDocument document, string paragraphId);

    IEnumerable<ParagraphBlock> Paragraphs(TextDocument document);

    StoryRole RoleOf(TextDocument document, string paragraphId);

    bool AcceptsNotes(TextDocument document, string paragraphId);
}

public sealed class StoryCatalog(IBlockTree tree) : IStoryCatalog
{
    public IEnumerable<BlockContainer> Stories(TextDocument document)
    {
        yield return document.Body;
        foreach (var note in document.Notes.All) yield return note.Content;
        foreach (var comment in document.Comments.All) yield return comment.Content;
        foreach (var item in document.Objects.All)
        {
            if (item.Content.Story is { } story) yield return story;
        }
        foreach (var page in document.Styles.Page.All)
        {
            foreach (var part in new[] { page.Header, page.Footer })
            {
                yield return part.Default;
                if (part.First is not null) yield return part.First;
                if (part.Even is not null) yield return part.Even;
            }
        }
    }

    public ParagraphLocation Find(TextDocument document, string paragraphId)
    {
        foreach (var story in Stories(document))
        {
            if (tree.Locate(story, paragraphId) is { Block: ParagraphBlock paragraph } location)
            {
                return new ParagraphLocation(paragraph, location, story);
            }
        }
        throw new KeyNotFoundException($"Paragraph '{paragraphId}' not found.");
    }

    public IEnumerable<ParagraphBlock> Paragraphs(TextDocument document) => Stories(document).SelectMany(tree.Paragraphs);

    public StoryRole RoleOf(TextDocument document, string paragraphId)
    {
        var story = Find(document, paragraphId).Story;
        if (ReferenceEquals(story, document.Body)) return StoryRole.Body;
        if (document.Notes.All.Any(note => ReferenceEquals(note.Content, story))) return StoryRole.Note;
        if (document.Comments.All.Any(comment => ReferenceEquals(comment.Content, story))) return StoryRole.Comment;
        if (document.Objects.All.Any(item => ReferenceEquals(item.Content.Story, story))) return StoryRole.Object;
        return StoryRole.HeaderFooter;
    }

    public bool AcceptsNotes(TextDocument document, string paragraphId) => RoleOf(document, paragraphId) is StoryRole.Body or StoryRole.Object;
}
