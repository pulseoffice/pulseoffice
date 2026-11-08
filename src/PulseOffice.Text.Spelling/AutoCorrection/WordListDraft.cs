namespace PulseOffice.Text.Spelling;

public abstract class WordListKind
{
    public abstract string Normalize(string word);

    public EntryProblem Validate(string normalized)
    {
        if (normalized.Length == 0) return EntryProblem.Empty;
        return normalized.Any(char.IsWhiteSpace) || !Accepts(normalized) ? EntryProblem.Invalid : EntryProblem.None;
    }

    protected abstract bool Accepts(string normalized);
}

public sealed class AbbreviationKind : WordListKind
{
    public override string Normalize(string word)
    {
        var trimmed = (word ?? string.Empty).Trim();
        return trimmed.Length == 0 || trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }

    protected override bool Accepts(string normalized) => normalized.TrimEnd('.').Any(char.IsLetter);
}

public sealed class TwoCapitalsKind : WordListKind
{
    public override string Normalize(string word) => (word ?? string.Empty).Trim();

    protected override bool Accepts(string normalized) =>
        normalized.Length >= 3 && char.IsUpper(normalized[0]) && char.IsUpper(normalized[1]);
}

public sealed record WordEntry(string Word, EntryOrigin Origin);

public sealed class WordListDraft
{
    private readonly WordListKind _kind;
    private readonly IReadOnlyDictionary<string, EntryOrigin> _origins;
    private readonly HashSet<string> _defaults;
    private readonly HashSet<string> _words;

    public WordListDraft(WordListKind kind, IReadOnlyDictionary<string, EntryOrigin> defaults, WordListCustomization customization)
    {
        _kind = kind ?? throw new ArgumentNullException(nameof(kind));
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(customization);

        _origins = new Dictionary<string, EntryOrigin>(defaults, StringComparer.OrdinalIgnoreCase);
        _defaults = new HashSet<string>(defaults.Keys, StringComparer.OrdinalIgnoreCase);
        _words = new HashSet<string>(customization.ApplyTo(_defaults), StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<WordEntry> Entries =>
        [.. _words.Order(StringComparer.CurrentCultureIgnoreCase)
            .Select(word => new WordEntry(word, _origins.TryGetValue(word, out var origin) ? origin : EntryOrigin.User))];

    public bool IsAtDefaults => _words.SetEquals(_defaults);

    public EntryProblem Check(string word)
    {
        var normalized = _kind.Normalize(word);
        var problem = _kind.Validate(normalized);
        if (problem != EntryProblem.None) return problem;

        return _words.Contains(normalized) ? EntryProblem.Duplicate : EntryProblem.None;
    }

    public EntryProblem Add(string word)
    {
        var problem = Check(word);
        if (problem != EntryProblem.None) return problem;

        _words.Add(_kind.Normalize(word));
        Changed?.Invoke(this, EventArgs.Empty);
        return EntryProblem.None;
    }

    public int Merge(IEnumerable<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var merged = words.Count(word => Check(word) == EntryProblem.None && _words.Add(_kind.Normalize(word)));
        if (merged > 0) Changed?.Invoke(this, EventArgs.Empty);
        return merged;
    }

    public bool Remove(string word)
    {
        if (!_words.Remove(word)) return false;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void RestoreDefaults()
    {
        _words.Clear();
        _words.UnionWith(_defaults);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public WordListCustomization Customization() => new(
        [.. _words.Where(word => !_defaults.Contains(word)).Order(StringComparer.Ordinal)],
        [.. _defaults.Where(word => !_words.Contains(word)).Order(StringComparer.Ordinal)]);
}
