using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;

namespace PulseOffice.Documents.Revisions;

public sealed record ChangeRegion(TrackedChange Change, DocumentPosition Start, DocumentPosition End)
{
    public bool IsPoint => Start == End;
}

public sealed class ChangeMap(IReadOnlyList<ChangeRegion> regions, IReadOnlyDictionary<string, int> order)
{
    public IReadOnlyList<ChangeRegion> Regions { get; } = regions;

    public int OrderOf(string paragraphId) => order.GetValueOrDefault(paragraphId, -1);

    public bool Knows(DocumentPosition position) => order.ContainsKey(position.ParagraphId);

    public int Compare(DocumentPosition first, DocumentPosition second)
    {
        var paragraph = order.GetValueOrDefault(first.ParagraphId, int.MaxValue).CompareTo(order.GetValueOrDefault(second.ParagraphId, int.MaxValue));
        return paragraph != 0 ? paragraph : first.Offset.CompareTo(second.Offset);
    }

    public ChangeRegion? Find(string changeId) => Regions.FirstOrDefault(region => region.Change.Id == changeId);

    public bool Covers(ChangeRegion region, DocumentPosition start, DocumentPosition end) =>
        Compare(region.Start, start) <= 0 && Compare(region.End, end) >= 0;

    public IReadOnlyList<ChangeRegion> Touching(DocumentRange range) =>
        [.. Regions.Where(region => range.IsCollapsed
            ? Compare(region.Start, range.Start) <= 0 && Compare(region.End, range.Start) >= 0
            : Compare(region.Start, range.End) <= 0 && Compare(region.End, range.Start) >= 0 && !(region.IsPoint && (region.Start == range.End && region.Start != range.Start)))];

    public ChangeRegion? After(DocumentPosition position) =>
        Regions.FirstOrDefault(region => Compare(region.Start, position) > 0);

    public ChangeRegion? Before(DocumentPosition position) =>
        Regions.LastOrDefault(region => Compare(region.Start, position) < 0);
}

public interface IChangeRegions
{
    ChangeMap Map(TextDocument document);
}

public sealed class ChangeRegions(IStoryCatalog stories) : IChangeRegions
{
    public ChangeMap Map(TextDocument document)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        var found = new List<(ChangeRegion Region, int Order)>();
        if (document.Changes.All.Count == 0) return new ChangeMap([], order);
        var open = new Dictionary<string, (DocumentPosition At, int Order)>(StringComparer.Ordinal);
        foreach (var paragraph in stories.Paragraphs(document))
        {
            var index = order.Count;
            order[paragraph.Id] = index;
            foreach (var mark in paragraph.Marks.OfType<ChangeMark>().OrderBy(mark => mark.Offset))
            {
                if (document.Changes.Find(mark.ChangeId) is not { } change) continue;
                var at = new DocumentPosition(paragraph.Id, mark.Offset);
                switch (mark.Edge)
                {
                    case MarkEdge.Start:
                        open[mark.ChangeId] = (at, index);
                        break;
                    case MarkEdge.End when open.Remove(mark.ChangeId, out var start):
                        found.Add((new ChangeRegion(change, start.At, at), start.Order));
                        break;
                    case MarkEdge.End:
                        break;
                    default:
                        found.Add((new ChangeRegion(change, at, at), index));
                        break;
                }
            }
        }
        foreach (var (id, start) in open)
        {
            if (document.Changes.Find(id) is { } change) found.Add((new ChangeRegion(change, start.At, start.At), start.Order));
        }
        return new ChangeMap([.. found.OrderBy(item => item.Order).ThenBy(item => item.Region.Start.Offset).Select(item => item.Region)], order);
    }
}
