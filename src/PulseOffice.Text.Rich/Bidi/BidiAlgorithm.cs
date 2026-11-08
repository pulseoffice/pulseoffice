namespace PulseOffice.Text.Rich.Bidi;

public interface IBidiAlgorithm
{
    BidiParagraph Resolve(string text, TextDirection? direction, IReadOnlyList<BidiEmbedding>? embeddings = null);
}

public sealed class BidiAlgorithm(IBidiClasses classes, IBidiMirroring mirroring) : IBidiAlgorithm
{
    public BidiAlgorithm() : this(new UnicodeBidiClasses(), new BidiMirroring())
    {
    }

    public BidiParagraph Resolve(string text, TextDirection? direction, IReadOnlyList<BidiEmbedding>? embeddings = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var (original, points) = Classify(text);
        var (kinds, codes, origin) = Extend(original, points, embeddings ?? []);
        var levels = new BidiResolution(kinds, codes, direction, mirroring).Resolve(out var baseLevel);
        var mapped = new byte[text.Length];
        for (var i = 0; i < origin.Length; i++)
        {
            if (origin[i] >= 0) mapped[origin[i]] = levels[i];
        }
        return new BidiParagraph(baseLevel, mapped, original);
    }

    private (BidiClass[] Classes, int[] Points) Classify(string text)
    {
        var result = new BidiClass[text.Length];
        var points = new int[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var pair = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
            var point = pair ? char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
            var kind = char.IsSurrogate(text[i]) && !pair ? BidiClass.L : classes.Of(point);
            result[i] = kind;
            points[i] = point;
            if (!pair) continue;
            result[i + 1] = kind;
            points[i + 1] = point;
            i++;
        }
        return (result, points);
    }

    private (BidiClass[] Kinds, int[] Codes, int[] Origin) Extend(BidiClass[] original, int[] points, IReadOnlyList<BidiEmbedding> embeddings)
    {
        if (embeddings.Count == 0) return ((BidiClass[])original.Clone(), points, [.. Enumerable.Range(0, original.Length)]);
        var opens = new Dictionary<int, List<BidiClass>>();
        var closes = new Dictionary<int, int>();
        foreach (var embedding in embeddings.Where(item => item.End > item.Start && item.Start >= 0 && item.End <= original.Length).OrderBy(item => item.Start))
        {
            if (!opens.TryGetValue(embedding.Start, out var list)) opens[embedding.Start] = list = [];
            list.Add(embedding.Direction == TextDirection.RightToLeft ? BidiClass.RLE : BidiClass.LRE);
            closes[embedding.End] = closes.GetValueOrDefault(embedding.End) + 1;
        }
        var kinds = new List<BidiClass>(original.Length + 2 * embeddings.Count);
        var codes = new List<int>(kinds.Capacity);
        var origin = new List<int>(kinds.Capacity);
        for (var i = 0; i <= original.Length; i++)
        {
            for (var close = closes.GetValueOrDefault(i); close > 0; close--)
            {
                kinds.Add(BidiClass.PDF);
                codes.Add(0x202C);
                origin.Add(-1);
            }
            if (opens.TryGetValue(i, out var opened))
            {
                foreach (var kind in opened)
                {
                    kinds.Add(kind);
                    codes.Add(kind == BidiClass.RLE ? 0x202B : 0x202A);
                    origin.Add(-1);
                }
            }
            if (i == original.Length) break;
            kinds.Add(original[i]);
            codes.Add(points[i]);
            origin.Add(i);
        }
        return ([.. kinds], [.. codes], [.. origin]);
    }
}
