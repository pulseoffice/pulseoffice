using System.Globalization;

namespace PulseOffice.UI.Controls.Spelling;

public sealed record ThesaurusChoice(string Synonym, string PartOfSpeech);

public sealed record DictionaryLanguage(string Code, string Name);

public interface IThesaurusPrompt
{
    Task<string?> AskAsync(string word, IReadOnlyList<ThesaurusChoice> choices, string titleKey, CancellationToken ct);
}

public interface ILanguagePrompt
{
    Task<int?> AskAsync(IReadOnlyList<DictionaryLanguage> choices, int current, string titleKey, CancellationToken ct);
}

public interface IDictionaryLanguageNames
{
    DictionaryLanguage For(string tag);
}

public sealed class DictionaryLanguageNames : IDictionaryLanguageNames
{
    public DictionaryLanguage For(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        try
        {
            return new DictionaryLanguage(tag, CultureInfo.GetCultureInfo(tag).DisplayName);
        }
        catch (CultureNotFoundException)
        {
            return new DictionaryLanguage(tag, tag);
        }
    }
}

public abstract record TextReviewRequest(string TitleKey);

public sealed record SpellingReviewRequest(SpellingFinding Finding, string TitleKey) : TextReviewRequest(TitleKey);

public sealed record ThesaurusReviewRequest(string Word, IReadOnlyList<ThesaurusChoice> Choices, string TitleKey) : TextReviewRequest(TitleKey);

public sealed record LanguageReviewRequest(IReadOnlyList<DictionaryLanguage> Choices, int Current, string TitleKey) : TextReviewRequest(TitleKey);

public sealed record MissingDictionariesRequest(string TitleKey) : TextReviewRequest(TitleKey);

public interface IDictionaryNotice
{
    void ShowMissingDictionaries();
}

public interface ITextReviewSession
{
    TextReviewRequest? Pending { get; }

    bool IsDisplayed { get; set; }

    bool Invalidated { get; }

    void Invalidate();

    event EventHandler? Changed;

    void Answer(object? answer);

    void Dismiss();
}

public sealed class TextReviewSession(ISidePanelHost panel) : ITextReviewSession, ISpellingPrompt, IThesaurusPrompt, ILanguagePrompt, IDictionaryNotice
{
    public const string PageKey = "textReview";

    private readonly object _stale = new();
    private TaskCompletionSource<object?>? _waiting;

    public bool Invalidated { get; private set; }

    public TextReviewRequest? Pending { get; private set; }

    public bool IsDisplayed { get; set; }

    public event EventHandler? Changed;

    public async Task<SpellingAnswer> AskAsync(SpellingFinding finding, string titleKey, CancellationToken ct)
    {
        var answer = await WaitAsync(new SpellingReviewRequest(finding, titleKey), ct).ConfigureAwait(true);
        return ReferenceEquals(answer, _stale) ? new SpellingAnswer(SpellingDecision.Recheck) : answer as SpellingAnswer ?? new SpellingAnswer(SpellingDecision.Stop);
    }

    public async Task<string?> AskAsync(string word, IReadOnlyList<ThesaurusChoice> choices, string titleKey, CancellationToken ct) =>
        await WaitAsync(new ThesaurusReviewRequest(word, choices, titleKey), ct).ConfigureAwait(true) as string;

    public async Task<int?> AskAsync(IReadOnlyList<DictionaryLanguage> choices, int current, string titleKey, CancellationToken ct) =>
        await WaitAsync(new LanguageReviewRequest(choices, current, titleKey), ct).ConfigureAwait(true) as int?;

    public void Answer(object? answer)
    {
        Invalidated = false;
        Complete(answer);
    }

    public void Dismiss()
    {
        Invalidated = false;
        Complete(null);
    }

    public void ShowMissingDictionaries()
    {
        Complete(null);
        Invalidated = false;
        if (!IsDisplayed || !panel.IsOpen) panel.Show(PageKey);
        Pending = new MissingDictionariesRequest("textReview.noDictionaries.title");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Invalidate()
    {
        if (_waiting is null) return;
        Invalidated = true;
        Complete(_stale);
    }

    private async Task<object?> WaitAsync(TextReviewRequest request, CancellationToken ct)
    {
        Complete(null);

        if (!IsDisplayed || !panel.IsOpen) panel.Show(PageKey);

        var waiting = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting = waiting;
        Pending = request;
        Changed?.Invoke(this, EventArgs.Empty);

        await using var registration = ct.Register(() => Complete(null)).ConfigureAwait(true);
        return await waiting.Task.ConfigureAwait(true);
    }

    private void Complete(object? answer)
    {
        if (_waiting is not { } waiting)
        {
            if (Pending is null) return;
            Pending = null;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _waiting = null;
        Pending = null;
        Changed?.Invoke(this, EventArgs.Empty);
        waiting.TrySetResult(answer);
    }
}
