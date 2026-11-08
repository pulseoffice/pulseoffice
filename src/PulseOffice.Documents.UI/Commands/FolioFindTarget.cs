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

    public IReadOnlyList<FindOption> Options { get; } =
    [
        new(MatchCase, "find.matchCase", previous?.MatchCase ?? false),
        new(WholeWords, "folio.find.wholeWords", previous?.WholeWords ?? false),
    ];

    public string InitialQuery => initialQuery;

    public bool IsReadOnly => editor.ReadOnly();

    public bool SupportsFindNext => true;

    public string? Summarize(FindRequest request) => null;

    public string FindNext(FindRequest request) =>
        commands.Next(editor, QueryOf(request)) ? string.Empty : texts.Format("folio.find.notFound", request.Query);

    public string ReplaceAll(FindRequest request) =>
        texts.Format("folio.find.replaced", commands.ReplaceAll(editor, QueryOf(request), request.Replacement));

    private SearchQuery QueryOf(FindRequest request) => new(request.Query, request.Has(MatchCase), request.Has(WholeWords));
}
