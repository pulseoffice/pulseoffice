using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Book = PulseOffice.Sheets.Workbook;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed record PlannedTable(uint Id, DatabaseRange Range, IReadOnlyList<string> Columns, bool CarriesSheetFilter);

internal sealed class XlsxTablePlan(IReadOnlyDictionary<int, IReadOnlyList<PlannedTable>> bySheet, int unwritable)
{
    public int Unwritable => unwritable;

    public IReadOnlyList<PlannedTable> For(int sheetIndex) => bySheet.TryGetValue(sheetIndex, out var tables) ? tables : [];

    public bool CarriesSheetFilter(int sheetIndex) => For(sheetIndex).Any(table => table.CarriesSheetFilter);
}

internal sealed partial class XlsxTablePlanner
{
    private const int MaximumNameLength = 255;

    [GeneratedRegex(@"^[A-Za-z_\\][A-Za-z0-9_.\\]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NameSyntax();

    [GeneratedRegex(@"^([A-Za-z]{1,3}[0-9]+|[RrCc][0-9]*|[Rr][0-9]*[Cc][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceLike();

    public XlsxTablePlan Plan(Book workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var definedNames = workbook.DefinedNames.Select(name => name.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bySheet = new Dictionary<int, IReadOnlyList<PlannedTable>>();
        var unwritable = 0;
        var id = 0u;

        foreach (var group in workbook.DatabaseRanges.All.GroupBy(range => range.SheetIndex).OrderBy(group => group.Key))
        {
            if (group.Key < 0 || group.Key >= workbook.Sheets.Count) { unwritable += group.Count(); continue; }
            var sheet = workbook.Sheets[group.Key];
            var planned = new List<PlannedTable>();
            foreach (var range in group.OrderBy(range => range.Range.Start.Row).ThenBy(range => range.Range.Start.Column))
            {
                var carries = sheet.AutoFilter.Range is { } filtered && (filtered == range.Range || filtered == range.RangeWithoutTotals);
                if (Columns(sheet, range) is not { } columns
                    || !ValidName(range.Name) || definedNames.Contains(range.Name)
                    || !Fits(sheet, range, carries, planned)
                    || carries && !range.HasHeader
                    || (range.ShowsFilterButtons || range.Filters.Count > 0) && !range.HasHeader)
                {
                    unwritable++;
                    continue;
                }
                planned.Add(new PlannedTable(++id, range, columns, carries));
            }
            if (planned.Count > 0) bySheet[group.Key] = planned;
        }
        return new XlsxTablePlan(bySheet, unwritable);
    }

    private bool ValidName(string name) =>
        name.Length is > 0 and <= MaximumNameLength && NameSyntax().IsMatch(name) && !ReferenceLike().IsMatch(name);

    private bool Fits(Sheet sheet, DatabaseRange range, bool carries, IReadOnlyList<PlannedTable> planned) =>
        range.QueryBindingId is null
        && range.Range.RowCount >= (range.HasHeader ? 2 : 1) + (range.HasTotalsRow ? 1 : 0)
        && !sheet.Merges.Overlapping(range.Range).Any()
        && !planned.Any(other => other.Range.Range.IntersectsWith(range.Range))
        && !sheet.QueryBindings.All.Any(binding => binding.Range.IntersectsWith(range.Range))
        && (carries || sheet.AutoFilter.Range is not { } filter || !filter.IntersectsWith(range.Range));

    private IReadOnlyList<string>? Columns(Sheet sheet, DatabaseRange range)
    {
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var column = range.Range.Start.Column; column <= range.Range.End.Column; column++)
        {
            string name;
            if (range.HasHeader)
            {
                var cell = sheet[new CellAddress(range.Range.Start.Row, column)];
                if (cell.HasFormula || cell.Value.Kind != ValueKind.Text || cell.Value.AsText() is not { Length: > 0 } text) return null;
                name = text;
            }
            else name = $"Column{column - range.Range.Start.Column + 1}";
            if (!seen.Add(name)) return null;
            columns.Add(name);
        }
        return columns;
    }
}

internal sealed class XlsxTableWriter(XlsxAutoFilterCodec filters, XlsxFormulaSyntax formulas)
{
    private readonly XlsxRangeFilters _rangeFilters = new();
    private readonly XlsxSortStateCodec _sorts = new();

    public void Write(WorksheetPart part, Sheet sheet, XlsxWriteContext context)
    {
        var sheetIndex = IndexOf(context.Workbook, sheet);
        var planned = context.Tables.For(sheetIndex);
        if (planned.Count == 0) return;

        var parts = new TableParts { Count = (uint)planned.Count };
        foreach (var table in planned)
        {
            var tablePart = part.AddNewPart<TableDefinitionPart>();
            tablePart.Table = Table(sheet, table, context);
            tablePart.Table.Save();
            parts.AppendChild(new TablePart { Id = part.GetIdOfPart(tablePart) });
        }
        part.Worksheet!.AppendChild(parts);
    }

    private Table Table(Sheet sheet, PlannedTable planned, XlsxWriteContext context)
    {
        var range = planned.Range;
        var reference = $"{range.Range.Start.ToA1()}:{range.Range.End.ToA1()}";
        var table = new Table
        {
            Id = planned.Id,
            Name = range.Name,
            DisplayName = range.Name,
            Reference = reference,
            HeaderRowCount = range.HasHeader ? null : 0u,
            TotalsRowCount = range.HasTotalsRow ? 1u : null,
            TotalsRowShown = range.HasTotalsRow ? null : false,
        };
        if (planned.CarriesSheetFilter)
            table.AppendChild(filters.Build(range.RangeWithoutTotals, sheet.AutoFilter.Filters, context.Conditionals, new XlsxFilterSource(context.Workbook, sheet, range.HasHeader)));
        else if (range.HasHeader && _rangeFilters.TableColumns(range) is { } stored && (range.ShowsFilterButtons || stored.Count > 0))
            table.AppendChild(filters.Build(range.RangeWithoutTotals, stored, context.Conditionals, new XlsxFilterSource(context.Workbook, sheet, range.HasHeader)));
        if (_sorts.Build(range.RangeWithoutTotals, range.HasHeader, planned.CarriesSheetFilter ? sheet.AutoFilter.Sort : range.Sort) is { } sort)
            table.AppendChild(sort);

        var columns = new TableColumns { Count = (uint)planned.Columns.Count };
        for (var index = 0; index < planned.Columns.Count; index++)
            columns.AppendChild(Column(sheet, range, index, planned.Columns[index]));
        table.AppendChild(columns);
        table.AppendChild(new TableStyleInfo { ShowFirstColumn = false, ShowLastColumn = false, ShowRowStripes = false, ShowColumnStripes = false });
        return table;
    }

    private TableColumn Column(Sheet sheet, DatabaseRange range, int index, string name)
    {
        var column = new TableColumn { Id = (uint)index + 1, Name = name };
        if (!range.HasTotalsRow) return column;
        var total = sheet[new CellAddress(range.Range.End.Row, range.Range.Start.Column + index)];
        if (total.HasFormula)
        {
            column.TotalsRowFunction = TotalsRowFunctionValues.Custom;
            column.TotalsRowFormula = new TotalsRowFormula(formulas.ToOpenXml(total.Formula!));
        }
        else if (total.Value.Kind == ValueKind.Text && total.Value.AsText() is { Length: > 0 } label)
            column.TotalsRowLabel = label;
        return column;
    }

    private int IndexOf(Book workbook, Sheet sheet)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (ReferenceEquals(workbook.Sheets[index], sheet)) return index;
        return -1;
    }
}
