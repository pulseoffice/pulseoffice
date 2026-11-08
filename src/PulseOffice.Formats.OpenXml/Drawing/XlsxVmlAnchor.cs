using System.Globalization;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxVmlAnchor(ISheetPlacement placement)
{
    private const int TwipsPerPixel = 15;

    public string Write(SheetLayout layout, PlacedObject placed)
    {
        var end = placement.End(layout, placed);
        int[] values = [placed.Anchor.Column, Pixels(placed.OffsetX), placed.Anchor.Row, Pixels(placed.OffsetY), end.Cell.Column, Pixels(end.X), end.Cell.Row, Pixels(end.Y)];
        return string.Join(", ", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }

    public PlacementBox? Read(SheetLayout layout, string? anchor)
    {
        var parts = (anchor ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 8) return null;
        var values = new int[8];
        for (var index = 0; index < 8; index++)
            if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[index]) || values[index] < 0) return null;
        if (values[0] >= CellAddress.MaxColumns || values[2] >= CellAddress.MaxRows || values[4] >= CellAddress.MaxColumns || values[6] >= CellAddress.MaxRows) return null;
        var left = Twips(layout.ColumnOffset(values[0]).Value + (long)values[1] * TwipsPerPixel);
        var top = Twips(layout.RowOffset(values[2]).Value + (long)values[3] * TwipsPerPixel);
        var right = Twips(layout.ColumnOffset(values[4]).Value + (long)values[5] * TwipsPerPixel);
        var bottom = Twips(layout.RowOffset(values[6]).Value + (long)values[7] * TwipsPerPixel);
        return new PlacementBox(new Twip(left), new Twip(top), new Twip(Math.Max(TwipsPerPixel, right - left)), new Twip(Math.Max(TwipsPerPixel, bottom - top)));
    }

    public PlacedObject Place(SheetLayout layout, PlacedObject seed, PlacementBox box) =>
        placement.Place(layout, new CellAddress(0, 0), seed, box);

    private int Twips(long value) => (int)Math.Clamp(value, 0, int.MaxValue / 2);

    private int Pixels(Twip twips) => (int)Math.Round(twips.Value / (double)TwipsPerPixel);
}
