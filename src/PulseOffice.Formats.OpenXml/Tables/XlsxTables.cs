using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using XlsxSheet = DocumentFormat.OpenXml.Spreadsheet.Sheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed record XlsxTable(string Name, string SheetName, CellRange Range, int HeaderRows, int TotalsRows, IReadOnlyList<string> Columns, SortSettings? Sort = null)
{
    public int FirstDataRow => Range.Start.Row + HeaderRows;

    public int LastDataRow => Range.End.Row - TotalsRows;

    public int? ColumnOf(string name)
    {
        for (var index = 0; index < Columns.Count; index++)
            if (string.Equals(Columns[index], name, StringComparison.OrdinalIgnoreCase)) return Range.Start.Column + index;
        return null;
    }
}

internal sealed class XlsxTableCatalog
{
    private readonly Dictionary<string, XlsxTable> _tables = new(StringComparer.OrdinalIgnoreCase);

    private readonly XlsxSortStateCodec _sorts = new();

    public IReadOnlyCollection<XlsxTable> All => _tables.Values;

    public XlsxTable? Find(string name) => _tables.GetValueOrDefault(name);

    public void Load(WorkbookPart workbookPart)
    {
        if (workbookPart.Workbook?.Sheets is not { } sheets) return;
        foreach (var sheet in sheets.Elements<XlsxSheet>())
        {
            if (sheet.Id?.Value is not { } id || sheet.Name?.Value is not { } sheetName) continue;
            if (!workbookPart.TryGetPartById(id, out var found) || found is not WorksheetPart part) continue;
            foreach (var tablePart in part.TableDefinitionParts)
            {
                if (tablePart.Table is not { } table) continue;
                var name = table.DisplayName?.Value ?? table.Name?.Value;
                if (name is null || table.Reference?.Value is not { } reference || !CellRange.TryParseA1(reference, out var range)) continue;
                var columns = table.TableColumns?.Elements<DocumentFormat.OpenXml.Spreadsheet.TableColumn>()
                    .Select(column => column.Name?.Value ?? string.Empty).ToList() ?? [];
                _tables[name] = new XlsxTable(name, sheetName, range,
                    (int)Math.Min(table.HeaderRowCount?.Value ?? 1, (uint)range.RowCount), (int)Math.Min(table.TotalsRowCount?.Value ?? 0, (uint)range.RowCount), columns,
                    _sorts.Read(table.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.SortState>(), range));
            }
        }
    }

    public void Publish(Workbook workbook)
    {
        foreach (var table in _tables.Values)
        {
            var index = workbook.Sheets.ToList().FindIndex(sheet => string.Equals(sheet.Name, table.SheetName, StringComparison.Ordinal));
            if (index < 0 || workbook.DatabaseRanges.Find(table.Name) is not null) continue;
            workbook.DatabaseRanges.Define(new DatabaseRange
            {
                Name = table.Name,
                SheetIndex = index,
                Range = table.Range,
                HasHeader = table.HeaderRows > 0,
                HasTotalsRow = table.TotalsRows > 0 && table.Range.RowCount > table.HeaderRows,
                Sort = table.Sort,
            });
        }
    }
}
