using PulseOffice.Foundation.Pages;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Printing;

public sealed record SheetPrintOptions
{
    public required PageSetup Setup { get; init; }

    public CellRange? Area { get; init; }

    public int RepeatedRows { get; init; }

    public int RepeatedColumns { get; init; }

    public BandSlice? RepeatedRowRange { get; init; }

    public BandSlice? RepeatedColumnRange { get; init; }

    public IReadOnlySet<int>? RowBreaks { get; init; }

    public IReadOnlySet<int>? ColumnBreaks { get; init; }

    public string DocumentName { get; init; } = string.Empty;
}
