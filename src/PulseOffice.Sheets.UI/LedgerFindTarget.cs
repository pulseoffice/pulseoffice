using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFindTarget(
    IActiveSheetContext context,
    IStatusBarService status,
    ITextCatalog texts,
    ISheetEditGuard guard,
    IWorkbookSearch search,
    bool readOnly,
    IWorkbookMatchOrder? order = null) : IFindReplaceTarget
{
    private readonly IWorkbookMatchOrder _order = order ?? new WorkbookMatchOrder();

    private const string MatchCase = "matchCase";
    private const string WholeCell = "wholeCell";
    private const string InFormulas = "inFormulas";
    private const string Regex = "regex";
    private const string AllSheets = "allSheets";
    private const string ByFormat = "byFormat";
    private const string InComments = "inComments";
    private const string ByColumns = "byColumns";

    public IReadOnlyList<FindOption> Options { get; } =
    [
        new(MatchCase, "find.matchCase"),
        new(WholeCell, "find.wholeCell"),
        new(InFormulas, "find.inFormulas"),
        new(Regex, "find.regex"),
        new(AllSheets, "find.allSheets"),
        new(ByFormat, "find.byFormat"),
        new(InComments, "find.inComments"),
        new(ByColumns, "find.byColumns"),
    ];

    public string InitialQuery => string.Empty;

    public bool IsReadOnly => readOnly;

    public bool SupportsFindNext => true;

    public bool SupportsFindPrevious => true;

    public LedgerFindQuery QueryOf(FindRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new LedgerFindQuery(request.Query, OptionsOf(request), context.SheetIndex);
    }

    public string? Summarize(FindRequest request)
    {
        if (context.Workbook is not { } workbook || (request.Query.Length == 0 && !request.Has(ByFormat)))
            return string.Empty;

        var matches = search.FindAll(workbook, context.SheetIndex, request.Query, OptionsOf(request));
        return matches.Count switch
        {
            0 => texts["find.matches.none"],
            1 => texts["find.matches.one"],
            _ => texts.Format("find.matches.many", matches.Count),
        };
    }

    public string FindNext(FindRequest request) => Go(request, backwards: false);

    public string FindPrevious(FindRequest request) => Go(request, backwards: true);

    private string Go(FindRequest request, bool backwards)
    {
        if (context.Workbook is not { } workbook || (request.Query.Length == 0 && !request.Has(ByFormat)))
            return string.Empty;

        var matches = search.FindAll(workbook, context.SheetIndex, request.Query, OptionsOf(request));
        if (_order.Next(matches, context.SheetIndex, context.Selection.Start, request.Has(ByColumns), backwards) is not { } match)
            return texts["find.matches.none"];

        if (match.SheetIndex != context.SheetIndex) context.SelectSheet(match.SheetIndex);
        context.SelectRange(new CellRange(match.Address, match.Address));
        var place = request.Has(AllSheets) ? match.SheetName + "." + match.Address.ToA1() : match.Address.ToA1();
        return texts.Format("find.foundInCell", place);
    }

    public string ReplaceAll(FindRequest request)
    {
        if (context.Workbook is not { } workbook) return string.Empty;

        if (readOnly)
        {
            status.Report(texts["status.readOnly"], NotificationSeverity.Error, NotificationRemedies.EditMode);
            return texts["status.readOnly"];
        }

        var options = OptionsOf(request) with { HistoryLabel = texts["find.replaceAll"] };
        var found = search.FindAll(workbook, context.SheetIndex, request.Query, options);
        if (options.InComments && found.Any(match => workbook.Sheets[match.SheetIndex].Protection.IsEnabled))
        {
            status.Report(texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return texts["status.protection.objects"];
        }

        var locked = !options.InComments && found
            .Any(match => !guard.CanWrite(workbook, workbook.Sheets[match.SheetIndex], new CellRange(match.Address, match.Address)));
        if (locked)
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return texts["status.protection.locked"];
        }

        int replaced;
        using (context.History?.BeginTransaction(texts["find.replaceAll"]))
            replaced = search.ReplaceAll(
                workbook, context.SheetIndex, request.Query, request.Replacement, options, scope: null, history: context.History);
        context.RequestRefresh();

        var message = replaced == 0 ? texts["find.replaced.none"] : texts.Format("find.replaced.many", replaced);
        status.Report(message, replaced == 0 ? NotificationSeverity.Warning : NotificationSeverity.Success);
        return message;
    }

    private SearchOptions OptionsOf(FindRequest request) => new()
    {
        MatchCase = request.Has(MatchCase),
        WholeCell = request.Has(WholeCell),
        SearchFormulas = request.Has(InFormulas),
        UseRegex = request.Has(Regex),
        AllSheets = request.Has(AllSheets),
        InComments = request.Has(InComments),
        Style = request.Has(ByFormat) ? CurrentFormat() : null,
    };

    private StyleFilter? CurrentFormat()
    {
        if (context.Workbook is not { } workbook) return null;

        var sheet = workbook.Sheets[context.SheetIndex];
        var style = workbook.Styles.Get(sheet.StyleIdAt(context.Selection.Start));

        return new StyleFilter
        {
            Bold = style.Font.Bold,
            Italic = style.Font.Italic,
            FontFamily = style.Font.Family,
            FontSize = style.Font.Size,
            BackgroundHex = style.BackgroundHex,
            NumberFormatId = style.NumberFormatId,
        };
    }
}
