using System.Globalization;

namespace PulseOffice.Text.Spelling;

public enum ReplacementSort
{
    FromAscending,
    FromDescending,
    ToAscending,
    ToDescending,
}

public interface IReplacementQuery
{
    IReadOnlyList<ReplacementEntry> Apply(IEnumerable<ReplacementEntry> entries, string? text, ReplacementSort sort);
}

public sealed class ReplacementQuery : IReplacementQuery
{
    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public IReadOnlyList<ReplacementEntry> Apply(IEnumerable<ReplacementEntry> entries, string? text, ReplacementSort sort)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var query = text?.Trim() ?? string.Empty;
        var matching = query.Length == 0
            ? entries
            : entries.Where(entry => compare.IndexOf(entry.From, query, Loose) >= 0 || compare.IndexOf(entry.To, query, Loose) >= 0);

        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);

        return sort switch
        {
            ReplacementSort.FromDescending => [.. matching.OrderByDescending(entry => entry.From, comparer)],
            ReplacementSort.ToAscending => [.. matching.OrderBy(entry => entry.To, comparer).ThenBy(entry => entry.From, comparer)],
            ReplacementSort.ToDescending => [.. matching.OrderByDescending(entry => entry.To, comparer).ThenBy(entry => entry.From, comparer)],
            _ => [.. matching.OrderBy(entry => entry.From, comparer)],
        };
    }
}

public sealed class LanguageDraft
{
    public LanguageDraft(IAutoCorrectDefaults defaults, string language, LanguageCustomization customization)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(customization);

        var set = defaults.For(language);

        Language = language;
        Replacements = new ReplacementListDraft(set.Replacements, customization);
        Abbreviations = new WordListDraft(new AbbreviationKind(), set.Abbreviations, customization.Abbreviations);
        TwoCapitalWords = new WordListDraft(new TwoCapitalsKind(), set.TwoCapitalWords, customization.TwoCapitalWords);
    }

    public string Language { get; }

    public ReplacementListDraft Replacements { get; }

    public WordListDraft Abbreviations { get; }

    public WordListDraft TwoCapitalWords { get; }

    public int Import(AutoCorrectLists lists)
    {
        ArgumentNullException.ThrowIfNull(lists);

        return Replacements.Merge(lists.Replacements)
            + Abbreviations.Merge(lists.Abbreviations)
            + TwoCapitalWords.Merge(lists.TwoCapitalWords);
    }

    public LanguageCustomization Customization()
    {
        var (replacements, removed) = Replacements.Customization();
        return new LanguageCustomization(replacements, removed, Abbreviations.Customization(), TwoCapitalWords.Customization());
    }
}

public sealed class AutoCorrectDraft
{
    private readonly IAutoCorrectDefaults _defaults;
    private readonly AutoCorrectProfile _original;
    private readonly Dictionary<string, LanguageDraft> _languages = new(StringComparer.Ordinal);

    public AutoCorrectDraft(IAutoCorrectDefaults defaults, AutoCorrectProfile profile)
    {
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _original = profile ?? throw new ArgumentNullException(nameof(profile));
        Enabled = profile.Enabled;
        Options = profile.Options;
    }

    public bool Enabled { get; set; }

    public AutoCorrectOptions Options { get; set; }

    public IReadOnlyList<string> Languages => _defaults.Languages;

    public LanguageDraft For(string language)
    {
        var resolved = _defaults.Resolve(language);
        if (_languages.TryGetValue(resolved, out var draft)) return draft;

        draft = new LanguageDraft(_defaults, resolved, _original.For(resolved));
        _languages[resolved] = draft;
        return draft;
    }

    public AutoCorrectProfile Profile()
    {
        var languages = new Dictionary<string, LanguageCustomization>(StringComparer.Ordinal);

        foreach (var (language, customization) in _original.Languages)
            languages[language] = customization;

        foreach (var (language, draft) in _languages)
            languages[language] = draft.Customization();

        return new AutoCorrectProfile(
            Enabled,
            Options with { Language = null },
            languages.Where(pair => !pair.Value.IsEmpty).ToDictionary(StringComparer.Ordinal));
    }
}

public interface IAutoCorrectDraftFactory
{
    AutoCorrectDraft Create(AutoCorrectProfile profile);
}

public sealed class AutoCorrectDraftFactory(IAutoCorrectDefaults defaults) : IAutoCorrectDraftFactory
{
    public AutoCorrectDraft Create(AutoCorrectProfile profile) => new(defaults, profile);
}
