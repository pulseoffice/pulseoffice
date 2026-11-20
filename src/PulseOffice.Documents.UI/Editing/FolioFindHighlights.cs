using System.Runtime.CompilerServices;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioFindHighlights
{
    void Show(FolioEditor editor, SearchQuery query);

    void Clear(FolioEditor editor);

    IReadOnlyDictionary<string, IReadOnlyList<(int From, int To)>> Of(FolioEditor editor);
}

public sealed class FolioFindHighlights(IDocumentSearch search, ISearchStories stories, IParagraphOrder order) : IFolioFindHighlights
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(int From, int To)>> None =
        new Dictionary<string, IReadOnlyList<(int From, int To)>>(StringComparer.Ordinal);

    private readonly ConditionalWeakTable<FolioEditor, Entry> _entries = new();

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
        foreach (var match in search.Matches(stories.From(editor.Document, editor.Caret.ParagraphId), entry.Query))
        {
            if (match.CommentId is not null) continue;
            var range = match.Range;
            foreach (var paragraph in order.Between(editor.Document, range.Start, range.End))
            {
                var from = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
                var to = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length;
                if (!map.TryGetValue(paragraph.Id, out var spans)) map[paragraph.Id] = spans = [];
                spans.Add((from, to));
            }
        }
        entry.Revision = editor.Document.Revision;
        entry.Map = map.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<(int From, int To)>)pair.Value, StringComparer.Ordinal);
        return entry.Map;
    }

    private sealed class Entry(SearchQuery query)
    {
        public SearchQuery Query { get; } = query;

        public long Revision { get; set; } = -1;

        public IReadOnlyDictionary<string, IReadOnlyList<(int From, int To)>>? Map { get; set; }
    }
}
