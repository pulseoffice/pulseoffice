namespace PulseOffice.Drawing.Shapes;

public sealed record FramedConnector(ShapeOutline Path, double Left, double Top, double Width, double Height);

public interface IConnectorFramer
{
    FramedConnector? Frame(ConnectorObject connector, ConnectorBox from, ConnectorBox to);
}

public sealed class ConnectorFramer(IConnectorRouter router) : IConnectorFramer
{
    private const double Margin = 120;

    public ConnectorFramer() : this(new ConnectorRouter()) { }

    public FramedConnector? Frame(ConnectorObject connector, ConnectorBox from, ConnectorBox to)
    {
        ArgumentNullException.ThrowIfNull(connector);

        var outline = router.Route(connector.Shape, from, connector.Start.Site, to, connector.End.Site);
        var points = new SegmentPoints().Of(outline).ToList();
        if (points.Count == 0) return null;

        var left = points.Min(point => point.X) - Margin;
        var top = points.Min(point => point.Y) - Margin;
        var width = Math.Max(1, points.Max(point => point.X) - left + Margin);
        var height = Math.Max(1, points.Max(point => point.Y) - top + Margin);

        return new FramedConnector(new OutlineFrame(left, top, width, height).Normalize(outline), left, top, width, height);
    }

    private sealed class OutlineFrame(double left, double top, double width, double height)
    {
        public ShapeOutline Normalize(ShapeOutline outline) =>
            new SegmentMapper(point => new((point.X - left) / width, (point.Y - top) / height), 1 / width, 1 / height).Map(outline);
    }
}
