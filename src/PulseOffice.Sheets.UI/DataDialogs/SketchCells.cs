using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI.DataDialogs;

public interface ISketchCells
{
    ITextCatalog Texts { get; }

    string Shown(CellValue value);

    SketchCell Cell(CellValue value, bool header = false, bool marked = false);

    SketchPreview From(
        IReadOnlyDictionary<CellAddress, CellValue> values, CellAddress origin, bool headerRow, bool headerColumn, string summaryKey);

    IReadOnlyList<IReadOnlyList<SketchCell>> Read(Worksheet sheet, CellRange range, bool headerRow, Func<CellAddress, bool>? marked = null);
}

public sealed class SketchCells(ITextCatalog texts) : ISketchCells
{
    public const int Rows = 6;

    public const int Columns = 4;

    public ITextCatalog Texts { get; } = texts;

    public string Shown(CellValue value) =>
        value.IsEmpty ? string.Empty
        : value.Kind == ValueKind.Number && value.TryCoerceToNumber(out var number) ? number.ToString("G6", CultureInfo.CurrentCulture)
        : value.ToString();

    public SketchCell Cell(CellValue value, bool header = false, bool marked = false) =>
        new(Shown(value), header, marked, value.Kind == ValueKind.Number);

    public SketchPreview From(
        IReadOnlyDictionary<CellAddress, CellValue> values, CellAddress origin, bool headerRow, bool headerColumn, string summaryKey)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0) return new SketchPreview([], Texts["status.tools.nothing"]);
        var top = values.Keys.Min(address => address.Row);
        var left = values.Keys.Min(address => address.Column);
        var bottom = values.Keys.Max(address => address.Row);
        var right = values.Keys.Max(address => address.Column);
        var rows = new List<IReadOnlyList<SketchCell>>();
        for (var row = top; row <= Math.Min(bottom, top + Rows - 1); row++)
        {
            var line = new List<SketchCell>();
            for (var column = left; column <= Math.Min(right, left + Columns - 1); column++)
                line.Add(Cell(values.GetValueOrDefault(new CellAddress(row, column), CellValue.Empty),
                    (headerRow && row == top) || (headerColumn && column == left)));
            rows.Add(line);
        }

        var area = new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
        return new SketchPreview(rows, null, Texts.Format(summaryKey, bottom - top + 1, right - left + 1, area.ToString()));
    }

    public IReadOnlyList<IReadOnlyList<SketchCell>> Read(Worksheet sheet, CellRange range, bool headerRow, Func<CellAddress, bool>? marked = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var rows = new List<IReadOnlyList<SketchCell>>();
        for (var row = range.Start.Row; row <= Math.Min(range.End.Row, range.Start.Row + Rows - 1); row++)
        {
            var line = new List<SketchCell>();
            for (var column = range.Start.Column; column <= Math.Min(range.End.Column, range.Start.Column + Columns - 1); column++)
            {
                var address = new CellAddress(row, column);
                line.Add(Cell(sheet[address].Value, headerRow && row == range.Start.Row, marked?.Invoke(address) ?? false));
            }

            rows.Add(line);
        }

        return rows;
    }
}
