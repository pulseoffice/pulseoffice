namespace PulseOffice.Text.Spelling;

public enum EntryOrigin
{
    BuiltIn,
    Bundled,
    Modified,
    User,
}

public enum EntryProblem
{
    None,
    Empty,
    Same,
    Duplicate,
    Invalid,
}

public sealed record ReplacementEntry(string From, string To, EntryOrigin Origin);

public sealed class ReplacementListDraft
{
    private readonly IReadOnlyDictionary<string, DefaultReplacement> _origins;
    private readonly IReadOnlyDictionary<string, string> _defaults;
    private readonly Dictionary<string, string> _entries;

    public ReplacementListDraft(IReadOnlyDictionary<string, DefaultReplacement> defaults, LanguageCustomization customization)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(customization);

        _origins = defaults;
        _defaults = defaults.ToDictionary(pair => pair.Key, pair => pair.Value.To, StringComparer.OrdinalIgnoreCase);
        _entries = new Dictionary<string, string>(customization.ApplyTo(_defaults), StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ReplacementEntry> Entries =>
        [.. _entries.Select(pair => new ReplacementEntry(pair.Key, pair.Value, OriginOf(pair.Key, pair.Value)))];

    public int Count => _entries.Count;

    public bool IsAtDefaults =>
        _entries.Count == _defaults.Count && _defaults.All(pair => _entries.TryGetValue(pair.Key, out var value) && value == pair.Value);

    public EntryProblem Check(string from, string to, string? replacing = null)
    {
        var key = (from ?? string.Empty).Trim();
        var value = (to ?? string.Empty).Trim();

        if (key.Length == 0 || value.Length == 0) return EntryProblem.Empty;
        if (key.Any(char.IsWhiteSpace)) return EntryProblem.Invalid;
        if (string.Equals(key, value, StringComparison.Ordinal)) return EntryProblem.Same;

        var renaming = replacing is null || !string.Equals(replacing, key, StringComparison.OrdinalIgnoreCase);
        return renaming && _entries.ContainsKey(key) ? EntryProblem.Duplicate : EntryProblem.None;
    }

    public EntryProblem Add(string from, string to) => Update(null, from, to);

    public EntryProblem Update(string? original, string from, string to)
    {
        var problem = Check(from, to, original);
        if (problem != EntryProblem.None) return problem;

        if (original is not null) _entries.Remove(original);
        _entries[from.Trim()] = to.Trim();
        Changed?.Invoke(this, EventArgs.Empty);
        return EntryProblem.None;
    }

    public int Merge(IReadOnlyDictionary<string, string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var merged = 0;
        foreach (var (from, to) in entries)
        {
            if (Check(from, to, from) != EntryProblem.None) continue;
            if (_entries.TryGetValue(from.Trim(), out var current) && current == to.Trim()) continue;

            _entries[from.Trim()] = to.Trim();
            merged++;
        }

        if (merged > 0) Changed?.Invoke(this, EventArgs.Empty);
        return merged;
    }

    public bool Remove(string from)
    {
        if (!_entries.Remove(from)) return false;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void RestoreDefaults()
    {
        _entries.Clear();
        foreach (var (from, to) in _defaults) _entries[from] = to;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public (IReadOnlyDictionary<string, string> Replacements, IReadOnlyCollection<string> Removed) Customization() =>
        (
            _entries.Where(pair => OriginOf(pair.Key, pair.Value) is EntryOrigin.Modified or EntryOrigin.User)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            [.. _defaults.Keys.Where(key => !_entries.ContainsKey(key)).Order(StringComparer.Ordinal)]
        );

    private EntryOrigin OriginOf(string from, string to) =>
        !_origins.TryGetValue(from, out var factory)
            ? EntryOrigin.User
            : factory.To == to ? factory.Origin : EntryOrigin.Modified;
}
