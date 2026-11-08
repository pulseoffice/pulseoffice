namespace PulseOffice.Document.Tables;

public readonly record struct CellSpan(int Row, int Column, int RowSpan, int ColumnSpan)
{
    public int LastRow => Row + RowSpan - 1;

    public int LastColumn => Column + ColumnSpan - 1;

    public bool Overlaps(TableCellRange range) =>
        Row <= range.Bottom && LastRow >= range.Top && Column <= range.Right && LastColumn >= range.Left;
}

public interface ITableSpans
{
    (int Start, int Span) Inserted(int start, int span, int at);

    (int Start, int Span)? Deleted(int start, int span, int first, int last);

    TableCellRange Enclose(TableCellRange range, IEnumerable<CellSpan> spans);
}

public sealed class TableSpans : ITableSpans
{
    public (int Start, int Span) Inserted(int start, int span, int at) =>
        start >= at ? (start + 1, span) : start + span > at ? (start, span + 1) : (start, span);

    public (int Start, int Span)? Deleted(int start, int span, int first, int last)
    {
        var end = start + span - 1;
        var count = last - first + 1;

        if (start > last) return (start - count, span);
        if (end < first) return (start, span);

        var overlap = Math.Min(end, last) - Math.Max(start, first) + 1;
        var remaining = span - overlap;
        return remaining <= 0 ? null : (Math.Min(start, first), remaining);
    }

    public TableCellRange Enclose(TableCellRange range, IEnumerable<CellSpan> spans)
    {
        var all = spans.ToList();
        var current = range.Normalized;
        bool grown;
        do
        {
            grown = false;
            foreach (var span in all.Where(span => span.Overlaps(current)))
            {
                var next = new TableCellRange(
                    Math.Min(current.Top, span.Row), Math.Min(current.Left, span.Column),
                    Math.Max(current.Bottom, span.LastRow), Math.Max(current.Right, span.LastColumn));
                if (next == current) continue;
                current = next;
                grown = true;
            }
        }
        while (grown);
        return current;
    }
}
