namespace PulseOffice.Drawing.Shapes;

public interface IArrowGeometry
{
    ShapeOutline Head(ArrowEnd kind, ShapePoint tip, ShapePoint from, double size);
}

public sealed class ArrowGeometry : IArrowGeometry
{
    public ShapeOutline Head(ArrowEnd kind, ShapePoint tip, ShapePoint from, double size)
    {
        if (kind is ArrowEnd.None || size <= 0) return ShapeOutline.Empty;

        var dx = tip.X - from.X;
        var dy = tip.Y - from.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 0.0001) return ShapeOutline.Empty;

        var ux = dx / length;
        var uy = dy / length;

        return kind switch
        {
            ArrowEnd.Circle => Circle(tip, size / 2),
            ArrowEnd.Diamond => Diamond(tip, ux, uy, size),
            ArrowEnd.OpenArrow => Open(tip, ux, uy, size),
            _ => Solid(tip, ux, uy, size),
        };
    }

    private static ShapeOutline Solid(ShapePoint tip, double ux, double uy, double size)
    {
        var (left, right) = Wings(tip, ux, uy, size);

        return new ShapeOutline([new ShapeFigure(
        [
            new MoveSegment(tip),
            new LineSegment(left),
            new LineSegment(right),
            CloseSegment.Instance,
        ])]);
    }

    private static ShapeOutline Open(ShapePoint tip, double ux, double uy, double size)
    {
        var (left, right) = Wings(tip, ux, uy, size);

        return new ShapeOutline([new ShapeFigure(
        [
            new MoveSegment(left),
            new LineSegment(tip),
            new LineSegment(right),
        ])]);
    }

    private static ShapeOutline Diamond(ShapePoint tip, double ux, double uy, double size)
    {
        var back = new ShapePoint(tip.X - ux * size, tip.Y - uy * size);
        var middle = new ShapePoint(tip.X - ux * size / 2, tip.Y - uy * size / 2);

        var half = size / 3;

        return new ShapeOutline([new ShapeFigure(
        [
            new MoveSegment(tip),
            new LineSegment(new ShapePoint(middle.X - uy * half, middle.Y + ux * half)),
            new LineSegment(back),
            new LineSegment(new ShapePoint(middle.X + uy * half, middle.Y - ux * half)),
            CloseSegment.Instance,
        ])]);
    }

    private static ShapeOutline Circle(ShapePoint centre, double radius)
    {
        const double k = 0.5522847498307936;

        var pull = radius * k;

        return new ShapeOutline([new ShapeFigure(
        [
            new MoveSegment(new ShapePoint(centre.X, centre.Y - radius)),
            new CubicSegment(
                new ShapePoint(centre.X + pull, centre.Y - radius),
                new ShapePoint(centre.X + radius, centre.Y - pull),
                new ShapePoint(centre.X + radius, centre.Y)),
            new CubicSegment(
                new ShapePoint(centre.X + radius, centre.Y + pull),
                new ShapePoint(centre.X + pull, centre.Y + radius),
                new ShapePoint(centre.X, centre.Y + radius)),
            new CubicSegment(
                new ShapePoint(centre.X - pull, centre.Y + radius),
                new ShapePoint(centre.X - radius, centre.Y + pull),
                new ShapePoint(centre.X - radius, centre.Y)),
            new CubicSegment(
                new ShapePoint(centre.X - radius, centre.Y - pull),
                new ShapePoint(centre.X - pull, centre.Y - radius),
                new ShapePoint(centre.X, centre.Y - radius)),
            CloseSegment.Instance,
        ])]);
    }

    private static (ShapePoint Left, ShapePoint Right) Wings(
        ShapePoint tip, double ux, double uy, double size)
    {
        var back = new ShapePoint(tip.X - ux * size, tip.Y - uy * size);
        var half = size / 2.5;

        return (
            new ShapePoint(back.X - uy * half, back.Y + ux * half),
            new ShapePoint(back.X + uy * half, back.Y - ux * half));
    }
}
