using PulseOffice.Text.Spelling;

namespace PulseOffice.UI.Controls.Spelling;

public enum SpellingStepKind
{
    Skip,
    Replace,
    Stop,
    Recheck,
}

public readonly record struct SpellingStep(SpellingStepKind Kind, string Replacement = "");

public sealed class SpellingRound(IUserDictionary userWords)
{
    private readonly HashSet<string> _ignored = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _everywhere = new(StringComparer.Ordinal);

    public bool Ignores(string word) => _ignored.Contains(word) || userWords.Knows(word);

    public string? Automatic(string word) => _everywhere.GetValueOrDefault(word);

    public SpellingStep Decide(string word, SpellingAnswer answer)
    {
        switch (answer.Decision)
        {
            case SpellingDecision.Stop:
                return new SpellingStep(SpellingStepKind.Stop);
            case SpellingDecision.Recheck:
                return new SpellingStep(SpellingStepKind.Recheck);
            case SpellingDecision.Ignore:
                return new SpellingStep(SpellingStepKind.Skip);
            case SpellingDecision.IgnoreAll:
                _ignored.Add(word);
                return new SpellingStep(SpellingStepKind.Skip);
            case SpellingDecision.Learn:
                userWords.Add(word);
                return new SpellingStep(SpellingStepKind.Skip);
            case SpellingDecision.ReplaceAll:
                _everywhere[word] = answer.Replacement;
                return new SpellingStep(SpellingStepKind.Replace, answer.Replacement);
            default:
                return new SpellingStep(SpellingStepKind.Replace, answer.Replacement);
        }
    }
}

public interface ISpellingLanguagePicker
{
    Task<string?> PickAsync(LanguageTag current, string titleKey, CancellationToken ct);

    Task<string?> PickAsync(LanguageTag current, string titleKey, DictionaryLanguage extra, CancellationToken ct);
}

public sealed class SpellingLanguagePicker(
    ISpellDictionaryProvider dictionaries,
    IDictionaryNotice notice,
    ILanguagePrompt prompt,
    IDictionaryLanguageNames choices) : ISpellingLanguagePicker
{
    public Task<string?> PickAsync(LanguageTag current, string titleKey, CancellationToken ct) => Offer(current, titleKey, null, ct);

    public Task<string?> PickAsync(LanguageTag current, string titleKey, DictionaryLanguage extra, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(extra);
        return Offer(current, titleKey, extra, ct);
    }

    private async Task<string?> Offer(LanguageTag current, string titleKey, DictionaryLanguage? extra, CancellationToken ct)
    {
        var available = dictionaries.Available;
        if (available.Count == 0 && extra is null)
        {
            notice.ShowMissingDictionaries();
            return null;
        }

        var names = available.Select(tag => tag.Canonical).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
        var index = names.IndexOf(current.Canonical) is var exact and >= 0
            ? exact
            : names.FindIndex(name => LanguageTag.Parse(name).Matches(current));

        List<DictionaryLanguage> offered = [.. names.Select(choices.For)];
        if (extra is not null) offered.Add(extra);
        var choice = await prompt.AskAsync(offered, Math.Max(0, index), titleKey, ct).ConfigureAwait(true);
        return choice is { } picked ? offered[Math.Clamp(picked, 0, offered.Count - 1)].Code : null;
    }
}
