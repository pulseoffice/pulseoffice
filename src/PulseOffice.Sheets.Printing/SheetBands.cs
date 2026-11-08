using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets;

namespace PulseOffice.Sheets.Printing;

public sealed class ColumnBand(SheetLayout layout, int first, int last, IReadOnlySet<int>? breaks = null)
    : IBandMetrics
{
    public int First => first;

    public int Last => last;

    public Twip SizeOf(int index) => layout.ColumnWidth(index);

    public bool IsHidden(int index) => layout.IsColumnHidden(index);

    public bool BreaksBefore(int index) => breaks?.Contains(index) == true;
}

public sealed class RowBand(SheetLayout layout, int first, int last, IReadOnlySet<int>? breaks = null)
    : IBandMetrics
{
    public int First => first;

    public int Last => last;

    public Twip SizeOf(int index) => layout.RowHeight(index);

    public bool IsHidden(int index) => layout.IsRowHidden(index);

    public bool BreaksBefore(int index) => breaks?.Contains(index) == true;
}
