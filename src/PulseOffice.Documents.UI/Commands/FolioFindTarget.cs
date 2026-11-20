using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Search;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioFindTarget(
    FolioEditor editor, FolioFindCommands commands, ITextCatalog texts, string initialQuery, SearchQuery? previous) : IFindReplaceTarget
{
    private const string MatchCase = "matchCase";
    private const string WholeWords = "wholeWords";
    private const string Regex = "regex";

    public IReadOnlyList<FindOption> Options { get; } =
    [
        new(MatchCase, "find.matchCase", previous?.MatchCase ?? false),
        new(WholeWords, "folio.find.wholeWords", previous?.WholeWords ?? false),
        new(Regex, "find.regex", previous?.Regex ?? false),
    ];

    public string InitialQuery => initialQuery;

    public bool IsReadOnly => editor.ReadOnly();

    public bool SupportsFindNext => true;

    public string? Summarize(FindRequest request)
    {
        if (request.Query.Length == 0) return string.Empty;
        var query = QueryOf(request);
        if (!commands.IsValid(query)) return texts["folio.find.invalidPattern"];
        return commands.Count(editor, query) switch
        {
            0 => texts["find.matches.none"],
            1 => texts["find.matches.one"],
            var many => texts.Format("find.matches.many", many),
        };
    }

    public string FindNext(FindRequest request) =>
        commands.Next(editor, QueryOf(request)) ? string.Empty : texts.Format("folio.find.notFound", request.Query);

    public bool SupportsReplace => true;

    public string Replace(FindRequest request) =>
        commands.Replace(editor, QueryOf(request), request.Replacement) ? string.Empty : texts.Format("folio.find.notFound", request.Query);

    public string ReplaceAll(FindRequest request) =>
        texts.Format("folio.find.replaced", commands.ReplaceAll(editor, QueryOf(request), request.Replacement));

    private SearchQuery QueryOf(FindRequest request) => new(request.Query, request.Has(MatchCase), request.Has(WholeWords), request.Has(Regex));
}
