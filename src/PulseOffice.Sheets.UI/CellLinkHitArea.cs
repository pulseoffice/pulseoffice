using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI;

public sealed record CellTextSpan(double Left, double Right)
{
    public bool Contains(double x) => x >= Left && x <= Right;
}

public interface ICellLinkHitArea
{
    CellTextSpan Span(double cellLeft, double cellRight, double textWidth, double padding, CellHorizontalAlignment alignment, bool alignsRight, bool wraps);

    CellTextSpan Spilled(double cellLeft, double cellRight, OverflowSpan spill, double textWidth, double padding, CellHorizontalAlignment alignment);
}

public sealed class CellLinkHitArea : ICellLinkHitArea
{
    public CellTextSpan Span(double cellLeft, double cellRight, double textWidth, double padding, CellHorizontalAlignment alignment, bool alignsRight, bool wraps)
    {
        var width = Math.Max(cellRight - cellLeft, 0);
        if (wraps || textWidth >= width - padding * 2) return new CellTextSpan(cellLeft, cellRight);
        var right = alignment switch
        {
            CellHorizontalAlignment.Right => true,
            CellHorizontalAlignment.General => alignsRight,
            _ => false,
        };
        var left = alignment == CellHorizontalAlignment.Center
            ? cellLeft + (width - textWidth) / 2
            : right ? cellRight - padding - textWidth : cellLeft + padding;
        return new CellTextSpan(left, left + textWidth);
    }

    public CellTextSpan Spilled(double cellLeft, double cellRight, OverflowSpan spill, double textWidth, double padding, CellHorizontalAlignment alignment)
    {
        var left = alignment switch
        {
            CellHorizontalAlignment.Center => cellLeft + (cellRight - cellLeft - textWidth) / 2,
            CellHorizontalAlignment.Right => cellRight - padding - textWidth,
            _ => cellLeft + padding,
        };
        return new CellTextSpan(Math.Max(left, spill.Left), Math.Min(left + textWidth, spill.Right));
    }
}

public interface ICellLinkClicking
{
    bool FollowsOnClick { get; }
}

public sealed class FixedCellLinkClicking(bool followsOnClick) : ICellLinkClicking
{
    public bool FollowsOnClick => followsOnClick;
}
