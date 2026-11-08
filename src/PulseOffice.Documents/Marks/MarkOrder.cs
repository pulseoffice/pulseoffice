namespace PulseOffice.Documents.Marks;

public interface IMarkOrder
{
    IReadOnlyList<InlineMark> Ordered(IEnumerable<InlineMark> marks);
}

public sealed class MarkOrder : IMarkOrder
{
    public IReadOnlyList<InlineMark> Ordered(IEnumerable<InlineMark> marks)
    {
        var ordered = new List<InlineMark>();
        foreach (var group in marks.Select((mark, index) => (mark, index)).GroupBy(entry => entry.mark.Offset).OrderBy(group => group.Key))
        {
            var entries = group.OrderBy(entry => entry.index).Select(entry => entry.mark).ToList();
            var opened = entries.OfType<ContentControlMark>().Where(mark => mark.Edge == MarkEdge.Start).Select(mark => mark.ControlId).ToHashSet(StringComparer.Ordinal);
            bool Closing(InlineMark mark) => mark is ContentControlMark { Edge: MarkEdge.End } end && !opened.Contains(end.ControlId);
            ordered.AddRange(entries.Where(Closing));
            ordered.AddRange(entries.Where(mark => !Closing(mark)));
        }
        return ordered;
    }
}
