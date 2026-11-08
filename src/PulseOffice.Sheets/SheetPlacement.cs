using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public readonly record struct PlacementBox(Twip Left, Twip Top, Twip Width, Twip Height)
{
    public Twip Right => Left + Width;
    public Twip Bottom => Top + Height;
}

public readonly record struct CellPoint(CellAddress Cell, Twip X, Twip Y);

public interface ISheetPlacement
{
    CellPoint End(SheetLayout layout, PlacedObject placed);

    PlacementBox Box(SheetLayout layout, CellAddress origin, PlacedObject placed);

    PlacedObject Place(SheetLayout layout, CellAddress origin, PlacedObject placed, PlacementBox box);

    Twip RowDistance(SheetLayout layout, int from, int to);

    Twip ColumnDistance(SheetLayout layout, int from, int to);
}

public sealed class SheetPlacement : ISheetPlacement
{
    public Twip RowDistance(SheetLayout layout, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (from == to) return Twip.Zero;

        var sign = to > from ? 1 : -1;
        var total = 0;

        for (var row = Math.Min(from, to); row < Math.Max(from, to); row++)
            total += layout.RowHeight(row).Value;

        return new Twip(total * sign);
    }

    public Twip ColumnDistance(SheetLayout layout, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (from == to) return Twip.Zero;

        var sign = to > from ? 1 : -1;
        var total = 0;

        for (var column = Math.Min(from, to); column < Math.Max(from, to); column++)
            total += layout.ColumnWidth(column).Value;

        return new Twip(total * sign);
    }

    public CellPoint End(SheetLayout layout, PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(placed);

        var end = Place(layout, placed.Anchor, placed, new PlacementBox(
            placed.OffsetX + placed.Size.Width, placed.OffsetY + placed.Size.Height, Twip.Zero, Twip.Zero));
        return new CellPoint(end.Anchor, end.OffsetX, end.OffsetY);
    }

    public PlacementBox Box(SheetLayout layout, CellAddress origin, PlacedObject placed)
    {
        ArgumentNullException.ThrowIfNull(placed);

        return new PlacementBox(
            ColumnDistance(layout, origin.Column, placed.Anchor.Column) + placed.OffsetX,
            RowDistance(layout, origin.Row, placed.Anchor.Row) + placed.OffsetY,
            placed.Size.Width,
            placed.Size.Height);
    }

    public PlacedObject Place(SheetLayout layout, CellAddress origin, PlacedObject placed, PlacementBox box)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(placed);

        var row = origin.Row;
        var top = box.Top.Value;

        while (top >= layout.RowHeight(row).Value && row < CellAddress.MaxRows - 1)
        {
            top -= layout.RowHeight(row).Value;
            row++;
        }

        var column = origin.Column;
        var left = box.Left.Value;

        while (left >= layout.ColumnWidth(column).Value && column < CellAddress.MaxColumns - 1)
        {
            left -= layout.ColumnWidth(column).Value;
            column++;
        }

        return placed with
        {
            Anchor = new CellAddress(row, column),
            OffsetX = new Twip(Math.Max(0, left)),
            OffsetY = new Twip(Math.Max(0, top)),
            Size = new DocumentSize(box.Width, box.Height),
        };
    }
}
