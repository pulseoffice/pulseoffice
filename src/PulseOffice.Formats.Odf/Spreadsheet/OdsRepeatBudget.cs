using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsRepeatBudget(List<Diagnostic> diagnostics)
{
    public const long CellCopies = 4L * 1024 * 1024;

    private long _cells = CellCopies;
    private bool _reported;

    public int Rows(int index, int repeat) => (int)Math.Clamp((long)CellAddress.MaxRows - index, 0, repeat);

    public int Columns(int index, int repeat) => (int)Math.Clamp((long)CellAddress.MaxColumns - index, 0, repeat);

    public int TakeCellRows(int rows, int cellsPerRow) => Take(ref _cells, rows, cellsPerRow);

    private int Take(ref long remaining, int count, int unit)
    {
        if (count <= 0 || unit <= 0) return Math.Max(count, 0);
        var granted = (int)Math.Min(count, remaining / unit);
        remaining -= (long)granted * unit;
        if (granted < count) Report();
        return granted;
    }

    private void Report()
    {
        if (_reported) return;
        _reported = true;
        diagnostics.Add(Diagnostic.Warning("ODF270",
            $"Repeated rows, columns or cells expand beyond {CellCopies} cells: the excess was not loaded."));
    }
}
