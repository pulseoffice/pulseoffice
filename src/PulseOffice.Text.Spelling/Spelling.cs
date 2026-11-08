namespace PulseOffice.Text.Spelling;

public sealed record LanguageTag
{
    public required string Language { get; init; }

    public string? Region { get; init; }

    public string Canonical => Region is null ? Language : $"{Language}-{Region}";

    public static readonly LanguageTag None = new() { Language = string.Empty };

    public static LanguageTag Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return None;

        var parts = text.Trim().Split('.', '@')[0].Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return None;

        return new LanguageTag
        {
            Language = parts[0].ToLowerInvariant(),
            Region = parts.Skip(1)
                .TakeWhile(part => part.Length is 2 or 3 or 4 && part.All(char.IsAsciiLetterOrDigit))
                .FirstOrDefault(part => part.Length == 2 && part.All(char.IsAsciiLetter) || part.Length == 3 && part.All(char.IsAsciiDigit))
                ?.ToUpperInvariant(),
        };
    }

    public bool Matches(LanguageTag other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Language, other.Language, StringComparison.Ordinal)) return false;

        return Region is null || other.Region is null
            || string.Equals(Region, other.Region, StringComparison.Ordinal);
    }

    public override string ToString() => Canonical;
}

public sealed record SpellingMistake(WordSpan Word, IReadOnlyList<string> Suggestions);

public interface ISpellDictionary
{
    LanguageTag Language { get; }

    bool Check(string word);

    IReadOnlyList<string> Suggest(string word);
}

public interface ISpellDictionaryProvider
{
    IReadOnlyList<LanguageTag> Available { get; }

    ISpellDictionary? For(LanguageTag language);

    void Refresh();
}

public interface IUserDictionary
{
    IReadOnlyCollection<string> Words { get; }

    bool Knows(string word);

    void Add(string word);

    bool Remove(string word);

    event EventHandler? Changed;
}

public interface ISpellChecker
{
    bool HasDictionary(LanguageTag language);

    IReadOnlyList<SpellingMistake> Check(string text, LanguageTag language, int suggestions = 6);

    bool IsCorrect(string word, LanguageTag language);
}

public sealed class SpellChecker(
    ISpellDictionaryProvider dictionaries, IUserDictionary user, IWordTokenizer tokenizer) : ISpellChecker
{
    public const int MaximumSuggestedMistakes = 64;

    public bool HasDictionary(LanguageTag language) => dictionaries.For(language) is not null;

    public IReadOnlyList<SpellingMistake> Check(string text, LanguageTag language, int suggestions = 6)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (string.IsNullOrWhiteSpace(text)) return [];
        if (dictionaries.For(language) is not { } dictionary) return [];

        var mistakes = new List<SpellingMistake>();
        var suggested = 0;

        foreach (var word in tokenizer.Words(text))
        {
            if (Known(dictionary, word.Text)) continue;

            var offered = suggestions > 0 && suggested < MaximumSuggestedMistakes;
            if (offered) suggested++;

            mistakes.Add(new SpellingMistake(
                word,
                offered ? [.. dictionary.Suggest(word.Text.Replace('\u2019', '\'')).Take(suggestions)] : []));
        }

        return mistakes;
    }

    public bool IsCorrect(string word, LanguageTag language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return dictionaries.For(language) is not { } dictionary || Known(dictionary, word);
    }

    private bool Known(ISpellDictionary dictionary, string word)
    {
        if (user.Knows(word) || dictionary.Check(word)) return true;

        var straight = word.Replace('\u2019', '\'');

        return straight != word
            && (user.Knows(straight) || dictionary.Check(straight));
    }
}

public interface IAutoSpellSettings
{
    bool Enabled { get; set; }

    event EventHandler? Changed;
}

public sealed class AutoSpellSettings : IAutoSpellSettings
{
    public bool Enabled
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;
}
