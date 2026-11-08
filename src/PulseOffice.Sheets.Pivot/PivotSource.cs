using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public interface IPivotSource
{
    IReadOnlyList<string> Headers { get; }

    int RowCount { get; }

    CellValue ValueAt(int row, int column);
}

public sealed class RangePivotSource : IPivotSource
{
    private readonly Worksheet _sheet;
    private readonly CellRange _range;

    public RangePivotSource(Worksheet sheet, CellRange range)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _range = range;

        Headers =
        [
            .. Enumerable.Range(range.Start.Column, range.End.Column - range.Start.Column + 1)
                .Select(column => HeaderOf(sheet, range.Start.Row, column)),
        ];
    }

    public IReadOnlyList<string> Headers { get; }

    public int RowCount => Math.Max(0, Math.Min(_range.End.Row, _sheet.LastUsedRow) - _range.Start.Row);

    public CellValue ValueAt(int row, int column) =>
        _sheet.GetValue(new CellAddress(_range.Start.Row + row + 1, _range.Start.Column + column));

    private static string HeaderOf(Worksheet sheet, int row, int column)
    {
        var value = sheet.GetValue(new CellAddress(row, column));

        return value.IsEmpty
            ? CellAddress.ColumnName(column)
            : PivotValues.KeyOf(value);
    }
}

public static class PivotValues
{
    public static string KeyOf(CellValue value) => value.Kind switch
    {
        ValueKind.Empty => string.Empty,
        ValueKind.Text => value.AsText(),
        ValueKind.Boolean => value.AsBoolean() ? "VERO" : "FALSO",
        ValueKind.Error => value.ErrorText,
        _ => value.AsNumber().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
    };
}
