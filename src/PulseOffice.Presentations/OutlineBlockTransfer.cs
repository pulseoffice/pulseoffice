namespace PulseOffice.Presentations;

public sealed record OutlineBlockRange(int First, int Count);

public interface IOutlineBlockTransfer
{
    OutlineBlockRange? Covered(string text, int start, int length);

    int? InsertedAt(IReadOnlyList<OutlineEntry> before, IReadOnlyList<OutlineEntry> after, IReadOnlyList<OutlineEntry> inserted);

    int RemovalStart(string text, int start, int length);
}

public sealed class OutlineBlockTransfer(IDeckOutlineText outline) : IOutlineBlockTransfer
{
    public OutlineBlockRange? Covered(string text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (length <= 0 || start < 0 || start + length > text.Length) return null;
        var end = start + length;

        var blockStarts = new List<int>();
        var lineStart = 0;
        var first = true;
        while (lineStart <= text.Length)
        {
            var newline = text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? text.Length : newline;
            var line = text[lineStart..lineEnd].TrimEnd('\r');
            if (first || IsTitle(line)) blockStarts.Add(lineStart);
            first = false;
            if (newline < 0) break;
            lineStart = newline + 1;
        }

        var from = blockStarts.IndexOf(start);
        if (from < 0) return null;
        for (var next = from + 1; next <= blockStarts.Count; next++)
        {
            var boundary = next < blockStarts.Count ? blockStarts[next] : text.Length;
            if (boundary > end + 2) break;
            if (end == boundary || (next < blockStarts.Count && end == TrimNewline(text, boundary)))
                return new OutlineBlockRange(from, next - from);
        }
        return null;
    }

    public int? InsertedAt(IReadOnlyList<OutlineEntry> before, IReadOnlyList<OutlineEntry> after, IReadOnlyList<OutlineEntry> inserted)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(inserted);
        if (inserted.Count == 0 || after.Count != before.Count + inserted.Count) return null;
        for (var at = 0; at <= before.Count; at++)
        {
            var candidate = before.Take(at).Concat(inserted).Concat(before.Skip(at));
            if (candidate.Zip(after).All(pair => Same(pair.First, pair.Second))) return at;
        }
        return null;
    }

    public int RemovalStart(string text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start <= 0 || length <= 0 || start + length != text.Length) return start;
        return TrimNewline(text, start);
    }

    private int TrimNewline(string text, int boundary)
    {
        var end = boundary;
        if (end > 0 && text[end - 1] == '\n') end--;
        if (end > 0 && text[end - 1] == '\r') end--;
        return end;
    }

    private bool Same(OutlineEntry left, OutlineEntry right) =>
        left.Title == right.Title && left.Body.SequenceEqual(right.Body);

    private bool IsTitle(string line) => outline.Parse(line) is [{ Body.Count: 0 }];
}
