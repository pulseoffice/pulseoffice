using Avalonia;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public interface ITableCellBox
{
    Rect Inner(Rect area, TableCellMargins? margins);
}

public sealed class TableCellBox : ITableCellBox
{
    private const double SidePadding = 6;
    private const double EdgePadding = 3;

    public Rect Inner(Rect area, TableCellMargins? margins)
    {
        var (left, right, top, bottom) = margins is null
            ? (SidePadding, SidePadding, EdgePadding, EdgePadding)
            : (Math.Max(0, margins.Left), Math.Max(0, margins.Right), Math.Max(0, margins.Top), Math.Max(0, margins.Bottom));
        var x = area.X + Math.Min(left, Math.Max(0, area.Width - 1));
        var y = area.Y + Math.Min(top, Math.Max(0, area.Height - 1));
        return new Rect(x, y, Math.Max(1, area.Width - left - right), Math.Max(1, area.Height - top - bottom));
    }
}
