namespace PulseOffice.Document.Forms;

public sealed class ListBoxLayout
{
    public double RowHeight { get; } = 18;

    public double Inset { get; } = 2;

    public int VisibleRows(double height) => (int)Math.Max(0, (height - (2 * Inset)) / RowHeight);

    public double RowTop(int row) => Inset + (row * RowHeight);

    public int RowAt(double offset, double height)
    {
        if (offset < Inset) return -1;

        var row = (int)Math.Floor((offset - Inset) / RowHeight);
        return row < VisibleRows(height) ? row : -1;
    }
}
