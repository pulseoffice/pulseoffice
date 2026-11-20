using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class ChartStructureOffsets(StructureChange change)
{
    public StructureAxis Axis => change.Axis;

    public bool Affects(string sheet, CellRange range) =>
        string.Equals(sheet, change.SheetName, StringComparison.CurrentCultureIgnoreCase) && change.InBand(range.Start) && change.InBand(range.End);

    public int Position(CellAddress address) => change.Axis == StructureAxis.Rows ? address.Row : address.Column;

    public int? Moved(int start, int position)
    {
        if (change.IsInsertion)
        {
            var newStart = start >= change.Index ? start + change.Count : start;
            var newPosition = position >= change.Index ? position + change.Count : position;
            return newPosition - newStart;
        }

        var last = change.Index + change.Count - 1;
        if (position >= change.Index && position <= last) return null;
        var shiftedStart = start > last ? start - change.Count : Math.Min(start, change.Index);
        var shiftedPosition = position > last ? position - change.Count : position;
        return shiftedPosition - shiftedStart;
    }
}
