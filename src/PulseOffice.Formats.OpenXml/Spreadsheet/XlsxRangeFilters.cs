using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Book = PulseOffice.Sheets.Workbook;
using Sheet = PulseOffice.Sheets.Worksheet;
using SdkDefinedName = DocumentFormat.OpenXml.Spreadsheet.DefinedName;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxRangeFilters
{
    private const string FilterDatabase = "_xlnm._FilterDatabase";
    private const string Criteria = "_xlnm.Criteria";

    private readonly FilterTreeColumns _columns = new();

    public (CellRange Range, IReadOnlyList<ColumnFilter> Columns)? SheetAutoFilter(Book workbook, Sheet sheet)
    {
        if (sheet.AutoFilter.IsActive || sheet.RangeFilter is not { HasHeader: true, Filter: { Criteria: null, UniqueOnly: false } stored } filtered) return null;
        var index = workbook.IndexOf(sheet);
        if (workbook.DatabaseRanges.All.Any(range => range.SheetIndex == index && range.Range.IntersectsWith(filtered.Range))) return null;
        return _columns.ToColumns(stored.Condition) is { Count: > 0 } columns ? (filtered.Range, columns) : null;
    }

    public IReadOnlyList<ColumnFilter>? TableColumns(DatabaseRange range)
    {
        if (range.RangeFilter is not { } stored) return range.Filters;
        if (stored.Criteria is not null || stored.UniqueOnly || _columns.ToColumns(stored.Condition) is not { } columns) return null;
        return columns.Any(column => range.Filters.Any(existing => existing.ColumnOffset == column.ColumnOffset))
            ? null
            : [.. range.Filters, .. columns];
    }

    public int WrittenAsAutoFilter(Book workbook) => workbook.Sheets.Count(sheet => SheetAutoFilter(workbook, sheet) is not null);

    public int Lost(Book workbook) =>
        workbook.Sheets.Count(sheet => sheet.RangeFilter is { } stored
            && (SheetAutoFilter(workbook, sheet) is null && !WritesCriteria(workbook, sheet) || stored.Filter.UniqueOnly))
        + workbook.DatabaseRanges.All.Count(range => range.RangeFilter is { } stored
            && (CriteriaOwner(workbook, range.SheetIndex) == range ? stored.UniqueOnly : TableColumns(range) is null || !range.HasHeader));

    public IEnumerable<SdkDefinedName> DefinedNames(Book workbook)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            if (StoredCriteria(workbook, index) is not { } stored) continue;
            var sheet = workbook.Sheets[index];
            yield return new SdkDefinedName { Name = FilterDatabase, LocalSheetId = (uint)index, Hidden = true, Text = Absolute(sheet.Name, stored.Range) };
            yield return new SdkDefinedName { Name = Criteria, LocalSheetId = (uint)index, Text = Absolute(workbook.Sheets[stored.Criteria.SheetIndex].Name, stored.Criteria.Range) };
        }
    }

    public void Read(IEnumerable<SdkDefinedName> names, Book workbook)
    {
        var databases = new Dictionary<uint, CellRange>();
        var criteria = new Dictionary<uint, (int Sheet, CellRange Range)>();
        foreach (var name in names)
        {
            if (name.LocalSheetId?.Value is not { } local || local >= workbook.Sheets.Count || Parse(workbook, name.Text) is not { } parsed) continue;
            if (name.Name?.Value == FilterDatabase && parsed.Sheet == (int)local) databases[local] = parsed.Range;
            else if (name.Name?.Value == Criteria) criteria[local] = parsed;
        }

        foreach (var (local, source) in criteria)
        {
            if (!databases.TryGetValue(local, out var range)) continue;
            var filter = new RangeFilter { Criteria = new AdvancedCriteria(source.Sheet, source.Range) };
            var named = workbook.DatabaseRanges.All.FirstOrDefault(candidate =>
                candidate.SheetIndex == (int)local && candidate.HasHeader && candidate.Range == range && candidate.RangeFilter is null && candidate.Filters.Count == 0);
            if (named is not null) workbook.DatabaseRanges.Define(named with { RangeFilter = filter });
            else if (workbook.Sheets[(int)local].RangeFilter is null) workbook.Sheets[(int)local].RangeFilter = new SheetRangeFilter(range, true, filter);
        }
    }

    private (CellRange Range, AdvancedCriteria Criteria)? StoredCriteria(Book workbook, int index)
    {
        var sheet = workbook.Sheets[index];
        if (WritesCriteria(workbook, sheet)) return (sheet.RangeFilter!.Range, sheet.RangeFilter.Filter.Criteria!);
        return CriteriaOwner(workbook, index) is { RangeFilter.Criteria: { } criteria } owner ? (owner.Range, criteria) : null;
    }

    private DatabaseRange? CriteriaOwner(Book workbook, int index)
    {
        if (index < 0 || index >= workbook.Sheets.Count) return null;
        var sheet = workbook.Sheets[index];
        if (sheet.AutoFilter.IsActive || sheet.RangeFilter is not null) return null;
        return workbook.DatabaseRanges.All
            .Where(range => range.SheetIndex == index)
            .OrderBy(range => range.Range.Start.Row).ThenBy(range => range.Range.Start.Column).ThenBy(range => range.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(range => range is { HasHeader: true, HasTotalsRow: false, ShowsFilterButtons: false, Filters.Count: 0, RangeFilter: { Criteria: { } criteria, Condition: null } }
                && criteria.SheetIndex >= 0 && criteria.SheetIndex < workbook.Sheets.Count);
    }

    private bool WritesCriteria(Book workbook, Sheet sheet) =>
        !sheet.AutoFilter.IsActive
        && sheet.RangeFilter is { HasHeader: true, Filter: { Criteria: { } criteria, Condition: null } }
        && criteria.SheetIndex >= 0 && criteria.SheetIndex < workbook.Sheets.Count;

    private string Absolute(string sheet, CellRange range) =>
        $"'{sheet.Replace("'", "''", StringComparison.Ordinal)}'!${CellAddress.ColumnName(range.Start.Column)}${range.Start.Row + 1}:${CellAddress.ColumnName(range.End.Column)}${range.End.Row + 1}";

    private (int Sheet, CellRange Range)? Parse(Book workbook, string? text)
    {
        if (text is null) return null;
        var bang = text.LastIndexOf('!');
        if (bang <= 0) return null;
        var sheetName = text[..bang];
        if (sheetName.Length >= 2 && sheetName[0] == '\'' && sheetName[^1] == '\'') sheetName = sheetName[1..^1].Replace("''", "'", StringComparison.Ordinal);
        var sheet = workbook.Sheets.ToList().FindIndex(candidate => string.Equals(candidate.Name, sheetName, StringComparison.OrdinalIgnoreCase));
        var reference = text[(bang + 1)..].Replace("$", string.Empty, StringComparison.Ordinal);
        if (sheet < 0) return null;
        if (CellRange.TryParseA1(reference, out var range)) return (sheet, range);
        return CellAddress.TryParseA1(reference, out var single) ? (sheet, new CellRange(single, single)) : null;
    }
}
