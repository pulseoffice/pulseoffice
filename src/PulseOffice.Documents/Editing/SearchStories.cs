using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public interface ISearchStories
{
    IReadOnlyList<SearchStory> From(TextDocument document, string paragraphId);
}

public sealed class SearchStories(IStoryCatalog stories, IBlockTree tree) : ISearchStories
{
    public IReadOnlyList<SearchStory> From(TextDocument document, string paragraphId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var current = stories.Find(document, paragraphId).Story;
        var seen = new HashSet<BlockContainer>(ReferenceEqualityComparer.Instance);
        var found = new List<SearchStory>();
        void Add(BlockContainer story, string? commentId = null)
        {
            if (seen.Add(story))
                found.Add(new SearchStory([.. tree.Paragraphs(story)]) { CommentId = commentId, Styles = document.Styles, SectionStarts = SectionStarts(story) });
        }
        var comment = document.Comments.All.FirstOrDefault(item => ReferenceEquals(item.Content, current));
        Add(current, comment?.Id);
        Add(document.Body);
        foreach (var note in document.Notes.All) Add(note.Content);
        foreach (var item in document.Objects.All)
        {
            if (item.Content.Story is { } story) Add(story);
        }
        foreach (var page in UsedPageStyles(document))
        {
            foreach (var part in new[] { page.Header, page.Footer })
            {
                if (!part.Enabled) continue;
                Add(part.Default);
                if (page.FirstPageDifferent && part.First is { } first) Add(first);
                if (page.EvenPagesDifferent && part.Even is { } even) Add(even);
            }
        }
        foreach (var item in document.Comments.All) Add(item.Content, item.Id);
        return found;
    }

    private HashSet<string> SectionStarts(BlockContainer story)
    {
        var owner = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in tree.Blocks(story).OfType<SectionBlock>())
            foreach (var paragraph in tree.Paragraphs(section.Content))
                owner[paragraph.Id] = section.Id;
        var starts = new HashSet<string>(StringComparer.Ordinal);
        if (owner.Count == 0) return starts;
        string? previous = null;
        var first = true;
        foreach (var paragraph in tree.Paragraphs(story))
        {
            var current = owner.GetValueOrDefault(paragraph.Id);
            if (!first && !string.Equals(current, previous, StringComparison.Ordinal)) starts.Add(paragraph.Id);
            previous = current;
            first = false;
        }
        return starts;
    }

    private IEnumerable<PageStyle> UsedPageStyles(TextDocument document)
    {
        var names = tree.Blocks(document.Body)
            .SelectMany(block => block switch
            {
                ParagraphBlock paragraph => new[] { paragraph.Content.PageStyleName, paragraph.Content.NextPageStyleName },
                TableBlock table => new[] { table.PageStyleName },
                _ => Array.Empty<string?>(),
            })
            .OfType<string>()
            .Append(document.InitialPageStyle);
        var pending = new Queue<string>(names);
        var used = new List<PageStyle>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var name))
        {
            if (!seen.Add(name) || document.Styles.Page.Find(name) is not { } style) continue;
            used.Add(style);
            if (style.NextStyleName is { } next) pending.Enqueue(next);
        }
        return used;
    }
}
