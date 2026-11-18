using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class CommentSearch
{
    public IReadOnlyList<SearchMatch> FindAll(Worksheet sheet, string query, SearchOptions options, CellRange? scope, TextMatchers matchers)
    {
        if (string.IsNullOrEmpty(query)) return [];
        var matcher = matchers.For(query, options);
        return [.. sheet.Comments
            .Where(pair => scope is not { } range || range.Contains(pair.Key))
            .OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column)
            .Where(pair => pair.Value.Texts.Any(matcher.Matches))
            .Select(pair => new SearchMatch(pair.Key, pair.Value.Texts.First(matcher.Matches)))];
    }

    public int ReplaceAll(Worksheet sheet, string query, string replacement, SearchOptions options, CellRange? scope, UndoStack? history, TextMatchers matchers)
    {
        var matches = FindAll(sheet, query, options, scope, matchers);
        if (matches.Count == 0) return 0;

        var matcher = matchers.For(query, options);
        using var transaction = history?.BeginTransaction(options.HistoryLabel);
        var replaced = 0;
        foreach (var match in matches)
        {
            var address = match.Address;
            var previous = sheet.Comments[address];
            var updated = previous with
            {
                Text = Substitute(previous.Text, matcher, replacement),
                Replies = [.. previous.Replies.Select(reply => reply with { Text = Substitute(reply.Text, matcher, replacement) })],
            };
            if (updated.Texts.SequenceEqual(previous.Texts, StringComparer.Ordinal)) continue;

            sheet.Comments[address] = updated;
            replaced++;
            history?.Push(new DelegateAction(
                options.HistoryLabel,
                redo: () => sheet.Comments[address] = updated,
                undo: () => sheet.Comments[address] = previous));
        }

        return replaced;
    }

    private string Substitute(string text, TextMatcher matcher, string replacement) =>
        matcher.Matches(text) ? matcher.Substitute(text, replacement) : text;
}
