using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI;

public readonly record struct VisibleRange(int FirstRow, int LastRow, int FirstColumn, int LastColumn)
{
    public int RowCount => LastRow - FirstRow + 1;
    public int ColumnCount => LastColumn - FirstColumn + 1;

    public bool Contains(CellAddress address) =>
        address.Row >= FirstRow && address.Row <= LastRow
        && address.Column >= FirstColumn && address.Column <= LastColumn;
}

public sealed class GridGeometry(SheetLayout layout)
{
    private const double PixelsPerTwip = 96.0 / Twip.PerInch;

    public GridGeometry() : this(new SheetLayout())
    {
    }

    public SheetLayout Layout { get; } = layout ?? throw new ArgumentNullException(nameof(layout));

    public static double ToPixels(Twip value) => value.Value * PixelsPerTwip;

    public static Twip ToTwips(double pixels) => new((int)Math.Round(pixels / PixelsPerTwip));

    public double DefaultColumnWidth
    {
        get => ToPixels(Layout.DefaultColumnWidth);
        init => Layout.DefaultColumnWidth = ToTwips(value);
    }

    public double DefaultRowHeight
    {
        get => ToPixels(Layout.DefaultRowHeight);
        init => Layout.DefaultRowHeight = ToTwips(value);
    }

    public double RowHeaderWidth { get; set; } = 44;

    public double ColumnHeaderHeight { get; set; } = 20;

    public double RowGutterWidth { get; set; }

    public double ColumnGutterHeight { get; set; }

    public int FrozenRows { get; set; }

    public int FrozenColumns { get; set; }

    public bool PanesAreSplit { get; set; }

    public bool HasFrozenPanes => FrozenRows > 0 || FrozenColumns > 0;

    public double FrozenWidth => FrozenColumns == 0 ? 0 : ColumnOffset(FrozenColumns);

    public double FrozenHeight => FrozenRows == 0 ? 0 : RowOffset(FrozenRows);

    public double ColumnWidth(int column) => ToPixels(Layout.ColumnWidth(column));

    public double RowHeight(int row) => ToPixels(Layout.RowHeight(row));

    public void SetColumnWidth(int column, double width) => Layout.SetColumnWidth(column, ToTwips(width));

    public void SetRowHeight(int row, double height) => Layout.SetRowHeight(row, ToTwips(height));

    public bool IsColumnHidden(int column) => Layout.IsColumnHidden(column);
    public bool IsRowHidden(int row) => Layout.IsRowHidden(row);

    public double ColumnOffset(int column) => ToPixels(Layout.ColumnOffset(column));

    public double RowOffset(int row) => ToPixels(Layout.RowOffset(row));

    public int ColumnAt(double x) => x < 0 ? -1 : IndexAt(x, CellAddress.MaxColumns, ColumnOffset);

    public int RowAt(double y) => y < 0 ? -1 : IndexAt(y, CellAddress.MaxRows, RowOffset);

    private static int IndexAt(double position, int count, Func<int, double> offset)
    {
        var low = 0;
        var high = count - 1;

        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (offset(middle + 1) > position) high = middle;
            else low = middle + 1;
        }

        return low;
    }

    public VisibleRange VisibleCells(double scrollX, double scrollY, double viewportWidth, double viewportHeight)
    {
        var firstColumn = Math.Max(ColumnAt(scrollX + FrozenWidth), FrozenColumns);
        var firstRow = Math.Max(RowAt(scrollY + FrozenHeight), FrozenRows);

        var lastColumn = firstColumn;
        var x = ColumnOffset(firstColumn);
        while (lastColumn < CellAddress.MaxColumns - 1 && x < scrollX + FrozenWidth + viewportWidth)
        {
            x += ColumnWidth(lastColumn);
            lastColumn++;
        }

        var lastRow = firstRow;
        var y = RowOffset(firstRow);
        while (lastRow < CellAddress.MaxRows - 1 && y < scrollY + FrozenHeight + viewportHeight)
        {
            y += RowHeight(lastRow);
            lastRow++;
        }

        return new VisibleRange(firstRow, lastRow, firstColumn, lastColumn);
    }

    public int ColumnBorderAt(double x, double tolerance = 3)
    {
        var column = ColumnAt(Math.Max(x - tolerance, 0));
        if (column < 0) return -1;

        var right = ColumnOffset(column) + ColumnWidth(column);
        return Math.Abs(right - x) <= tolerance ? column : -1;
    }

    public int RowBorderAt(double y, double tolerance = 3)
    {
        var row = RowAt(Math.Max(y - tolerance, 0));
        if (row < 0) return -1;

        var bottom = RowOffset(row) + RowHeight(row);
        return Math.Abs(bottom - y) <= tolerance ? row : -1;
    }

    public (double X, double Y, double Width, double Height) CellBounds(CellAddress address) =>
    (ColumnOffset(address.Column), RowOffset(address.Row),
     ColumnWidth(address.Column), RowHeight(address.Row));

    public (double Width, double Height) Extent(CellRange? used)
    {
        const int Margin = 50;

        var columns = Math.Min((used?.End.Column ?? 0) + Margin, CellAddress.MaxColumns - 1);
        var rows = Math.Min((used?.End.Row ?? 0) + Margin, CellAddress.MaxRows - 1);

        return (ColumnOffset(columns) + ColumnWidth(columns), RowOffset(rows) + RowHeight(rows));
    }
}
