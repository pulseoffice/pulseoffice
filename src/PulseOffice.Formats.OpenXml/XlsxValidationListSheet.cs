using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using Book = PulseOffice.Sheets.Workbook;
using SheetEntry = DocumentFormat.OpenXml.Spreadsheet.Sheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed partial class XlsxValidationListSheet
{
    public const string BaseName = "PulseOffice Lists";

    private readonly List<IReadOnlyList<string>> _placed = [];
    private readonly Dictionary<int, SortedDictionary<int, string>> _columns = [];
    private string _name = BaseName;
    private string? _readName;

    public bool HasLists => _placed.Count > 0;

    public string Name => _name;

    public void NameFor(Book workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        _name = BaseName;
        for (var suffix = 2; workbook.FindSheet(_name) is not null; suffix++)
            _name = string.Create(CultureInfo.InvariantCulture, $"{BaseName} {suffix}");
    }

    public bool Needs(IReadOnlyList<string> values, string literal) =>
        values.Count > 0 && (literal.Length - 2 > XlsxLongValidationLists.FormulaLimit || values.Any(value => value.Contains(',', StringComparison.Ordinal)));

    public string Place(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _placed.Add(values);
        var column = CellAddress.ColumnName(_placed.Count - 1);
        return string.Create(CultureInfo.InvariantCulture, $"{Quoted(_name)}!${column}$1:${column}${values.Count}");
    }

    public void Write(WorkbookPart workbookPart, DocumentFormat.OpenXml.Spreadsheet.Sheets sheets, uint sheetId)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(sheets);
        if (!HasLists) return;

        var part = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        var rows = _placed.Max(list => list.Count);
        for (var row = 0; row < rows; row++)
        {
            var line = new Row { RowIndex = (uint)(row + 1) };
            for (var column = 0; column < _placed.Count; column++)
            {
                if (row >= _placed[column].Count) continue;
                line.AppendChild(new Cell
                {
                    CellReference = new CellAddress(row, column).ToA1(),
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(_placed[column][row]) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }),
                });
            }
            data.AppendChild(line);
        }
        part.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(data);
        sheets.AppendChild(new SheetEntry { Id = workbookPart.GetIdOfPart(part), SheetId = sheetId, Name = _name, State = SheetStateValues.VeryHidden });
    }

    public bool IsHelper(SheetEntry sheet) =>
        sheet.State?.Value == SheetStateValues.VeryHidden
        && sheet.Name?.Value is { } name && name.StartsWith(BaseName, StringComparison.Ordinal);

    public void Load(WorkbookPart workbookPart, IReadOnlyList<string> strings)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(strings);
        var helper = workbookPart.Workbook?.Sheets?.Elements<SheetEntry>().FirstOrDefault(IsHelper);
        if (helper?.Id?.Value is not { } id || !workbookPart.TryGetPartById(id, out var found) || found is not WorksheetPart part) return;

        _readName = helper.Name!.Value;
        foreach (var cell in part.Worksheet?.Descendants<Cell>() ?? [])
        {
            if (cell.CellReference?.Value is not { } reference || !CellAddress.TryParseA1(reference, out var address)) continue;
            var text = CellText(cell, strings);
            if (text is null) continue;
            if (!_columns.TryGetValue(address.Column, out var column)) _columns[address.Column] = column = [];
            column[address.Row] = text;
        }
    }

    public IReadOnlyList<string>? Resolve(string? formula)
    {
        if (_readName is null || formula is null) return null;
        var match = ReferencePattern().Match(formula.Trim().TrimStart('='));
        if (!match.Success) return null;
        var sheet = match.Groups["quoted"].Success ? match.Groups["quoted"].Value.Replace("''", "'", StringComparison.Ordinal) : match.Groups["bare"].Value;
        if (!string.Equals(sheet, _readName, StringComparison.OrdinalIgnoreCase)) return null;
        if (!CellAddress.TryParseA1(match.Groups["first"].Value.Replace("$", string.Empty, StringComparison.Ordinal), out var first)
            || !CellAddress.TryParseA1(match.Groups["last"].Value.Replace("$", string.Empty, StringComparison.Ordinal), out var last)
            || first.Column != last.Column) return null;
        if (!_columns.TryGetValue(first.Column, out var column)) return [];
        return [.. column.Where(entry => entry.Key >= first.Row && entry.Key <= last.Row).Select(entry => entry.Value)];
    }

    private static string? CellText(Cell cell, IReadOnlyList<string> strings)
    {
        if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText;
        if (cell.DataType?.Value == CellValues.SharedString)
            return int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < strings.Count ? strings[index] : null;
        return cell.CellValue?.Text;
    }

    private static string Quoted(string name) => $"'{name.Replace("'", "''", StringComparison.Ordinal)}'";

    [GeneratedRegex(@"^(?:'(?<quoted>(?:[^']|'')+)'|(?<bare>[^!']+))!(?<first>\$?[A-Za-z]{1,3}\$?\d+):(?<last>\$?[A-Za-z]{1,3}\$?\d+)$")]
    private static partial Regex ReferencePattern();
}
