using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record WorkbookMatch(int SheetIndex, string SheetName, CellAddress Address, string Text);

public interface IWorkbookSearch
{
    IReadOnlyList<WorkbookMatch> FindAll(
        Workbook workbook, int currentSheet, string query,
        SearchOptions? options = null, CellRange? scope = null);

    int ReplaceAll(
        Workbook workbook, int currentSheet, string query, string replacement,
        SearchOptions? options = null, CellRange? scope = null, UndoStack? history = null);
}

public sealed class WorkbookSearch : IWorkbookSearch
{
    private readonly ISheetSearch _sheets;

    public WorkbookSearch(ISheetSearch? sheets = null) => _sheets = sheets ?? new SheetSearch();

    public IReadOnlyList<WorkbookMatch> FindAll(
        Workbook workbook, int currentSheet, string query,
        SearchOptions? options = null, CellRange? scope = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        options ??= SearchOptions.Default;

        var matches = new List<WorkbookMatch>();

        foreach (var index in SheetsToVisit(workbook, currentSheet, options))
        {
            var sheet = workbook.Sheets[index];
            var area = options.AllSheets && index != currentSheet ? null : scope;

            foreach (var match in _sheets.FindAll(sheet, query, options, area, workbook.Styles))
                matches.Add(new WorkbookMatch(index, sheet.Name, match.Address, match.Text));
        }

        return matches;
    }

    public int ReplaceAll(
        Workbook workbook, int currentSheet, string query, string replacement,
        SearchOptions? options = null, CellRange? scope = null, UndoStack? history = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        options ??= SearchOptions.Default;

        using var transaction = history?.BeginTransaction(options.HistoryLabel);
        var replaced = 0;

        foreach (var index in SheetsToVisit(workbook, currentSheet, options))
        {
            var area = options.AllSheets && index != currentSheet ? null : scope;

            replaced += _sheets.ReplaceAll(
                workbook.Sheets[index], query, replacement, options, area, history);
        }

        return replaced;
    }

    private IEnumerable<int> SheetsToVisit(
        Workbook workbook, int currentSheet, SearchOptions options)
    {
        if (!options.AllSheets)
        {
            if (currentSheet >= 0 && currentSheet < workbook.Sheets.Count) yield return currentSheet;
            yield break;
        }

        for (var index = 0; index < workbook.Sheets.Count; index++) yield return index;
    }
}
