using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation;

public readonly record struct TableTarget(int SheetIndex, CellRange Range);

public interface ITableReferenceResolution
{
    TableTarget? Resolve(Workbook workbook, TableReferenceNode node, SheetCell at);

    FormulaNode? Reference(Workbook workbook, TableReferenceNode node, SheetCell at);

    IReadOnlyList<string> Columns(Worksheet sheet, DatabaseRange table);
}

public sealed class TableReferenceResolution : ITableReferenceResolution
{
    public TableTarget? Resolve(Workbook workbook, TableReferenceNode node, SheetCell at)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(node);
        if (workbook.DatabaseRanges.Find(node.Table) is not { } table
            || table.SheetIndex < 0 || table.SheetIndex >= workbook.Sheets.Count) return null;

        var range = table.Range;
        var headerRows = table.HasHeader ? 1 : 0;
        var totalsRows = table.HasTotalsRow && range.RowCount > headerRows ? 1 : 0;
        var firstData = range.Start.Row + headerRows;
        var lastData = range.End.Row - totalsRows;

        if (Rows(node.Area, range, headerRows, totalsRows, firstData, lastData, table.SheetIndex, at) is not { } rows) return null;
        if (Span(workbook.Sheets[table.SheetIndex], table, node) is not { } span) return null;
        return new TableTarget(table.SheetIndex, new CellRange(new CellAddress(rows.Top, span.Left), new CellAddress(rows.Bottom, span.Right)));
    }

    public FormulaNode? Reference(Workbook workbook, TableReferenceNode node, SheetCell at)
    {
        if (Resolve(workbook, node, at) is not { } target) return null;
        var sheet = workbook.Sheets[target.SheetIndex].Name;
        return target.Range.IsSingleCell
            ? new ReferenceNode(sheet, target.Range.Start, true, true)
            : new RangeNode(sheet, target.Range, true, true, true, true);
    }

    public IReadOnlyList<string> Columns(Worksheet sheet, DatabaseRange table)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(table);
        var columns = new List<string>(table.Range.ColumnCount);
        for (var column = table.Range.Start.Column; column <= table.Range.End.Column; column++)
        {
            var position = column - table.Range.Start.Column + 1;
            var header = table.HasHeader ? sheet[new CellAddress(table.Range.Start.Row, column)].Value : CellValue.Empty;
            columns.Add(header.Kind switch
            {
                ValueKind.Text when header.AsText().Length > 0 => header.AsText(),
                ValueKind.Number => header.AsNumber().ToString(CultureInfo.InvariantCulture),
                ValueKind.Empty or ValueKind.Text => $"Column{position}",
                _ => header.ToString(),
            });
        }
        return columns;
    }

    private (int Top, int Bottom)? Rows(TableArea area, CellRange range, int headerRows, int totalsRows, int firstData, int lastData, int sheetIndex, SheetCell at)
    {
        if (area.HasFlag(TableArea.ThisRow))
            return at.SheetIndex == sheetIndex && at.Address.Row >= firstData && at.Address.Row <= lastData ? (at.Address.Row, at.Address.Row) : null;

        var headers = area.HasFlag(TableArea.Headers) && headerRows > 0;
        var data = area.HasFlag(TableArea.Data);
        var totals = area.HasFlag(TableArea.Totals) && totalsRows > 0;
        if (!headers && !data && !totals) return null;

        var top = headers ? range.Start.Row : data ? firstData : lastData + 1;
        var bottom = totals ? range.End.Row : data ? lastData : range.Start.Row + headerRows - 1;
        return bottom < top ? null : (top, bottom);
    }

    private (int Left, int Right)? Span(Worksheet sheet, DatabaseRange table, TableReferenceNode node)
    {
        if (node.FirstColumn is null) return (table.Range.Start.Column, table.Range.End.Column);
        var columns = Columns(sheet, table);
        var first = IndexOf(columns, node.FirstColumn);
        var last = node.LastColumn is null ? first : IndexOf(columns, node.LastColumn);
        if (first < 0 || last < 0) return null;
        return (table.Range.Start.Column + Math.Min(first, last), table.Range.Start.Column + Math.Max(first, last));
    }

    private int IndexOf(IReadOnlyList<string> columns, string name)
    {
        for (var index = 0; index < columns.Count; index++)
            if (string.Equals(columns[index], name, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }
}
