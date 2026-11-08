using System.Globalization;

namespace PulseOffice.Text.Spelling;

public interface IHyphenator
{
    LanguageTag Language { get; }

    IReadOnlyList<int> Positions(string word, int minimumEdge = 2);
}

public interface IHyphenationProvider
{
    IHyphenator? For(LanguageTag language);
}

public sealed class HyphenationPatterns : IHyphenator
{
    private readonly Dictionary<string, int[]> _patterns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _exceptions = new(StringComparer.OrdinalIgnoreCase);

    private HyphenationPatterns(LanguageTag language)
    {
        Language = language;
        Culture = CultureOf(language);
    }

    public LanguageTag Language { get; }

    private int _longestPattern;

    public int LeftMinimum { get; private set; }

    public int RightMinimum { get; private set; }

    private CultureInfo Culture { get; }

    public static HyphenationPatterns Parse(LanguageTag language, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(lines);

        var patterns = new HyphenationPatterns(language);
        var first = true;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('%') || line.StartsWith('#')) continue;

            if (first)
            {
                first = false;
                if (patterns.IsCharset(line)) continue;
            }

            patterns.Accept(line);
        }

        return patterns;
    }

    public IReadOnlyList<int> Positions(string word, int minimumEdge = 2)
    {
        var left = Math.Max(minimumEdge, LeftMinimum);
        var right = Math.Max(minimumEdge, RightMinimum);

        if (string.IsNullOrEmpty(word) || word.Length < left + right) return [];

        var lower = word.ToLower(Culture);
        if (lower.Length != word.Length) lower = word.ToLowerInvariant();

        if (_exceptions.TryGetValue(lower, out var known))
            return [.. known.Where(position => position >= left && position <= word.Length - right && Breakable(word, position))];

        var padded = "." + lower + ".";
        var levels = new int[padded.Length + 1];
        var lookup = _patterns.GetAlternateLookup<ReadOnlySpan<char>>();

        for (var start = 0; start < padded.Length; start++)
            for (var length = 1; length <= Math.Min(_longestPattern, padded.Length - start); length++)
            {
                if (!lookup.TryGetValue(padded.AsSpan(start, length), out var values)) continue;

                for (var i = 0; i < values.Length; i++)
                {
                    var index = start + i;
                    if (index < levels.Length) levels[index] = Math.Max(levels[index], values[i]);
                }
            }

        var positions = new List<int>();

        for (var i = 1; i < word.Length; i++)
        {
            if (i < left || i > word.Length - right) continue;
            if (levels[i + 1] % 2 == 1 && Breakable(word, i)) positions.Add(i);
        }

        return positions;
    }

    private bool Breakable(string word, int position) =>
        !char.IsLowSurrogate(word[position])
        && char.GetUnicodeCategory(word, position) is not (System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark
            or System.Globalization.UnicodeCategory.EnclosingMark);

    private void Accept(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var keyword = parts[0].ToUpperInvariant();

        switch (keyword)
        {
            case "LEFTHYPHENMIN":
                LeftMinimum = Number(parts);
                return;
            case "RIGHTHYPHENMIN":
                RightMinimum = Number(parts);
                return;
            case "COMPOUNDLEFTHYPHENMIN" or "COMPOUNDRIGHTHYPHENMIN" or "NOHYPHEN":
                return;
            case "NEXTLEVEL":
                _patterns.Clear();
                return;
        }

        if (line.Contains('/', StringComparison.Ordinal)) return;

        if (line.Contains('=', StringComparison.Ordinal))
        {
            var word = line.Replace("=", string.Empty, StringComparison.Ordinal).ToLower(Culture);
            _exceptions[word] = Breaks(line);
            return;
        }

        AddPattern(line.ToLower(Culture));
    }

    private int Number(string[] parts) =>
        parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Math.Max(0, value)
            : 0;

    private bool IsCharset(string line) =>
        !line.Any(char.IsWhiteSpace) && line != "NEXTLEVEL" && (line.Any(char.IsUpper)
        || line.StartsWith("utf", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("iso", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("koi", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("microsoft", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("cp", StringComparison.OrdinalIgnoreCase) && line.Skip(2).All(char.IsDigit));

    private CultureInfo CultureOf(LanguageTag language)
    {
        if (string.IsNullOrEmpty(language.Language)) return CultureInfo.InvariantCulture;

        try
        {
            return CultureInfo.GetCultureInfo(language.Canonical);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private void AddPattern(string pattern)
    {
        var letters = new System.Text.StringBuilder();
        var values = new List<int>();
        var pending = 0;

        foreach (var character in pattern)
        {
            if (char.IsAsciiDigit(character))
            {
                pending = character - '0';
                continue;
            }

            values.Add(pending);
            pending = 0;
            letters.Append(character);
        }

        values.Add(pending);

        _patterns[letters.ToString()] = [.. values];
        _longestPattern = Math.Max(_longestPattern, letters.Length);
    }

    private int[] Breaks(string line)
    {
        var positions = new List<int>();
        var index = 0;

        foreach (var character in line)
        {
            if (character == '=') positions.Add(index);
            else index++;
        }

        return [.. positions];
    }
}
