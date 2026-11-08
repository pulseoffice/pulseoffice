namespace PulseOffice.Document.Borders;

public sealed record CellPlacement(bool FirstRow, bool LastRow, bool FirstColumn, bool LastColumn);

public sealed record BorderEdit(bool Changes, BorderStroke? Stroke)
{
    public static readonly BorderEdit Keep = new(false, null);

    public T Apply<T>(T current, Func<BorderStroke?, T> convert) => Changes ? convert(Stroke) : current;
}

public sealed record CellBorderEdits(BorderEdit Top, BorderEdit Bottom, BorderEdit Left, BorderEdit Right, BorderEdit DiagonalDown, BorderEdit DiagonalUp)
{
    public bool IsEmpty => !Top.Changes && !Bottom.Changes && !Left.Changes && !Right.Changes && !DiagonalDown.Changes && !DiagonalUp.Changes;
}

public interface IBorderFrameResolver
{
    CellBorderEdits Resolve(BorderFrame before, BorderFrame after, CellPlacement placement);

    CellPlacement Place(int row, int column, int firstRow, int firstColumn, int lastRow, int lastColumn);
}

public sealed class BorderFrameResolver : IBorderFrameResolver
{
    public CellBorderEdits Resolve(BorderFrame before, BorderFrame after, CellPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(placement);
        return new CellBorderEdits(
            Edit(before, after, placement.FirstRow ? BorderPosition.Top : BorderPosition.InsideHorizontal),
            Edit(before, after, placement.LastRow ? BorderPosition.Bottom : BorderPosition.InsideHorizontal),
            Edit(before, after, placement.FirstColumn ? BorderPosition.Left : BorderPosition.InsideVertical),
            Edit(before, after, placement.LastColumn ? BorderPosition.Right : BorderPosition.InsideVertical),
            Edit(before, after, BorderPosition.DiagonalDown),
            Edit(before, after, BorderPosition.DiagonalUp));
    }

    public CellPlacement Place(int row, int column, int firstRow, int firstColumn, int lastRow, int lastColumn) =>
        new(row <= firstRow, row >= lastRow, column <= firstColumn, column >= lastColumn);

    private BorderEdit Edit(BorderFrame before, BorderFrame after, BorderPosition position)
    {
        var wanted = after.Stroke(position);
        return wanted == before.Stroke(position) ? BorderEdit.Keep : new BorderEdit(true, wanted);
    }
}
