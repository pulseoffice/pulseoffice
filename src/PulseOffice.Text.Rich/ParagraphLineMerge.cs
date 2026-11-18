namespace PulseOffice.Text.Rich;

public interface IParagraphLineMerge
{
    RichText Merge(RichText text, IReadOnlyList<string> lines);
}

public sealed class ParagraphLineMerge(ITextEditSplice splice) : IParagraphLineMerge
{
    public RichText Merge(RichText text, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(lines);
        var old = text.Paragraphs;
        if (old.Count == lines.Count && old.Select((paragraph, index) => paragraph.Text == lines[index]).All(same => same)) return text;
        if (old.Count == 0 || lines.Count == 0) return splice.Apply(text, string.Join("\n", lines));

        var anchors = Anchors(old, lines);
        var merged = new List<TextParagraph>(lines.Count);
        int oldAt = 0, newAt = 0;
        foreach (var (oldIndex, newIndex) in anchors.Append((old.Count, lines.Count)))
        {
            var oldGap = oldIndex - oldAt;
            var newGap = newIndex - newAt;
            for (var i = 0; i < newGap; i++)
            {
                var source = i < oldGap ? old[oldAt + i]
                    : merged.Count > 0 ? merged[^1]
                    : old[Math.Min(oldAt + i, old.Count - 1)];
                merged.Add(Retyped(text, source, lines[newAt + i]));
            }
            if (oldIndex < old.Count) merged.Add(old[oldIndex]);
            oldAt = oldIndex + 1;
            newAt = newIndex + 1;
        }
        return text with { Paragraphs = merged };
    }

    private TextParagraph Retyped(RichText text, TextParagraph source, string line)
    {
        if (source.Text == line) return source;
        var spliced = splice.Apply(text with { Paragraphs = [source] }, line).Paragraphs;
        return spliced.Count == 1 ? spliced[0] : source with { Text = line, Runs = [] };
    }

    private List<(int Old, int New)> Anchors(IReadOnlyList<TextParagraph> old, IReadOnlyList<string> lines)
    {
        var lengths = new int[old.Count + 1, lines.Count + 1];
        for (var i = old.Count - 1; i >= 0; i--)
            for (var j = lines.Count - 1; j >= 0; j--)
                lengths[i, j] = old[i].Text == lines[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

        var anchors = new List<(int, int)>();
        int a = 0, b = 0;
        while (a < old.Count && b < lines.Count)
        {
            if (old[a].Text == lines[b]) { anchors.Add((a, b)); a++; b++; }
            else if (lengths[a + 1, b] >= lengths[a, b + 1]) a++;
            else b++;
        }
        return anchors;
    }
}
