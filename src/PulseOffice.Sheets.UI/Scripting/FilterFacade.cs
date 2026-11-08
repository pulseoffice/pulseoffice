using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.Scripting;

public sealed class FilterFacade(IScriptDispatcher dispatcher, IActiveSheetContext context, ISheetFilter filter, IScriptSheetEdits edits, IScriptReferences references, IScriptProtection protection, ISheetRangeFilters rangeFilters) : ScriptFacade(dispatcher)
{
    public bool IsActive => Read(() => Sheet()?.AutoFilter.IsActive ?? false);

    public string Range => Read(() =>
        Sheet()?.AutoFilter.Range is { } range ? range.ToString() : string.Empty);

    public int HiddenRows => Read(() => Apply().Count);

    public bool Activate(string reference, bool hasHeader = true) => Write(() => edits.Record("Filter", () =>
    {
        var range = references.Range(reference);
        protection.Permitted(rules => rules.AllowFilter, "using the AutoFilter");
        if (Sheet() is not { } sheet) return false;

        sheet.AutoFilter.Activate(range, hasHeader);

        rangeFilters.Refresh(context, sheet);

        return true;
    }, done => done));

    public void Deactivate() => Change(() => edits.Record("Filter", () =>
    {
        protection.Permitted(rules => rules.AllowFilter, "using the AutoFilter");
        if (Sheet() is not { } sheet) return false;

        sheet.AutoFilter.Deactivate();

        rangeFilters.Refresh(context, sheet);
        return true;
    }, done => done));

    public int ByValues(int column, string[] values) => Set(column, new ColumnFilter(column - 1)
    {
        AllowedValues = new HashSet<string>(values ?? [], StringComparer.CurrentCultureIgnoreCase),
    });

    public int ByCondition(int column, string criteria) => Set(column, new ColumnFilter(column - 1)
    {
        Criteria = criteria,
    });

    public int ByConditions(int column, string criteria, string secondCriteria, string join = "and") => Set(column, new ColumnFilter(column - 1)
    {
        Criteria = criteria,
        SecondCriteria = secondCriteria,
        Join = Join(join),
    });

    private CriteriaJoin Join(string join) => join?.Trim().ToLowerInvariant() switch
    {
        "and" or "" or null => CriteriaJoin.And,
        "or" => CriteriaJoin.Or,
        _ => throw new ArgumentException($"The join must be \"and\" or \"or\", not \"{join}\".", nameof(join)),
    };

    public int ClearColumn(int column) => Set(column, new ColumnFilter(column - 1));

    public void ClearAll() => Change(() => edits.Record("Filter", () =>
    {
        protection.Permitted(rules => rules.AllowFilter, "using the AutoFilter");
        if (Sheet() is not { } sheet) return false;

        sheet.AutoFilter.Clear();

        rangeFilters.Refresh(context, sheet);
        return true;
    }, done => done));

    public string[] Values(int column) => Read<string[]>(() =>
        Sheet() is { } sheet && Active(sheet) is var range && Column(range, column) is var index
            ? [.. filter.DistinctValues(sheet, range, index - 1, sheet.AutoFilter.HasHeader)]
            : []);

    private CellRange Active(Worksheet sheet) => sheet.AutoFilter is { IsActive: true, Range: { } range }
        ? range
        : throw new InvalidOperationException("There is no AutoFilter on this sheet: call filter.Activate(reference) first.");

    private int Column(CellRange range, int column) => column >= 1 && column <= range.End.Column - range.Start.Column + 1
        ? column
        : throw new ArgumentOutOfRangeException(nameof(column), column,
            $"The filter column must be between 1 and {range.End.Column - range.Start.Column + 1}.");

    private int Set(int column, ColumnFilter columnFilter) => Math.Max(0, Write(() => edits.Record("Filter", () =>
    {
        if (Sheet() is not { } sheet) return -1;
        var range = Active(sheet);
        protection.Permitted(rules => rules.AllowFilter, "using the AutoFilter");
        Column(range, column);

        sheet.AutoFilter.Set(columnFilter);

        rangeFilters.Refresh(context, sheet);

        return Apply().Count;
    }, count => count >= 0)));

    private IReadOnlySet<int> Apply()
    {
        if (Sheet() is not { AutoFilter.Range: { } range } sheet) return new HashSet<int>();

        var hidden = filter.RowsToHide(
            sheet, range, sheet.AutoFilter.Filters, sheet.AutoFilter.HasHeader, context.Workbook?.Styles);

        return hidden;
    }

    private Worksheet? Sheet() =>
        context.Workbook is { } workbook ? workbook.Sheets[context.SheetIndex] : null;
}
