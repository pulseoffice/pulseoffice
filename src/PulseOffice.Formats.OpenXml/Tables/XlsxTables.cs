using System.Text;
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

internal sealed class StructuredReferenceResolver(XlsxTableCatalog tables)
{
    public string Resolve(string formula, CellAddress cell, string sheetName)
    {
        if (tables.All.Count == 0 || !formula.Contains('[', StringComparison.Ordinal)) return formula;

        var result = new StringBuilder(formula.Length);
        var index = 0;
        while (index < formula.Length)
        {
            var c = formula[index];
            if (c == '"')
            {
                var end = index + 1;
                while (end < formula.Length && !(formula[end] == '"' && (end + 1 >= formula.Length || formula[end + 1] != '"')))
                    end += formula[end] == '"' ? 2 : 1;
                end = Math.Min(end + 1, formula.Length);
                result.Append(formula, index, end - index);
                index = end;
                continue;
            }
            if (IsNameStart(c) && (index == 0 || !IsNamePart(formula[index - 1])))
            {
                var nameEnd = index;
                while (nameEnd < formula.Length && IsNamePart(formula[nameEnd])) nameEnd++;
                if (nameEnd < formula.Length && formula[nameEnd] == '[' && tables.Find(formula[index..nameEnd]) is { } table
                    && Closing(formula, nameEnd) is { } close
                    && Reference(table, formula[(nameEnd + 1)..close], cell, sheetName) is { } reference)
                {
                    result.Append(reference);
                    index = close + 1;
                    continue;
                }
                result.Append(formula, index, nameEnd - index);
                index = nameEnd;
                continue;
            }
            result.Append(c);
            index++;
        }
        return result.ToString();
    }

    private int? Closing(string text, int open)
    {
        var depth = 0;
        for (var index = open; index < text.Length; index++)
        {
            if (text[index] == '\'') { index++; continue; }
            if (text[index] == '[') depth++;
            else if (text[index] == ']' && --depth == 0) return index;
        }
        return null;
    }

    private string? Reference(XlsxTable table, string specifier, CellAddress cell, string sheetName)
    {
        var items = Items(specifier.Trim());
        if (items is null) return null;

        var specials = items.Where(item => item.Special).Select(item => item.Text.ToUpperInvariant()).ToList();
        var columns = items.Where(item => !item.Special).ToList();

        int top, bottom;
        if (specials.Contains("#THIS ROW")) top = bottom = cell.Row;
        else if (specials.Contains("#ALL")) (top, bottom) = (table.Range.Start.Row, table.Range.End.Row);
        else
        {
            var headers = specials.Contains("#HEADERS");
            var totals = specials.Contains("#TOTALS");
            var data = specials.Contains("#DATA") || (!headers && !totals);
            top = headers ? table.Range.Start.Row : data ? table.FirstDataRow : table.LastDataRow + 1;
            bottom = totals ? table.Range.End.Row : data ? table.LastDataRow : table.Range.Start.Row + table.HeaderRows - 1;
        }
        if (bottom < top) return null;

        int left, right;
        if (columns.Count == 0) (left, right) = (table.Range.Start.Column, table.Range.End.Column);
        else
        {
            if (table.ColumnOf(columns[0].Text) is not { } first || table.ColumnOf(columns[^1].Text) is not { } last) return null;
            (left, right) = (Math.Min(first, last), Math.Max(first, last));
        }

        var start = Absolute(new CellAddress(top, left));
        var end = Absolute(new CellAddress(bottom, right));
        var body = start == end ? start : start + ":" + end;
        return string.Equals(table.SheetName, sheetName, StringComparison.Ordinal) ? body : Qualified(table.SheetName) + "!" + body;
    }

    private List<(string Text, bool Special)>? Items(string specifier)
    {
        if (specifier.Length == 0) return [];
        if (specifier[0] == '@')
        {
            var rest = specifier[1..].Trim();
            var column = rest.StartsWith('[') && rest.EndsWith(']') ? Unescape(rest[1..^1]) : Unescape(rest);
            return column.Length == 0 ? [("#This Row", true)] : [("#This Row", true), (column, false)];
        }
        if (specifier[0] != '[') return [(Unescape(specifier), specifier[0] == '#')];

        var items = new List<(string, bool)>();
        var index = 0;
        while (index < specifier.Length)
        {
            var c = specifier[index];
            if (c is ',' or ':' or ' ') { index++; continue; }
            if (c != '[') return null;
            var close = Closing(specifier, index);
            if (close is not { } end) return null;
            var text = Unescape(specifier[(index + 1)..end]);
            items.Add((text, text.StartsWith('#')));
            index = end + 1;
        }
        return items;
    }

    private string Unescape(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\'' && index + 1 < text.Length) index++;
            builder.Append(text[index]);
        }
        return builder.ToString();
    }

    private string Absolute(CellAddress address)
    {
        var a1 = address.ToA1();
        var digits = a1.IndexOfAny("0123456789".ToCharArray());
        return "$" + a1[..digits] + "$" + a1[digits..];
    }

    private string Qualified(string sheet) =>
        sheet.Any(c => !char.IsLetterOrDigit(c) && c != '_') ? "'" + sheet.Replace("'", "''", StringComparison.Ordinal) + "'" : sheet;

    private bool IsNameStart(char c) => char.IsLetter(c) || c is '_' or '\\';

    private bool IsNamePart(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '\\';
}
