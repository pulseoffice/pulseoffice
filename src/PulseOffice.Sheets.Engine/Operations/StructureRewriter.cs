using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Operations;

public enum StructureAxis { Rows, Columns }

public sealed record StructureChange(
    StructureAxis Axis, int Index, int Count, bool IsInsertion, string SheetName, int? BandFirst = null, int? BandLast = null)
{
    public bool InBand(CellAddress address)
    {
        if (BandFirst is not { } first || BandLast is not { } last) return true;
        var across = Axis == StructureAxis.Rows ? address.Column : address.Row;
        return across >= first && across <= last;
    }
}

public sealed class StructureRewriter(StructureChange change, string formulaSheet) : FormulaRewriter
{
    private bool AppliesTo(string? sheetName) =>
        string.Equals(sheetName ?? formulaSheet, change.SheetName, StringComparison.OrdinalIgnoreCase);

    public override FormulaNode VisitReference(ReferenceNode node)
    {
        if (!AppliesTo(node.SheetName) || !change.InBand(node.Address)) return node;

        var position = Position(node.Address);
        var moved = MovePosition(position);

        return moved is null || moved.Value >= Limit ? Broken() : node with { Address = Rebuild(node.Address, moved.Value) };
    }

    public override FormulaNode VisitSpillReference(SpillReferenceNode node)
    {
        if (!AppliesTo(node.SheetName) || !change.InBand(node.Anchor)) return node;
        var moved = MovePosition(Position(node.Anchor));
        return moved is null || moved.Value >= Limit ? Broken() : node with { Anchor = Rebuild(node.Anchor, moved.Value) };
    }

    public override FormulaNode VisitRange(RangeNode node)
    {
        if (!AppliesTo(node.SheetName) || !change.InBand(node.Range.Start) || !change.InBand(node.Range.End)) return node;
        if (change.Axis == StructureAxis.Rows ? node.Range.IsWholeColumns : node.Range.IsWholeRows) return node;

        var start = Position(node.Range.Start);
        var end = Position(node.Range.End);

        if (change.IsInsertion)
        {
            var newStart = start >= change.Index ? start + change.Count : start;
            var newEnd = Math.Min(end >= change.Index ? end + change.Count : end, Limit - 1);
            if (newStart >= Limit) return Broken();

            return node with { Range = Rebuild(node.Range, newStart, newEnd) };
        }

        var last = change.Index + change.Count - 1;

        if (start >= change.Index && end <= last) return Broken();

        var shiftedStart = start > last ? start - change.Count : Math.Min(start, change.Index);
        var shiftedEnd = end > last ? end - change.Count : end < change.Index ? end : change.Index - 1;

        if (shiftedEnd < shiftedStart) return Broken();

        return node with { Range = Rebuild(node.Range, shiftedStart, shiftedEnd) };
    }

    private int Limit => change.Axis == StructureAxis.Rows ? CellAddress.MaxRows : CellAddress.MaxColumns;

    private int Position(CellAddress address) =>
        change.Axis == StructureAxis.Rows ? address.Row : address.Column;

    private int? MovePosition(int position)
    {
        if (change.IsInsertion)
            return position >= change.Index ? position + change.Count : position;

        if (position < change.Index) return position;
        if (position < change.Index + change.Count) return null;

        return position - change.Count;
    }

    private CellAddress Rebuild(CellAddress address, int position) =>
        change.Axis == StructureAxis.Rows
            ? new CellAddress(position, address.Column)
            : new CellAddress(address.Row, position);

    private CellRange Rebuild(CellRange range, int start, int end) =>
        new(Rebuild(range.Start, start), Rebuild(range.End, end));
}
