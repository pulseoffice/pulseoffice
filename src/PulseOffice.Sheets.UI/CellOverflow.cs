using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI;

public readonly record struct OverflowSpan(double Left, double Right);

public interface ICellOverflow
{
    OverflowSpan Span(Worksheet sheet, CellAddress address, CellStyle style, double left, double right, Func<int, double> columnWidth);
}

public sealed class CellOverflow : ICellOverflow
{
    private const int MaxColumns = 32;

    public OverflowSpan Span(Worksheet sheet, CellAddress address, CellStyle style, double left, double right, Func<int, double> columnWidth)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(columnWidth);

        if (style.WrapText || style.RotationDegrees % 360 != 0) return new OverflowSpan(left, right);
        if (style.Horizontal is CellHorizontalAlignment.Fill or CellHorizontalAlignment.Justify) return new OverflowSpan(left, right);
        if (sheet[address].Value.Kind != ValueKind.Text) return new OverflowSpan(left, right);

        var toRight = style.Horizontal != CellHorizontalAlignment.Right;
        var toLeft = style.Horizontal is CellHorizontalAlignment.Right or CellHorizontalAlignment.Center;

        return new OverflowSpan(
            toLeft ? left - Extend(sheet, address, -1, columnWidth) : left,
            toRight ? right + Extend(sheet, address, 1, columnWidth) : right);
    }

    private double Extend(Worksheet sheet, CellAddress address, int direction, Func<int, double> columnWidth)
    {
        var total = 0.0;

        for (var step = 1; step <= MaxColumns; step++)
        {
            var column = address.Column + step * direction;
            if (column < 0 || column >= CellAddress.MaxColumns) break;

            var neighbour = new CellAddress(address.Row, column);
            if (Occupied(sheet, neighbour)) break;

            total += Math.Max(columnWidth(column), 0);
        }

        return total;
    }

    private bool Occupied(Worksheet sheet, CellAddress address)
    {
        if (sheet.Merges.At(address) is not null) return true;
        var cell = sheet[address];
        return !cell.Value.IsEmpty || cell.Formula is not null;
    }
}
