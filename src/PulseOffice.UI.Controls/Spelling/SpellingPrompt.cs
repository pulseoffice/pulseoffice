namespace PulseOffice.UI.Controls.Spelling;

public enum SpellingDecision
{
    Replace,
    ReplaceAll,
    Ignore,
    IgnoreAll,
    Learn,
    Stop,
    Recheck,
}

public sealed record SpellingFinding(string Word, string Where, IReadOnlyList<string> Suggestions)
{
    public string? Rule { get; init; }
}

public sealed record SpellingAnswer(SpellingDecision Decision, string Replacement = "");

public interface ISpellingPrompt
{
    Task<SpellingAnswer> AskAsync(SpellingFinding finding, string titleKey, CancellationToken ct);
}
