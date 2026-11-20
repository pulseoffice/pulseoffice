using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IWorkbookMatchOrder
{
    WorkbookMatch? Next(IReadOnlyList<WorkbookMatch> matches, int sheetIndex, CellAddress from, bool byColumns, bool backwards);
}

public sealed class WorkbookMatchOrder : IWorkbookMatchOrder
{
    public WorkbookMatch? Next(IReadOnlyList<WorkbookMatch> matches, int sheetIndex, CellAddress from, bool byColumns, bool backwards)
    {
        ArgumentNullException.ThrowIfNull(matches);
        if (matches.Count == 0) return null;
        var ordered = matches.OrderBy(match => Key(match.SheetIndex, match.Address, byColumns)).ToList();
        var current = Key(sheetIndex, from, byColumns);
        return backwards
            ? ordered.LastOrDefault(match => Key(match.SheetIndex, match.Address, byColumns).CompareTo(current) < 0) ?? ordered[^1]
            : ordered.FirstOrDefault(match => Key(match.SheetIndex, match.Address, byColumns).CompareTo(current) > 0) ?? ordered[0];
    }

    private (int Sheet, int Major, int Minor) Key(int sheet, CellAddress address, bool byColumns) =>
        byColumns ? (sheet, address.Column, address.Row) : (sheet, address.Row, address.Column);
}
