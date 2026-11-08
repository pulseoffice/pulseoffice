using System.Runtime.CompilerServices;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.Controls;

public sealed class RevisionCachedContentControlCatalog(ILiveContentControlCatalog inner, IStoryCatalog stories) : IContentControlCatalog
{
    private sealed class Snapshot(long revision, ContentControlSpan[] spans, Dictionary<string, int> orders)
    {
        public long Revision { get; } = revision;

        public ContentControlSpan[] Spans { get; } = spans;

        public Dictionary<string, int> Orders { get; } = orders;
    }

    private readonly ConditionalWeakTable<TextDocument, Snapshot> _snapshots = [];

    private readonly Lock _gate = new();

    public IReadOnlyList<ContentControlSpan> All(TextDocument document) => Current(document).Spans;

    public ContentControlSpan? Find(TextDocument document, string controlId) =>
        Array.Find(Current(document).Spans, span => span.Id == controlId);

    public ContentControlSpan? At(TextDocument document, DocumentPosition position)
    {
        var snapshot = Current(document);
        if (snapshot.Spans.Length == 0) return null;
        var order = snapshot.Orders.GetValueOrDefault(position.ParagraphId, -1);
        return snapshot.Spans
            .Where(span => (span.StartOrder, span.Start.Offset).CompareTo((order, position.Offset)) <= 0 && (order, position.Offset).CompareTo((span.EndOrder, span.End.Offset)) <= 0)
            .OrderByDescending(span => (span.StartOrder, span.Start.Offset))
            .FirstOrDefault();
    }

    public int Order(TextDocument document, string paragraphId) =>
        Current(document).Orders.GetValueOrDefault(paragraphId, -1);

    public void Join(TextDocument document) => inner.Join(document);

    private Snapshot Current(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            if (_snapshots.TryGetValue(document, out var cached) && cached.Revision == document.Revision) return cached;
            var revision = document.Revision;
            var orders = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = 0;
            foreach (var paragraph in stories.Paragraphs(document))
            {
                order++;
                orders.TryAdd(paragraph.Id, order);
            }
            var fresh = new Snapshot(revision, [.. inner.All(document)], orders);
            _snapshots.AddOrUpdate(document, fresh);
            return fresh;
        }
    }
}
