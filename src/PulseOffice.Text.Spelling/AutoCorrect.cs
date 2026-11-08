using System.Globalization;

namespace PulseOffice.Text.Spelling;

public sealed record AutoCorrectOptions
{
    public bool Replacements { get; init; } = true;

    public bool CapitaliseFirstLetter { get; init; } = true;

    public bool FixTwoInitialCapitals { get; init; } = true;

    public bool OrdinalSuperscript { get; init; }

    public bool SmartQuotes { get; init; } = true;

    public bool Dashes { get; init; } = true;

    public bool AutomaticLists { get; init; } = true;

    public bool ApplyBorders { get; init; } = true;

    public bool WordCompletion { get; init; } = true;

    public string? Language { get; init; }

    public static readonly AutoCorrectOptions Default = new();
}

public interface IAutoCorrectTable
{
    IReadOnlyDictionary<string, string> EntriesFor(string language);

    IReadOnlySet<string> AbbreviationsFor(string language);

    IReadOnlySet<string> TwoCapitalWordsFor(string language);

    string Resolve(string? language);

    void Customize(IReadOnlyDictionary<string, LanguageCustomization> languages);
}

public sealed class AutoCorrectTable(IAutoCorrectDefaults defaults) : IAutoCorrectTable
{
    private readonly Lock _gate = new();
    private IReadOnlyDictionary<string, LanguageCustomization> _languages = new Dictionary<string, LanguageCustomization>();
    private Dictionary<string, LanguageTable> _cache = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> EntriesFor(string language) => TableOf(language).Entries;

    public IReadOnlySet<string> AbbreviationsFor(string language) => TableOf(language).Abbreviations;

    public IReadOnlySet<string> TwoCapitalWordsFor(string language) => TableOf(language).TwoCapitals;

    public string Resolve(string? language) => defaults.Resolve(language);

    public void Customize(IReadOnlyDictionary<string, LanguageCustomization> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);

        lock (_gate)
        {
            _languages = languages;
            _cache = new Dictionary<string, LanguageTable>(StringComparer.Ordinal);
        }
    }

    private LanguageTable TableOf(string language)
    {
        var resolved = defaults.Resolve(language);

        lock (_gate)
        {
            if (_cache.TryGetValue(resolved, out var cached)) return cached;

            var customization = _languages.TryGetValue(resolved, out var found) ? found : LanguageCustomization.None;
            var set = defaults.For(resolved);
            var table = new LanguageTable(
                customization.ApplyTo(set.ReplacementTexts),
                customization.Abbreviations.ApplyTo(set.Abbreviations.Keys),
                customization.TwoCapitalWords.ApplyTo(set.TwoCapitalWords.Keys));

            _cache[resolved] = table;
            return table;
        }
    }

    private sealed record LanguageTable(
        IReadOnlyDictionary<string, string> Entries,
        IReadOnlySet<string> Abbreviations,
        IReadOnlySet<string> TwoCapitals);
}

public interface IAutoCorrect
{
    string Apply(string text, AutoCorrectOptions options);
}

public sealed class AutoCorrect : IAutoCorrect
{
    private readonly IAutoCorrectTable _table;
    private readonly IReadOnlyList<AutoCorrectRule> _rules;

    public AutoCorrect(IAutoCorrectTable table, IWordTokenizer tokenizer)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _rules =
        [
            new ReplacementRule(table, tokenizer),
            new TwoInitialsRule(table, tokenizer),
            new SentenceCapitalRule(table),
            new DashRule(),
            new SmartQuoteRule(),
            new OrdinalRule(),
        ];
    }

    public string Apply(string text, AutoCorrectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrEmpty(text)) return text;

        var language = _table.Resolve(options.Language);

        return _rules
            .Where(rule => rule.Enabled(options))
            .Aggregate(text, (current, rule) => rule.Apply(current, language));
    }
}

internal abstract class AutoCorrectRule
{
    public abstract bool Enabled(AutoCorrectOptions options);

    public abstract string Apply(string text, string language);
}

internal sealed class ReplacementRule(IAutoCorrectTable table, IWordTokenizer tokenizer) : AutoCorrectRule
{
    public override bool Enabled(AutoCorrectOptions options) => options.Replacements;

    public override string Apply(string text, string language)
    {
        var entries = table.EntriesFor(language);
        var words = tokenizer.Words(text);
        if (words.Count == 0) return Symbols(text, entries);

        var builder = new System.Text.StringBuilder(text.Length);
        var cursor = 0;

        foreach (var word in words)
        {
            builder.Append(text, cursor, word.Start - cursor);
            builder.Append(entries.TryGetValue(word.Text, out var replacement) ? Shaped(word.Text, replacement) : word.Text);
            cursor = word.End;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return Symbols(builder.ToString(), entries);
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyDictionary<string, string>, IReadOnlyList<SymbolReplacement>> _symbols = [];

    private string Symbols(string text, IReadOnlyDictionary<string, string> entries) =>
        _symbols.GetValue(entries, SymbolsOf)
            .Where(symbol => symbol.Occurs(text))
            .Aggregate(text, (current, symbol) => symbol.Apply(current));

    private IReadOnlyList<SymbolReplacement> SymbolsOf(IReadOnlyDictionary<string, string> entries) =>
        [.. entries
            .Where(entry => !entry.Key.All(character => char.IsLetterOrDigit(character) || character == '\''))
            .OrderByDescending(entry => entry.Key.Length)
            .Select(entry => new SymbolReplacement(entry.Key, entry.Value))];

    private string Shaped(string original, string replacement) =>
        char.IsUpper(original[0]) && replacement.Length > 0 && char.IsLower(replacement[0])
            ? char.ToUpper(replacement[0], CultureInfo.CurrentCulture) + replacement[1..]
            : replacement;
}

internal sealed class SymbolReplacement(string key, string value)
{
    private readonly LinkAreas _links = new();

    public bool Occurs(string text) => text.Contains(key, StringComparison.OrdinalIgnoreCase);

    public string Apply(string text)
    {
        var protectedAreas = _links.Of(text);
        var builder = new System.Text.StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var found = text.IndexOf(key, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0) break;

            builder.Append(text, index, found - index);

            if (Accepts(text, found) && !protectedAreas.Any(area => found < area.End && area.Start < found + key.Length))
            {
                builder.Append(value);
                index = found + key.Length;
            }
            else
            {
                builder.Append(text[found]);
                index = found + 1;
            }
        }

        builder.Append(text, index, text.Length - index);
        return builder.ToString();
    }

    private bool Accepts(string text, int start)
    {
        var before = start > 0 ? text[start - 1] : (char?)null;
        var afterIndex = start + key.Length;
        var after = afterIndex < text.Length ? text[afterIndex] : (char?)null;

        if (before == key[0] || after == key[^1]) return false;

        if (key.Any(char.IsLetter))
            return !IsWordCharacter(before) && !IsWordCharacter(after);

        if (key.All(character => character == '-'))
            return IsJoin(before) && IsJoin(after);

        return true;
    }

    private static bool IsWordCharacter(char? character) => character is { } value && char.IsLetterOrDigit(value);

    private static bool IsJoin(char? character) =>
        character is null || char.IsWhiteSpace(character.Value) || char.IsLetterOrDigit(character.Value);
}

internal sealed class LinkAreas
{
    public IReadOnlyList<WordSpan> Of(string text)
    {
        var areas = new List<WordSpan>();
        var index = 0;

        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index])) index++;
            if (index == start) continue;

            var run = text[start..index];
            if (run.Contains('@', StringComparison.Ordinal)
                || run.Contains("://", StringComparison.Ordinal)
                || run.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                areas.Add(new WordSpan(start, run.Length, run));
        }

        return areas;
    }
}

internal sealed class TwoInitialsRule(IAutoCorrectTable table, IWordTokenizer tokenizer) : AutoCorrectRule
{
    public override bool Enabled(AutoCorrectOptions options) => options.FixTwoInitialCapitals;

    public override string Apply(string text, string language)
    {
        var allowed = table.TwoCapitalWordsFor(language);
        var builder = new System.Text.StringBuilder(text);

        foreach (var word in tokenizer.Words(text))
        {
            if (word.Length < 3) continue;

            var value = word.Text;
            if (!char.IsUpper(value[0]) || !char.IsUpper(value[1]) || !char.IsLower(value[2])) continue;
            if (value.Skip(2).Any(char.IsUpper)) continue;
            if (allowed.Contains(value)) continue;

            builder[word.Start + 1] = char.ToLower(value[1], CultureInfo.CurrentCulture);
        }

        return builder.ToString();
    }
}

internal sealed class SentenceCapitalRule(IAutoCorrectTable table) : AutoCorrectRule
{
    private const string Openers = "\"'\u201C\u2018\u201E\u00AB([{\u00BF\u00A1";

    private const string Closers = "\"'\u201D\u2019\u00BB)]}";

    public override bool Enabled(AutoCorrectOptions options) => options.CapitaliseFirstLetter;

    public override string Apply(string text, string language)
    {
        var abbreviations = table.AbbreviationsFor(language);
        var builder = new System.Text.StringBuilder(text);
        var start = true;
        var afterStop = false;

        for (var i = 0; i < builder.Length; i++)
        {
            var character = builder[i];

            if (start && char.IsLetter(character))
            {
                builder[i] = char.ToUpper(character, CultureInfo.CurrentCulture);
                start = false;
                continue;
            }

            if (start && Openers.Contains(character)) continue;

            if (afterStop && Closers.Contains(character)) continue;

            if (character is '.' or '!' or '?')
            {
                afterStop = character != '.' || !abbreviations.Contains(WordEndingAt(builder, i));
                start = false;
            }
            else if (char.IsWhiteSpace(character))
            {
                start |= afterStop;
                afterStop = false;
            }
            else
            {
                afterStop = false;
                start = false;
            }
        }

        return builder.ToString();
    }

    private string WordEndingAt(System.Text.StringBuilder builder, int end)
    {
        var start = end;
        while (start > 0 && !char.IsWhiteSpace(builder[start - 1])) start--;
        while (start < end && Openers.Contains(builder[start])) start++;

        return builder.ToString(start, end - start + 1);
    }
}

internal sealed class DashRule : AutoCorrectRule
{
    public override bool Enabled(AutoCorrectOptions options) => options.Dashes;

    public override string Apply(string text, string language) => text.Replace(" - ", " – ", StringComparison.Ordinal);
}

internal sealed class SmartQuoteRule : AutoCorrectRule
{
    public override bool Enabled(AutoCorrectOptions options) => options.SmartQuotes;

    public override string Apply(string text, string language)
    {
        var builder = new System.Text.StringBuilder(text);

        for (var i = 0; i < builder.Length; i++)
            if (builder[i] == '"')
                builder[i] = Opens(i == 0 ? null : builder[i - 1]) ? '“' : '”';

        return builder.ToString();
    }

    private static bool Opens(char? previous)
    {
        if (previous is not { } value) return true;
        if (char.IsWhiteSpace(value)) return true;

        return char.GetUnicodeCategory(value) is System.Globalization.UnicodeCategory.OpenPunctuation
            or System.Globalization.UnicodeCategory.InitialQuotePunctuation
            or System.Globalization.UnicodeCategory.DashPunctuation
            || value is '“' or '‘' or '/' or '\u00AB';
    }
}

internal sealed class OrdinalRule : AutoCorrectRule
{
    private static readonly System.Text.RegularExpressions.Regex Ordinal = new(
        @"(?<![\p{L}\p{N}])(\d+)(st|nd|rd|th|o|a)(?![\p{L}\p{N}])",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    private readonly IReadOnlyDictionary<string, string> _marks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["st"] = "\u02E2\u1D57",
        ["nd"] = "\u207F\u1D48",
        ["rd"] = "\u02B3\u1D48",
        ["th"] = "\u1D57\u02B0",
        ["o"] = "\u00BA",
        ["a"] = "\u00AA",
    };

    private readonly LinkAreas _links = new();

    public override bool Enabled(AutoCorrectOptions options) => options.OrdinalSuperscript;

    public override string Apply(string text, string language)
    {
        var protectedAreas = _links.Of(text);

        try
        {
            return Ordinal.Replace(text, match =>
                protectedAreas.Any(area => match.Index < area.End && area.Start < match.Index + match.Length)
                || !Fits(match.Groups[1].Value, match.Groups[2].Value, language)
                    ? match.Value
                    : match.Groups[1].Value + _marks[match.Groups[2].Value]);
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return text;
        }
    }

    private readonly IReadOnlySet<string> _genderedOrdinals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "it", "es", "pt", "gl", "ca" };

    private bool Fits(string number, string suffix, string language)
    {
        if (suffix is "o" or "a") return _genderedOrdinals.Contains(language.Split('-')[0]);

        var lastTwo = number.Length > 1 ? number[^2] : '0';
        var expected = lastTwo == '1' ? "th" : number[^1] switch
        {
            '1' => "st",
            '2' => "nd",
            '3' => "rd",
            _ => "th",
        };

        return suffix == expected;
    }
}

public interface IAutoCorrectSettings
{
    bool Enabled { get; set; }

    AutoCorrectOptions Options { get; set; }
}

public sealed class AutoCorrectSettings : IAutoCorrectSettings
{
    public bool Enabled { get; set; } = true;

    public AutoCorrectOptions Options { get; set; } = AutoCorrectOptions.Default;
}
