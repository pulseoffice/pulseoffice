using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Queries;

public interface ILineOffsets
{
    double XOf(LineBox line, int offset);

    int OffsetAt(LineBox line, double x);

    int StartOf(LineBox line);

    (int Start, int End)? SpanBetween(LineBox line, double left, double right);

    int EndOf(LineBox line);
}

public sealed class LineOffsets(PulseOffice.Text.Rich.ITextClusters clusters) : ILineOffsets
{
    public double XOf(LineBox line, int offset)
    {
        var composed = line.Line;
        var runs = composed.Runs;
        var x = composed.Origin;
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            if (run.SourceLength == 0) continue;
            if (offset < run.SourceOffset) return line.Left + run.X;
            var end = run.SourceOffset + run.SourceLength;
            if (offset <= end)
            {
                var left = Edge(run, offset - run.SourceOffset);
                return line.Left + (offset == end && !run.RightToLeft ? AfterCitations(runs, index, left) : left);
            }
            x = run.RightToLeft ? run.X : run.X + run.Width;
        }
        return line.Left + x;
    }

    private double Edge(GlyphRun run, int count)
    {
        var advance = 0.0;
        for (var i = 0; i < count && i < run.Advances.Count; i++) advance += run.Advances[i];
        return run.RightToLeft ? run.X + run.Width - advance : run.X + advance;
    }

    private double AfterCitations(IReadOnlyList<GlyphRun> runs, int index, double left)
    {
        for (var next = index + 1; next < runs.Count && runs[next] is { SourceLength: 0, NoteId: not null } citation; next++)
            left = citation.X + citation.Width;
        return left;
    }

    public int OffsetAt(LineBox line, double x)
    {
        var local = x - line.Left;
        var last = EndOf(line);
        var text = line.Line.Runs.Where(run => run.SourceLength > 0).ToList();
        if (text.Count == 0) return line.Line.Start;
        if (!line.Line.Bidirectional) return Within(text, local, last) ?? Math.Max(line.Line.Start, last);
        var visual = text.OrderBy(run => run.X).ToList();
        if (local <= visual[0].X) return Math.Min(Leading(visual[0]), last);
        foreach (var run in visual)
        {
            if (local < run.X + run.Width) return Within([run], local, last) ?? Math.Min(Trailing(run), last);
        }
        return Math.Max(line.Line.Start, Math.Min(Trailing(visual[^1]), last));
    }

    private int Leading(GlyphRun run) => run.RightToLeft ? run.SourceOffset + run.SourceLength : run.SourceOffset;

    private int Trailing(GlyphRun run) => run.RightToLeft ? run.SourceOffset : run.SourceOffset + run.SourceLength;

    private int? Within(IReadOnlyList<GlyphRun> runs, double local, int last)
    {
        if (!runs[0].RightToLeft && local <= runs[0].X) return runs[0].SourceOffset;
        foreach (var run in runs)
        {
            var count = Math.Min(run.Advances.Count, run.Text.Length);
            var position = run.RightToLeft ? run.X + run.Width - local : local - run.X;
            var left = 0.0;
            for (var i = 0; i < count;)
            {
                var next = Math.Min(count, Math.Max(i + 1, clusters.Next(run.Text, i)));
                var width = 0.0;
                for (var j = i; j < next; j++) width += run.Advances[j];
                if (position < left + width / 2) return Math.Min(run.SourceOffset + i, last);
                left += width;
                i = next;
            }
            for (var i = count; i < run.Advances.Count; i++)
            {
                var advance = run.Advances[i];
                if (position < left + advance / 2) return Math.Min(run.SourceOffset + i, last);
                left += advance;
            }
            if (run.RightToLeft) return Math.Min(run.SourceOffset + count, last);
        }
        return null;
    }

    public int StartOf(LineBox line) => line.Line.Start;

    public (int Start, int End)? SpanBetween(LineBox line, double left, double right)
    {
        var from = Math.Min(left, right) - line.Left;
        var to = Math.Max(left, right) - line.Left;
        var last = EndOf(line);
        int? start = null;
        var end = 0;
        foreach (var run in line.Line.Runs.Where(run => run.SourceLength > 0))
        {
            var x = run.RightToLeft ? run.X + run.Width : run.X;
            for (var i = 0; i < run.Advances.Count && i < run.SourceLength; i++)
            {
                var advance = run.Advances[i];
                var offset = run.SourceOffset + i;
                var glyphLeft = run.RightToLeft ? x - advance : x;
                if (offset < last && Intersects(glyphLeft, glyphLeft + advance, from, to))
                {
                    start = Math.Min(start ?? offset, offset);
                    end = Math.Max(end, offset + 1);
                }
                x = run.RightToLeft ? x - advance : x + advance;
            }
        }
        return start is { } first ? (first, end) : null;
    }

    private bool Intersects(double glyphLeft, double glyphRight, double from, double to) =>
        from == to ? glyphLeft <= from && from < glyphRight : glyphLeft < to && glyphRight > from;

    public int EndOf(LineBox line)
    {
        var composed = line.Line;
        if (composed.Forced) return Math.Max(composed.Start, composed.End - 1);
        if (composed.IsLast) return composed.End;
        var run = composed.Runs.LastOrDefault(candidate => candidate.SourceLength > 0);
        var trailing = run is not null && run.SourceOffset + run.Text.Length == composed.End && run.Text.EndsWith(' ');
        return Math.Max(composed.Start, composed.End - (trailing ? 1 : 0));
    }
}
