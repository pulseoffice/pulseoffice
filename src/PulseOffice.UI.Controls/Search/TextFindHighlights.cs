using System.Runtime.CompilerServices;
using PulseOffice.Text.Spelling;

namespace PulseOffice.UI.Controls.Search;

public sealed record TextFindQuery(string Text, bool MatchCase)
{
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

public sealed class TextFindHighlights : ITextFindHighlights
{
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
        var comparison = query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var spans = new List<WordSpan>();
        var index = 0;
        while (index < text.Length && (index = text.IndexOf(query.Text, index, comparison)) >= 0)
        {
            spans.Add(new WordSpan(index, query.Text.Length, text.Substring(index, query.Text.Length)));
            index += query.Text.Length;
        }
        return spans;
    }
}
