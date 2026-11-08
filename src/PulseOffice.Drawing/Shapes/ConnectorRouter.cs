namespace PulseOffice.Drawing.Shapes;

public readonly record struct ConnectorBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CentreX => X + Width / 2;
    public double CentreY => Y + Height / 2;
}

public interface IConnectorRouter
{
    ShapeOutline Route(ConnectorKind kind, ConnectorBox from, ConnectorSite fromSite,
        ConnectorBox to, ConnectorSite toSite);

    (double X, double Y) Attachment(ConnectorBox box, ConnectorSite site, ConnectorBox towards);

    ConnectorSite Resolve(ConnectorBox box, ConnectorSite site, ConnectorBox towards);
}

public sealed class ConnectorRouter : IConnectorRouter
{
    private const double StubFraction = 0.15;

    public ConnectorSite Resolve(ConnectorBox box, ConnectorSite site, ConnectorBox towards)
    {
        if (site is not ConnectorSite.Automatic) return site;

        var dx = towards.CentreX - box.CentreX;
        var dy = towards.CentreY - box.CentreY;

        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? ConnectorSite.Right : ConnectorSite.Left
            : dy >= 0 ? ConnectorSite.Bottom : ConnectorSite.Top;
    }

    public (double X, double Y) Attachment(ConnectorBox box, ConnectorSite site, ConnectorBox towards) =>
        Resolve(box, site, towards) switch
        {
            ConnectorSite.Left => (box.X, box.CentreY),
            ConnectorSite.Right => (box.Right, box.CentreY),
            ConnectorSite.Top => (box.CentreX, box.Y),
            _ => (box.CentreX, box.Bottom),
        };

    public ShapeOutline Route(
        ConnectorKind kind, ConnectorBox from, ConnectorSite fromSite,
        ConnectorBox to, ConnectorSite toSite)
    {
        var start = Attachment(from, fromSite, to);
        var end = Attachment(to, toSite, from);

        var startSide = Resolve(from, fromSite, to);
        var endSide = Resolve(to, toSite, from);

        var segments = kind switch
        {
            ConnectorKind.Straight => Straight(start, end),
            ConnectorKind.Curved => Curved(start, end, startSide, endSide),
            _ => Elbow(start, end, startSide, endSide),
        };

        return new ShapeOutline([new ShapeFigure(segments)]);
    }

    private List<ShapeSegment> Straight((double X, double Y) start, (double X, double Y) end) =>
    [
        new MoveSegment(new ShapePoint(start.X, start.Y)),
        new LineSegment(new ShapePoint(end.X, end.Y)),
    ];

    private List<ShapeSegment> Elbow(
        (double X, double Y) start, (double X, double Y) end,
        ConnectorSite startSide, ConnectorSite endSide)
    {
        var points = Horizontal(startSide) == Horizontal(endSide)
            ? Parallel(start, end, startSide, endSide)
            : Crossed(start, end, startSide, endSide);

        var segments = new List<ShapeSegment> { new MoveSegment(new ShapePoint(start.X, start.Y)) };
        segments.AddRange(points.Skip(1).Select(point => (ShapeSegment)new LineSegment(new ShapePoint(point.X, point.Y))));
        return segments;
    }

    private List<(double X, double Y)> Parallel(
        (double X, double Y) start, (double X, double Y) end,
        ConnectorSite startSide, ConnectorSite endSide)
    {
        var horizontal = Horizontal(startSide);
        var stub = Stub(start, end);

        if (startSide == endSide)
        {
            var outward = Sign(startSide);
            var edge = outward > 0
                ? Math.Max(Along(start, horizontal), Along(end, horizontal)) + stub
                : Math.Min(Along(start, horizontal), Along(end, horizontal)) - stub;

            return [start, At(edge, Across(start, horizontal), horizontal), At(edge, Across(end, horizontal), horizontal), end];
        }

        if (Facing(startSide, start, end) && Facing(endSide, end, start))
        {
            var middle = (Along(start, horizontal) + Along(end, horizontal)) / 2;

            return [start, At(middle, Across(start, horizontal), horizontal), At(middle, Across(end, horizontal), horizontal), end];
        }

        var startStub = Pull(start, startSide, stub);
        var endStub = Pull(end, endSide, stub);
        var across = (Across(start, horizontal) + Across(end, horizontal)) / 2;

        return
        [
            start, startStub,
            At(Along(startStub, horizontal), across, horizontal),
            At(Along(endStub, horizontal), across, horizontal),
            endStub, end,
        ];
    }

    private List<(double X, double Y)> Crossed(
        (double X, double Y) start, (double X, double Y) end,
        ConnectorSite startSide, ConnectorSite endSide)
    {
        var horizontal = Horizontal(startSide);
        var corner = horizontal ? (end.X, start.Y) : (start.X, end.Y);

        if (Facing(startSide, start, corner) && Facing(endSide, end, corner))
            return [start, corner, end];

        var stub = Stub(start, end);
        var startStub = Pull(start, startSide, stub);
        var endStub = Pull(end, endSide, stub);
        var bend = horizontal ? (startStub.X, endStub.Y) : (endStub.X, startStub.Y);

        return [start, startStub, bend, endStub, end];
    }

    private bool Horizontal(ConnectorSite side) => side is ConnectorSite.Left or ConnectorSite.Right;

    private int Sign(ConnectorSite side) => side is ConnectorSite.Right or ConnectorSite.Bottom ? 1 : -1;

    private double Along((double X, double Y) point, bool horizontal) => horizontal ? point.X : point.Y;

    private double Across((double X, double Y) point, bool horizontal) => horizontal ? point.Y : point.X;

    private (double X, double Y) At(double along, double across, bool horizontal) =>
        horizontal ? (along, across) : (across, along);

    private bool Facing(ConnectorSite side, (double X, double Y) from, (double X, double Y) to) =>
        (Along(to, Horizontal(side)) - Along(from, Horizontal(side))) * Sign(side) >= 0;

    private double Stub((double X, double Y) start, (double X, double Y) end) =>
        Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) * StubFraction;

    private List<ShapeSegment> Curved(
        (double X, double Y) start, (double X, double Y) end,
        ConnectorSite startSide, ConnectorSite endSide)
    {
        var distance = Math.Max(
            Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)) / 2;

        var first = Pull(start, startSide, distance);
        var second = Pull(end, endSide, distance);

        return
        [
            new MoveSegment(new ShapePoint(start.X, start.Y)),
            new CubicSegment(
                new ShapePoint(first.X, first.Y),
                new ShapePoint(second.X, second.Y),
                new ShapePoint(end.X, end.Y)),
        ];
    }

    private (double X, double Y) Pull(
        (double X, double Y) point, ConnectorSite side, double distance) => side switch
    {
        ConnectorSite.Left => (point.X - distance, point.Y),
        ConnectorSite.Right => (point.X + distance, point.Y),
        ConnectorSite.Top => (point.X, point.Y - distance),
        _ => (point.X, point.Y + distance),
    };
}
