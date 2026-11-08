namespace PulseOffice.Formats;

public static class ImportedTableLimits
{
    public const int TextTableColumns = 1024;

    public const long TextTableCells = 1_000_000;

    public const long DocumentTableCells = 2_000_000;

    public const int SlideTableRows = 1000;

    public const int SlideTableColumns = 1000;

    public const int SlideTableCells = 100_000;
}

public sealed class ImportedCellBudget(long cells)
{
    private long _remaining = cells;

    public bool Exhausted { get; private set; }

    public int Rows(int rows, int cellsPerRow)
    {
        if (rows <= 0) return 0;
        var granted = (int)Math.Min(rows, _remaining / Math.Max(1, cellsPerRow));
        _remaining -= (long)granted * Math.Max(1, cellsPerRow);
        if (granted < rows) Exhausted = true;
        return granted;
    }
}
