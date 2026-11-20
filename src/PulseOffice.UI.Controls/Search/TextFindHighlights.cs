using System.Runtime.CompilerServices;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Spelling;

namespace PulseOffice.UI.Controls.Search;

public sealed record TextFindQuery(string Text, bool MatchCase, bool WholeWords = false, bool Regex = false)
{
    public TextPattern Pattern => new(Text, MatchCase, WholeWords, Regex);

    public bool IsEmpty => Text.Length == 0;
}

public interface ITextFindHighlights
{
    void Show(object scope, TextFindQuery query);

    void Clear(object scope);

    TextFindQuery? QueryOf(object scope);

    IReadOnlyList<WordSpan> SpansIn(TextFindQuery query, string text);

    event EventHandler? Changed;
}

public sealed class TextFindHighlights(ITextPatternMatcher matcher) : ITextFindHighlights
{
    public TextFindHighlights() : this(new TextPatternMatcher())
    {
    }

    private readonly ConditionalWeakTable<object, TextFindQuery> _queries = new();

    public event EventHandler? Changed;

    public void Show(object scope, TextFindQuery query)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        if (_queries.TryGetValue(scope, out var current) && current == query) return;
        if (query.IsEmpty) _queries.Remove(scope);
        else _queries.AddOrUpdate(scope, query);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear(object scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (_queries.Remove(scope)) Changed?.Invoke(this, EventArgs.Empty);
    }

    public TextFindQuery? QueryOf(object scope) =>
        scope is not null && _queries.TryGetValue(scope, out var query) ? query : null;

    public IReadOnlyList<WordSpan> SpansIn(TextFindQuery query, string text)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.IsEmpty || string.IsNullOrEmpty(text)) return [];
        return [.. matcher.Find(query.Pattern, text).Select(hit => new WordSpan(hit.Start, hit.Length, text.Substring(hit.Start, hit.Length)))];
    }
}
