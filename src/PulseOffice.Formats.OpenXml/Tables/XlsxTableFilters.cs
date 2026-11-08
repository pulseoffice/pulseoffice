using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using Book = PulseOffice.Sheets.Workbook;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxTableFilters(XlsxAutoFilterCodec codec)
{
    private readonly XlsxSortStateCodec _sorts = new();

    public void Read(AutoFilter? sheetFilter, WorksheetPart part, Sheet sheet, Book workbook, IReadOnlyList<DifferentialFormat> formats)
    {
        codec.Read(sheetFilter, sheet, formats);
        RememberSort(sheet, sheetFilter?.GetFirstChild<SortState>());
        var sheetSort = part.Worksheet?.GetFirstChild<SortState>();
        var sheetIndex = IndexOf(workbook, sheet);
        foreach (var tablePart in part.TableDefinitionParts)
        {
            if (tablePart.Table is not { } table || table.AutoFilter is not { } tableFilter) continue;
            if (!sheet.AutoFilter.IsActive)
            {
                codec.Read(tableFilter, sheet, formats);
                RememberSort(sheet, table.GetFirstChild<SortState>() ?? tableFilter.GetFirstChild<SortState>());
                continue;
            }
            if (sheetIndex < 0 || (table.DisplayName?.Value ?? table.Name?.Value) is not { Length: > 0 } name) continue;
            if (table.Reference?.Value is not { } reference || !PulseOffice.Sheets.Addressing.CellRange.TryParseA1(reference, out var range)) continue;
            var filters = codec.Read(tableFilter, formats, sheet)?.Filters ?? [];
            workbook.DatabaseRanges.Define(new DatabaseRange
            {
                Name = name,
                SheetIndex = sheetIndex,
                Range = range,
                HasHeader = (table.HeaderRowCount?.Value ?? 1) > 0,
                HasTotalsRow = (table.TotalsRowCount?.Value ?? 0) > 0 && range.RowCount > ((table.HeaderRowCount?.Value ?? 1) > 0 ? 1 : 0),
                ShowsFilterButtons = true,
                Filters = filters,
                Sort = Downward(_sorts.Read(table.GetFirstChild<SortState>() ?? tableFilter.GetFirstChild<SortState>(), range)),
            });
        }
        ReadSheetSort(sheet, sheetSort);
    }

    private void RememberSort(Sheet sheet, SortState? state)
    {
        if (sheet.AutoFilter.Range is { } range && sheet.AutoFilter.Sort is null) sheet.AutoFilter.Remember(Downward(_sorts.Read(state, range)));
    }

    private void ReadSheetSort(Sheet sheet, SortState? state)
    {
        if (state is null) return;
        if (sheet.AutoFilter.Range is { } filtered && sheet.AutoFilter.Sort is null && state.ColumnSort?.Value != true
            && _sorts.Area(state) is { } area && filtered.Contains(area.Start) && filtered.Contains(area.End))
        {
            RememberSort(sheet, state);
            return;
        }
        sheet.RangeSort = _sorts.ReadPlain(state);
    }

    private SortSettings? Downward(SortSettings? sort) => sort is { LeftToRight: false } ? sort : null;

    private int IndexOf(Book workbook, Sheet sheet)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (ReferenceEquals(workbook.Sheets[index], sheet)) return index;
        return -1;
    }
}
