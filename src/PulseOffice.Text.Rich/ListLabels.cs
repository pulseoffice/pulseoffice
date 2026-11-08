namespace PulseOffice.Text.Rich;

public interface IListMarker
{
    bool Marks(TextParagraph paragraph);

    string Label(ListCounters counters, int level, TextParagraph paragraph);
}

public sealed class ListCounters
{
    private readonly int[] _counts = new int[9];

    public int Clamp(int level) => Math.Clamp(level, 0, _counts.Length - 1);

    public void Reset() => Array.Clear(_counts);

    public void ResetBelow(int level)
    {
        for (var deeper = level + 1; deeper < _counts.Length; deeper++) _counts[deeper] = 0;
    }

    public void Clear(int level) => _counts[level] = 0;

    public int Next(int level) => ++_counts[level];
}

public sealed class NumberMarker(IListNumberFormats formats) : IListMarker
{
    public NumberMarker() : this(new ListNumberFormats())
    {
    }

    public bool Marks(TextParagraph paragraph) => paragraph.Numbered;

    public string Label(ListCounters counters, int level, TextParagraph paragraph) =>
        formats.Label(paragraph.NumberFormat, counters.Next(level));
}

public sealed class BulletGlyphs
{
    private readonly IReadOnlyDictionary<char, string> _wingdings = new Dictionary<char, string>
    {
        ['§'] = "▪", ['Ø'] = "➢", ['ü'] = "✓", ['q'] = "❑", ['v'] = "❖", ['n'] = "■", ['l'] = "●", ['o'] = "□", ['ß'] = "◆", ['à'] = "➔", ['Ú'] = "➢",
    };

    private readonly IReadOnlyDictionary<char, string> _symbol = new Dictionary<char, string>
    {
        ['·'] = "•", ['-'] = "–", ['®'] = "→", ['Þ'] = "⇒", ['¨'] = "♣",
    };

    public string Shown(BulletSymbol symbol, string fallback)
    {
        if (symbol.Character.Length == 0) return fallback;
        var first = symbol.Character[0];
        var font = symbol.Font ?? string.Empty;
        if (font.StartsWith("Wingdings", StringComparison.OrdinalIgnoreCase)) return _wingdings.GetValueOrDefault(first, fallback);
        if (font.Equals("Symbol", StringComparison.OrdinalIgnoreCase)) return _symbol.GetValueOrDefault(first, fallback);
        return first is >= '\uE000' and <= '\uF8FF' ? fallback : symbol.Character;
    }
}

public sealed class BulletMarker(BulletGlyphs glyphs) : IListMarker
{
    public const string Symbol = "•";

    public BulletMarker() : this(new BulletGlyphs())
    {
    }

    public bool Marks(TextParagraph paragraph) => paragraph.Bullet;

    public string Label(ListCounters counters, int level, TextParagraph paragraph)
    {
        counters.Clear(level);
        return paragraph.BulletSymbol is { } symbol ? glyphs.Shown(symbol, Symbol) : Symbol;
    }
}

public interface IListLabels
{
    IReadOnlyList<string> For(IReadOnlyList<TextParagraph> paragraphs);
}

public sealed class ListLabels(IEnumerable<IListMarker> markers) : IListLabels
{
    private readonly IReadOnlyList<IListMarker> _markers = [.. markers];

    public ListLabels() : this([new NumberMarker(), new BulletMarker()])
    {
    }

    public IReadOnlyList<string> For(IReadOnlyList<TextParagraph> paragraphs)
    {
        var counters = new ListCounters();
        var labels = new string[paragraphs.Count];

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var marker = _markers.FirstOrDefault(candidate => candidate.Marks(paragraph));
            if (marker is null)
            {
                counters.Reset();
                labels[i] = string.Empty;
                continue;
            }

            var level = counters.Clamp(paragraph.BulletLevel);
            counters.ResetBelow(level);
            labels[i] = marker.Label(counters, level, paragraph);
        }

        return labels;
    }
}
