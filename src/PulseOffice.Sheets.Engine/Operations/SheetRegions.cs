using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface ISheetRegions
{
    CellRange CurrentRegion(Worksheet sheet, CellAddress address);
}

public sealed class SheetRegions : ISheetRegions
{
    public CellRange CurrentRegion(Worksheet sheet, CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet[address] == Cell.Blank) return CellRange.Single(address);

        var top = address.Row;
        var bottom = address.Row;
        var left = address.Column;
        var right = address.Column;

        var grown = true;

        while (grown)
        {
            grown = false;

            while (top > 0 && RowHasContent(sheet, top - 1, Math.Max(left - 1, 0), Math.Min(right + 1, CellAddress.MaxColumns - 1)))
            {
                top--;
                grown = true;
            }

            while (bottom < CellAddress.MaxRows - 1
                && RowHasContent(sheet, bottom + 1, Math.Max(left - 1, 0), Math.Min(right + 1, CellAddress.MaxColumns - 1)))
            {
                bottom++;
                grown = true;
            }

            while (left > 0 && ColumnHasContent(sheet, left - 1, Math.Max(top - 1, 0), Math.Min(bottom + 1, CellAddress.MaxRows - 1)))
            {
                left--;
                grown = true;
            }

            while (right < CellAddress.MaxColumns - 1
                && ColumnHasContent(sheet, right + 1, Math.Max(top - 1, 0), Math.Min(bottom + 1, CellAddress.MaxRows - 1)))
            {
                right++;
                grown = true;
            }
        }

        return new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
    }

    private bool RowHasContent(Worksheet sheet, int row, int left, int right)
    {
        for (var column = left; column <= right; column++)
            if (sheet[new CellAddress(row, column)] != Cell.Blank)
                return true;

        return false;
    }

    private bool ColumnHasContent(Worksheet sheet, int column, int top, int bottom)
    {
        for (var row = top; row <= bottom; row++)
            if (sheet[new CellAddress(row, column)] != Cell.Blank)
                return true;

        return false;
    }
}
