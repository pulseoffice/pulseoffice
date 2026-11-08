using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Revisions;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record MarkedParagraph(TextParagraph Content, IReadOnlyList<InlineInsertion> Deletions, string Key);

public sealed record ChangeSpan(int Start, int End, ChangeKind Kind, uint Color, string ChangeId);

public interface IChangeSpans
{
    IReadOnlyList<ChangeSpan> Of(TextDocument document, string paragraphId);
}

public interface IChangeMarkup
{
    MarkedParagraph? Mark(TextDocument document, ParagraphBlock block, TextParagraph content);
}

public sealed class ChangeSpans(IChangeRegions regions, IAuthorColors colors) : IChangeSpans
{
    private sealed class Snapshot
    {
        public long Revision { get; set; } = -1;

        public Dictionary<string, List<ChangeSpan>> Spans { get; set; } = new(StringComparer.Ordinal);
    }

    private readonly ConditionalWeakTable<TextDocument, Snapshot> _snapshots = new();

    public IReadOnlyList<ChangeSpan> Of(TextDocument document, string paragraphId)
    {
        if (document.Changes.All.Count == 0) return [];
        var snapshot = _snapshots.GetValue(document, _ => new Snapshot());
        if (snapshot.Revision != document.Revision)
        {
            snapshot.Spans = Build(document);
            snapshot.Revision = document.Revision;
        }
        return snapshot.Spans.TryGetValue(paragraphId, out var spans) ? spans : [];
    }

    private Dictionary<string, List<ChangeSpan>> Build(TextDocument document)
    {
        var map = regions.Map(document);
        var palette = colors.Of(document);
        var paragraphs = new SortedList<int, string>();
        var result = new Dictionary<string, List<ChangeSpan>>(StringComparer.Ordinal);
        foreach (var region in map.Regions)
        {
            var color = palette.TryGetValue(region.Change.Author, out var found) ? found : 0xFFC69200;
            var first = map.OrderOf(region.Start.ParagraphId);
            var last = map.OrderOf(region.End.ParagraphId);
            Add(result, region.Start.ParagraphId, new ChangeSpan(region.Start.Offset, first == last ? region.End.Offset : int.MaxValue, region.Change.Kind, color, region.Change.Id));
            if (first == last) continue;
            if (paragraphs.Count == 0) Index(document, map, paragraphs);
            for (var order = first + 1; order < last; order++)
            {
                if (paragraphs.TryGetValue(order, out var middle)) Add(result, middle, new ChangeSpan(0, int.MaxValue, region.Change.Kind, color, region.Change.Id));
            }
            Add(result, region.End.ParagraphId, new ChangeSpan(0, region.End.Offset, region.Change.Kind, color, region.Change.Id));
        }
        foreach (var table in document.Body.Blocks.SelectMany(Tables))
        {
            foreach (var row in table.Rows.Where(row => row.ChangeId is not null))
            {
                if (document.Changes.Find(row.ChangeId!) is not { Kind: ChangeKind.Insertion or ChangeKind.Deletion } change) continue;
                var color = palette.TryGetValue(change.Author, out var found) ? found : 0xFFC69200;
                foreach (var paragraph in row.Cells.SelectMany(cell => cell.Content.Blocks).SelectMany(Flatten))
                    Add(result, paragraph.Id, new ChangeSpan(0, int.MaxValue, change.Kind, color, change.Id));
            }
        }
        return result;
    }

    private IEnumerable<TableBlock> Tables(Block block) => block switch
    {
        SectionBlock section => section.Content.Blocks.SelectMany(Tables),
        TableBlock table => [table, .. table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Content.Blocks).SelectMany(Tables)],
        _ => [],
    };

    private void Index(TextDocument document, ChangeMap map, SortedList<int, string> paragraphs)
    {
        foreach (var paragraph in document.Body.Blocks.SelectMany(Flatten))
        {
            var order = map.OrderOf(paragraph.Id);
            if (order >= 0) paragraphs[order] = paragraph.Id;
        }
    }

    private IEnumerable<ParagraphBlock> Flatten(Block block) => block switch
    {
        ParagraphBlock paragraph => [paragraph],
        SectionBlock section => section.Content.Blocks.SelectMany(Flatten),
        TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Content.Blocks).SelectMany(Flatten),
        _ => [],
    };

    private void Add(Dictionary<string, List<ChangeSpan>> result, string paragraphId, ChangeSpan span)
    {
        if (!result.TryGetValue(paragraphId, out var list)) result[paragraphId] = list = [];
        list.Add(span);
    }
}

public sealed class ChangeMarkup(IChangeSpans spans, IParagraphText text) : IChangeMarkup
{
    private readonly Dictionary<string, (TextParagraph Source, string Key, MarkedParagraph Result)> _cache = new(StringComparer.Ordinal);

    public MarkedParagraph? Mark(TextDocument document, ParagraphBlock block, TextParagraph content)
    {
        if (!document.Changes.ShowChanges || document.Changes.All.Count == 0) return null;
        var found = spans.Of(document, block.Id);
        if (found.Count == 0) return null;
        var key = Key(document, found);
        if (_cache.TryGetValue(block.Id, out var cached) && ReferenceEquals(cached.Source, content) && cached.Key == key) return cached.Result;
        var state = new ParagraphState(content, []);
        var length = content.Text.Length;
        var deletions = new List<InlineInsertion>();
        foreach (var span in found)
        {
            var start = Math.Clamp(span.Start, 0, length);
            var end = Math.Clamp(span.End, start, length);
            switch (span.Kind)
            {
                case ChangeKind.Insertion when end > start:
                    state = text.Map(state, start, end, run => run with { Underline = true, Color = span.Color });
                    break;
                case ChangeKind.Deletion when end > start:
                    state = text.Map(state, start, end, run => run with { Strikethrough = true, Color = span.Color });
                    break;
                case ChangeKind.Format when end > start:
                    state = text.Map(state, start, end, run => run with { Bold = true, Color = span.Color });
                    break;
                case ChangeKind.Deletion when document.Changes.Find(span.ChangeId) is Deletion { Removed.Count: > 0 } deletion:
                    deletions.AddRange(Words(Removed(deletion)).Select(word => new InlineInsertion(start, InlineKind.Deleted, span.ChangeId, word, 0, 0) { Ink = span.Color, Underlined = Nested(document, deletion) }));
                    break;
            }
        }
        var result = new MarkedParagraph(state.Content, deletions, key);
        _cache[block.Id] = (content, key, result);
        return result;
    }

    private string Key(TextDocument document, IReadOnlyList<ChangeSpan> found)
    {
        var builder = new StringBuilder();
        foreach (var span in found)
        {
            builder.Append(span.ChangeId).Append(':').Append(span.Start.ToString(CultureInfo.InvariantCulture)).Append('-')
                .Append(span.End.ToString(CultureInfo.InvariantCulture)).Append(':').Append(span.Color.ToString(CultureInfo.InvariantCulture));
            if (span.Kind == ChangeKind.Deletion && document.Changes.Find(span.ChangeId) is Deletion deletion)
                builder.Append(':').Append(RuntimeHelpers.GetHashCode(deletion).ToString(CultureInfo.InvariantCulture)).Append(Nested(document, deletion) ? ":n" : string.Empty);
            builder.Append(';');
        }
        return builder.ToString();
    }

    private bool Nested(TextDocument document, Deletion deletion) =>
        deletion.WithinInsertion is { } within && document.Changes.Find(within) is Insertion;

    private string Removed(Deletion deletion) =>
        string.Join("¶", deletion.Removed.Blocks.Select(block => block is ParagraphBlock paragraph ? paragraph.Text : "▦"));

    private IEnumerable<string> Words(string removed)
    {
        var start = 0;
        for (var i = 0; i < removed.Length; i++)
        {
            if (removed[i] != ' ' && removed[i] != '¶') continue;
            yield return removed[start..(i + 1)];
            start = i + 1;
        }
        if (start < removed.Length) yield return removed[start..];
    }
}
