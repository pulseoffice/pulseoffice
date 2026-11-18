namespace PulseOffice.Presentations;

public interface IOutlineSlideMatching
{
    IReadOnlyList<int?> Match(IReadOnlyList<OutlineEntry> original, IReadOnlyList<OutlineEntry> edited);
}

public sealed class OutlineSlideMatching : IOutlineSlideMatching
{
    public IReadOnlyList<int?> Match(IReadOnlyList<OutlineEntry> original, IReadOnlyList<OutlineEntry> edited)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);

        var matched = new int?[edited.Count];
        var taken = new bool[original.Count];
        var previous = -1;
        for (var i = 0; i < edited.Count; i++)
        {
            var found = SameTitle(original, taken, edited[i].Title, previous);
            if (found < 0) continue;
            matched[i] = found;
            taken[found] = true;
            previous = found;
        }

        var anchor = -1;
        var gap = 0;
        for (var i = 0; i < edited.Count; i++)
        {
            if (matched[i] is { } kept)
            {
                anchor = kept;
                gap = 0;
                continue;
            }
            gap++;
            var candidate = anchor + gap;
            if (candidate < original.Count && !taken[candidate])
            {
                matched[i] = candidate;
                taken[candidate] = true;
            }
        }

        return matched;
    }

    private int SameTitle(IReadOnlyList<OutlineEntry> original, bool[] taken, string title, int previous)
    {
        var fallback = -1;
        for (var index = 0; index < original.Count; index++)
        {
            if (taken[index] || original[index].Title != title) continue;
            if (index > previous) return index;
            if (fallback < 0) fallback = index;
        }
        return fallback;
    }
}

public interface IOutlineBlockMove
{
    (string Text, int Line) Move(string text, int line, bool up);
}

public sealed class OutlineBlockMove(IDeckOutlineText outline) : IOutlineBlockMove
{
    public (string Text, int Line) Move(string text, int line, bool up)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (line < 0 || line >= lines.Count) return (text, line);

        var starts = Enumerable.Range(0, lines.Count).Where(index => IsTitle(lines[index])).ToList();
        if (starts.Count == 0 || starts[0] != 0) starts.Insert(0, 0);
        var block = starts.FindLastIndex(start => start <= line);
        var other = up ? block - 1 : block + 1;
        if (block < 0 || other < 0 || other >= starts.Count) return (text, line);

        var first = Math.Min(block, other);
        var range = Block(starts, first, lines.Count);
        var next = Block(starts, first + 1, lines.Count);
        var upper = lines.GetRange(range.Start, range.Count);
        var lower = lines.GetRange(next.Start, next.Count);
        var reordered = lines.Take(range.Start).Concat(lower).Concat(upper).Concat(lines.Skip(next.Start + next.Count)).ToList();

        var offset = line - starts[block];
        var moved = up ? range.Start + offset : range.Start + lower.Count + offset;
        return (string.Join("\n", reordered), moved);
    }

    private (int Start, int Count) Block(List<int> starts, int block, int total)
    {
        var start = starts[block];
        var end = block + 1 < starts.Count ? starts[block + 1] : total;
        return (start, end - start);
    }

    private bool IsTitle(string line) => outline.Parse(line) is [{ Body.Count: 0 }];
}
