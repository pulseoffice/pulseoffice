namespace PulseOffice.Text.Spelling;

public sealed record WordListCustomization(IReadOnlyCollection<string> Added, IReadOnlyCollection<string> Removed)
{
    public static readonly WordListCustomization None = new([], []);

    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0;

    public IReadOnlySet<string> ApplyTo(IEnumerable<string> defaults)
    {
        var words = new HashSet<string>(defaults, StringComparer.OrdinalIgnoreCase);
        words.ExceptWith(Removed);
        words.UnionWith(Added);
        return words;
    }
}

public sealed record LanguageCustomization(
    IReadOnlyDictionary<string, string> Replacements,
    IReadOnlyCollection<string> RemovedReplacements,
    WordListCustomization Abbreviations,
    WordListCustomization TwoCapitalWords)
{
    public static readonly LanguageCustomization None = new(
        new Dictionary<string, string>(), [], WordListCustomization.None, WordListCustomization.None);

    public bool IsEmpty =>
        Replacements.Count == 0 && RemovedReplacements.Count == 0 && Abbreviations.IsEmpty && TwoCapitalWords.IsEmpty;

    public IReadOnlyDictionary<string, string> ApplyTo(IReadOnlyDictionary<string, string> defaults)
    {
        var entries = new Dictionary<string, string>(defaults, StringComparer.OrdinalIgnoreCase);
        foreach (var removed in RemovedReplacements) entries.Remove(removed);
        foreach (var (from, to) in Replacements) entries[from] = to;
        return entries;
    }
}

public sealed record AutoCorrectProfile(
    bool Enabled,
    AutoCorrectOptions Options,
    IReadOnlyDictionary<string, LanguageCustomization> Languages)
{
    public static readonly AutoCorrectProfile Default = new(true, AutoCorrectOptions.Default, new Dictionary<string, LanguageCustomization>());

    public LanguageCustomization For(string language) =>
        Languages.TryGetValue(language, out var customization) ? customization : LanguageCustomization.None;
}
