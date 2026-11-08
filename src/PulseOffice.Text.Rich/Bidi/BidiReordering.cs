namespace PulseOffice.Text.Rich.Bidi;

public interface IBidiReordering
{
    byte[] LineLevels(BidiParagraph paragraph, int start, int end);

    int[] VisualOrder(IReadOnlyList<byte> levels);
}

public sealed class BidiReordering : IBidiReordering
{
    public byte[] LineLevels(BidiParagraph paragraph, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        start = Math.Clamp(start, 0, paragraph.Levels.Length);
        end = Math.Clamp(end, start, paragraph.Levels.Length);
        var levels = paragraph.Levels[start..end];
        var trailing = true;
        for (var i = end - 1; i >= start; i--)
        {
            var kind = paragraph.Classes[i];
            if (kind is BidiClass.S or BidiClass.B)
            {
                levels[i - start] = paragraph.BaseLevel;
                trailing = true;
            }
            else if (trailing && Whitespace(kind)) levels[i - start] = paragraph.BaseLevel;
            else trailing = false;
        }
        return levels;
    }

    public int[] VisualOrder(IReadOnlyList<byte> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        var order = Enumerable.Range(0, levels.Count).ToArray();
        if (levels.Count == 0) return order;
        var highest = levels.Max();
        var lowestOdd = levels.Where(level => (level & 1) == 1).DefaultIfEmpty(byte.MaxValue).Min();
        for (var level = highest; level >= lowestOdd && level > 0; level--)
        {
            for (var i = 0; i < order.Length; i++)
            {
                if (levels[order[i]] < level) continue;
                var end = i;
                while (end < order.Length && levels[order[end]] >= level) end++;
                Array.Reverse(order, i, end - i);
                i = end;
            }
        }
        return order;
    }

    private bool Whitespace(BidiClass kind) =>
        kind is BidiClass.WS or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI or BidiClass.BN
            or BidiClass.LRE or BidiClass.RLE or BidiClass.LRO or BidiClass.RLO or BidiClass.PDF;
}
