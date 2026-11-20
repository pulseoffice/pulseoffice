using System.Runtime.CompilerServices;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.UI.Editing;

public sealed record CommentFind(string ParagraphId, int From, int To);

public interface IFolioFindHighlights
{
    void Show(FolioEditor editor, SearchQuery query);

    void Clear(FolioEditor editor);

    IReadOnlyDictionary<string, IReadOnlyList<(int From, int To)>> Of(FolioEditor editor);

    IReadOnlyDictionary<string, IReadOnlyList<CommentFind>> InComments(FolioEditor editor);

    void Focus(FolioEditor editor, SearchMatch? match);

    SearchMatch? Focused(FolioEditor editor);
}

public sealed class FolioFindHighlights(IDocumentSearch search, ISearchStories stories, IParagraphOrder order) : IFolioFindHighlights
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(int From, int To)>> None =
        new Dictionary<string, IReadOnlyList<(int From, int To)>>(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<CommentFind>> NoComments =
        new Dictionary<string, IReadOnlyList<CommentFind>>(StringComparer.Ordinal);

    private readonly ConditionalWeakTable<FolioEditor, Entry> _entries = new();

    private readonly ConditionalWeakTable<FolioEditor, StrongBox<SearchMatch?>> _focused = new();

    public void Focus(FolioEditor editor, SearchMatch? match)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var current = Focused(editor);
        if (current == match) return;
        _focused.AddOrUpdate(editor, new StrongBox<SearchMatch?>(match));
        editor.Refresh();
    }

    public SearchMatch? Focused(FolioEditor editor) =>
        editor is not null && _focused.TryGetValue(editor, out var box) ? box.Value : null;

    public IReadOnlyDictionary<string, IReadOnlyList<CommentFind>> InComments(FolioEditor editor)
    {
        Of(editor);
        return _entries.TryGetValue(editor, out var entry) && entry.Comments is { } comments ? comments : NoComments;
    }

    public void Show(FolioEditor editor, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _entries.AddOrUpdate(editor, new Entry(query));
        editor.Refresh();
    }

    public void Clear(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (_entries.Remove(editor)) editor.Refresh();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<(int From, int To)>> Of(FolioEditor editor)
    {
        if (!_entries.TryGetValue(editor, out var entry) || entry.Query.IsEmpty) return None;
        if (entry.Revision == editor.Document.Revision && entry.Map is { } cached) return cached;
        var map = new Dictionary<string, List<(int From, int To)>>(StringComparer.Ordinal);
        var comments = new Dictionary<string, List<CommentFind>>(StringComparer.Ordinal);
        foreach (var match in search.Matches(stories.From(editor.Document, editor.Caret.ParagraphId), entry.Query))
        {
            if (match.CommentId is { } commentId)
            {
                if (!comments.TryGetValue(commentId, out var found)) comments[commentId] = found = [];
                var end = match.Range.End.ParagraphId == match.Range.Start.ParagraphId ? match.Range.End.Offset : int.MaxValue;
                found.Add(new CommentFind(match.Range.Start.ParagraphId, match.Range.Start.Offset, end));
                continue;
            }
            var range = match.Range;
            foreach (var paragraph in order.Between(editor.Document, range.Start, range.End))
            {
                var from = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
                var to = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length;
                if (!map.TryGetValue(paragraph.Id, out var spans)) map[paragraph.Id] = spans = [];
                spans.Add((from, to));
            }
        }
        entry.Comments = comments.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<CommentFind>)pair.Value, StringComparer.Ordinal);
        entry.Revision = editor.Document.Revision;
        entry.Map = map.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<(int From, int To)>)pair.Value, StringComparer.Ordinal);
        return entry.Map;
    }

    private sealed class Entry(SearchQuery query)
    {
        public SearchQuery Query { get; } = query;

        public long Revision { get; set; } = -1;

        public IReadOnlyDictionary<string, IReadOnlyList<(int From, int To)>>? Map { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyList<CommentFind>>? Comments { get; set; }
    }
}
