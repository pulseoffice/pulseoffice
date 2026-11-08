namespace PulseOffice.Documents.Layout.Tree;

public interface ITurnedFrame
{
    LayoutRect Bounds { get; }

    double RotationDegrees { get; }

    LayoutRect RotatedBounds { get; }

    IEnumerable<LineBox> Lines { get; }
}

public sealed class TurnedBox(LayoutRect bounds, double rotationDegrees, IReadOnlyList<LayoutBox> content) : LayoutBox, ITurnedFrame
{
    public override LayoutRect Bounds { get; } = bounds;

    public double RotationDegrees { get; } = rotationDegrees;

    public IReadOnlyList<LayoutBox> Content { get; } = content;

    public LayoutRect RotatedBounds => RotatedRect.Around(Bounds, RotationDegrees);

    public IEnumerable<LineBox> Lines => Content.OfType<LineBox>().Concat(Content.OfType<TableBox>().SelectMany(table => table.Lines));

    public override IReadOnlyList<LayoutBox> Children => Content;

    public override void Accept(ILayoutVisitor visitor)
    {
        visitor.VisitTurn(this);
        foreach (var child in Content) child.Accept(visitor);
        visitor.LeaveTurn(this);
    }

    public override LayoutBox Moved(double dx, double dy) => new TurnedBox(Bounds.Offset(dx, dy), RotationDegrees, [.. Content.Select(child => child.Moved(dx, dy))]);
}
