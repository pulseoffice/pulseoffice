using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OccupiedCells
{
    public CellRange Clip(CellRange range, PulseOffice.Sheets.Worksheet sheet) =>
        new(range.Start, new CellAddress(
            Math.Min(range.End.Row, Math.Max(range.Start.Row, sheet.LastUsedRow)),
            Math.Min(range.End.Column, Math.Max(range.Start.Column, sheet.LastUsedColumn))));
}
