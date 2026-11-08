using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Lines;

public sealed record LineSpan(int Start, int End, int ContentEnd, double X0, IReadOnlyList<double> Widths)
{
    public bool Hyphenated { get; init; }

    public bool Forced { get; init; }

    public bool Broken { get; init; }

    public bool IsLast { get; init; }

    public IReadOnlyDictionary<int, TabLeader> Leaders { get; init; } = new Dictionary<int, TabLeader>();

    public double ContentWidth => Widths.Take(ContentEnd - Start).Sum();
}

public interface ILineBreaker
{
    IReadOnlyList<LineSpan> Break(ShapedParagraph paragraph, ParagraphFrame frame, double columnWidth);

    IReadOnlyList<LineSpan> Break(ShapedParagraph paragraph, ParagraphFrame frame, double columnWidth, int start, int firstIndex);

    LineSpan BreakLine(ShapedParagraph paragraph, ParagraphFrame frame, int start, int index, double available);
}

public sealed class LineBreaker(ITabResolver tabs) : ILineBreaker
{
    private const double Epsilon = 0.001;

    public IReadOnlyList<LineSpan> Break(ShapedParagraph paragraph, ParagraphFrame frame, double columnWidth) => Break(paragraph, frame, columnWidth, 0, 0);

    public IReadOnlyList<LineSpan> Break(ShapedParagraph paragraph, ParagraphFrame frame, double columnWidth, int start, int firstIndex)
    {
        var lines = new List<LineSpan>();
        var text = paragraph.Text;
        var available = columnWidth - frame.MarginLeft - frame.MarginRight;
        var origin = start;
        while (true)
        {
            var line = BreakOne(paragraph, frame, start, available, start == 0 && lines.Count == 0, firstIndex + lines.Count);
            lines.Add(line);
            start = line.End;
            if (start < text.Length) continue;
            if (line.Forced) lines.Add(new LineSpan(start, start, start, 0, []));
            break;
        }
        lines[^1] = lines[^1] with { IsLast = true };
        return origin > text.Length ? [] : lines;
    }

    public LineSpan BreakLine(ShapedParagraph paragraph, ParagraphFrame frame, int start, int index, double available)
    {
        if (start >= paragraph.Text.Length && start > 0) return new LineSpan(start, start, start, 0, []) { IsLast = true };
        var line = BreakOne(paragraph, frame, start, available, start == 0, index);
        return line.End >= paragraph.Text.Length && !line.Forced ? line with { IsLast = true } : line;
    }

    private LineSpan BreakOne(ShapedParagraph paragraph, ParagraphFrame frame, int start, double available, bool first, int index)
    {
        var text = paragraph.Text;
        var x0 = first ? frame.FirstLineIndent : 0;
        if (first && paragraph.Label is { } label)
        {
            var labelEnd = label.Position + label.Width;
            x0 = labelEnd <= 0 + Epsilon ? 0 : label.FollowedByTab ? tabs.Next(frame, labelEnd) : labelEnd;
        }
        if (paragraph.Drop is { } drop && drop.Covers(index)) x0 += drop.Indent;
        var widths = new List<double>();
        var leaders = new Dictionary<int, TabLeader>();
        var x = x0;
        var breakAt = -1;
        for (var i = start; i < text.Length; i++)
        {
            var character = text[i];
            if (character is '\n' or '\u2028')
            {
                widths.Add(0);
                return new LineSpan(start, i + 1, Trim(text, start, i), x0, widths) { Forced = true, Leaders = leaders };
            }
            double width;
            if (character == '\t')
            {
                var tab = tabs.Resolve(paragraph, frame, i, x, available);
                width = tab.Width;
                leaders[i] = tab.Leader;
            }
            else
            {
                width = paragraph.Advances[i];
            }
            if (character == ' ')
            {
                widths.Add(width);
                x += width;
                breakAt = i + 1;
                continue;
            }
            if (x + width > available + Epsilon && i > start)
            {
                var end = Choose(paragraph, start, i, breakAt, widths, x0, available, out var hyphenated);
                while (widths.Count < end - start) widths.Add(paragraph.Advances[start + widths.Count]);
                return new LineSpan(start, end, hyphenated ? end : Trim(text, start, end), x0, widths.Take(end - start).ToList())
                {
                    Hyphenated = hyphenated,
                    Broken = !hyphenated && breakAt <= start,
                    Leaders = leaders.Where(pair => pair.Key < end).ToDictionary(),
                };
            }
            widths.Add(width);
            x += width;
            if ((character == '-' && i > start && char.IsLetterOrDigit(text[i - 1])) || character == '\u200B') breakAt = i + 1;
            if (character == InlineInserter.Placeholder && paragraph.Inlines.TryGetValue(i, out var inline) && inline.Kind == InlineKind.Deleted) breakAt = i + 1;
        }
        return new LineSpan(start, text.Length, Trim(text, start, text.Length), x0, widths) { Leaders = leaders };
    }

    private int Choose(ShapedParagraph paragraph, int start, int overflow, int breakAt, List<double> widths, double x0, double available, out bool hyphenated)
    {
        var text = paragraph.Text;
        var wordStart = Math.Max(start, breakAt);
        for (var point = overflow; point > wordStart; point--)
        {
            var soft = text[point - 1] == '\u00AD';
            if (!soft && !paragraph.HyphenPoints.Contains(point)) continue;
            var reach = x0 + Sum(widths, point - start) + paragraph.RunAt(point - 1).HyphenWidth;
            if (reach > available + Epsilon) continue;
            hyphenated = true;
            return point;
        }
        hyphenated = false;
        if (breakAt > start) return breakAt;
        var end = Math.Max(start + 1, overflow);
        if (end < text.Length && char.IsLowSurrogate(text[end]) && char.IsHighSurrogate(text[end - 1])) end = end - 1 > start ? end - 1 : end + 1;
        return end;
    }

    private double Sum(List<double> widths, int count)
    {
        var total = 0.0;
        for (var i = 0; i < count && i < widths.Count; i++) total += widths[i];
        return total;
    }

    private int Trim(string text, int start, int end)
    {
        while (end > start && text[end - 1] == ' ') end--;
        return end;
    }
}
