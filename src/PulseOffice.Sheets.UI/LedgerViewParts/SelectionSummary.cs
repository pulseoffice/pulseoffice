using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

public sealed record SelectionTotals(int Filled, int Numbers, double Sum)
{
    public double Average => Numbers == 0 ? 0 : Sum / Numbers;
}

public interface ISelectionSummary
{
    SelectionTotals Summarize(Worksheet sheet, CellRange range);
}

public sealed class SelectionSummary : ISelectionSummary
{
    public SelectionTotals Summarize(Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var filled = 0;
        var numbers = 0;
        var sum = 0d;

        foreach (var (_, cell) in sheet.NonEmptyCellsIn(range))
        {
            if (cell.Value.IsEmpty) continue;

            filled++;
            if (cell.Value.Kind != ValueKind.Number) continue;

            numbers++;
            sum += cell.Value.AsNumber();
        }

        return new SelectionTotals(filled, numbers, sum);
    }
}
