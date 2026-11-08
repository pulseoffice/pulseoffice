using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public interface ISheetObjectExtent
{
    CellRange Cover(SheetLayout layout, PlacedObject placed);

    CellRange? CoverOfAll(Worksheet sheet);
}

public sealed class SheetObjectExtent : ISheetObjectExtent
{
    public CellRange Cover(SheetLayout layout, PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(placed);

        var lastRow = placed.Anchor.Row;
        var span = placed.OffsetY + placed.Size.Height;

        var used = layout.RowHeight(lastRow);

        while (used.Value < span.Value && lastRow < CellAddress.MaxRows - 1)
        {
            lastRow++;
            used += layout.RowHeight(lastRow);
        }

        var lastColumn = placed.Anchor.Column;
        var width = placed.OffsetX + placed.Size.Width;

        used = layout.ColumnWidth(lastColumn);

        while (used.Value < width.Value && lastColumn < CellAddress.MaxColumns - 1)
        {
            lastColumn++;
            used += layout.ColumnWidth(lastColumn);
        }

        return new CellRange(placed.Anchor, new CellAddress(lastRow, lastColumn));
    }

    public CellRange? CoverOfAll(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        CellRange? total = null;

        foreach (var placed in sheet.Objects)
        {
            var cover = Cover(sheet.Layout, placed);

            total = total is { } current
                ? new CellRange(
                    new CellAddress(
                        Math.Min(current.Start.Row, cover.Start.Row),
                        Math.Min(current.Start.Column, cover.Start.Column)),
                    new CellAddress(
                        Math.Max(current.End.Row, cover.End.Row),
                        Math.Max(current.End.Column, cover.End.Column)))
                : cover;
        }

        return total;
    }
}
