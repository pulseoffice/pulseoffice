using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfGluePoints
{
    public string? Index(ConnectorSite site) => site switch
    {
        ConnectorSite.Top => "0",
        ConnectorSite.Right => "1",
        ConnectorSite.Bottom => "2",
        ConnectorSite.Left => "3",
        _ => null,
    };

    public ConnectorSite Site(string? index) => index switch
    {
        "0" => ConnectorSite.Top,
        "1" => ConnectorSite.Right,
        "2" => ConnectorSite.Bottom,
        "3" => ConnectorSite.Left,
        _ => ConnectorSite.Automatic,
    };
}

internal sealed class DeckConnectorCodec(IShapePathWriter paths, OdfShapeStyleReader styles, OdfGluePoints glue) : DeckObjectCodec
{
    private readonly SegmentEnds _ends = new();

    public XElement Build(ObjectElement element, ConnectorObject connector, string styleName, Func<string, string?> written)
    {
        var bounds = element.Bounds;
        var points = _ends.Of(connector.Path).ToList();
        var start = points.Count > 0 ? points[0] : new ShapePoint(0, 0);
        var end = points.Count > 0 ? points[^1] : new ShapePoint(1, 1);
        var (left, top) = (Hundredths(bounds.Left.Value), Hundredths(bounds.Top.Value));
        var (width, height) = (Math.Max(1, Hundredths(bounds.Width.Value)), Math.Max(1, Hundredths(bounds.Height.Value)));
        var absolute = new SegmentMapper(point => new ShapePoint(left + point.X * width, top + point.Y * height), width, height).Map(connector.Path);

        var node = new XElement(Draw + "connector",
            new XAttribute(Draw + "style-name", styleName),
            new XAttribute(Xml + "id", element.Id),
            new XAttribute(Draw + "id", element.Id),
            new XAttribute(Draw + "type", connector.Shape switch
            {
                ConnectorKind.Straight => "line",
                ConnectorKind.Curved => "curve",
                _ => "standard",
            }),
            new XAttribute(Svg + "x1", Cm(At(bounds.Left, bounds.Width, start.X))),
            new XAttribute(Svg + "y1", Cm(At(bounds.Top, bounds.Height, start.Y))),
            new XAttribute(Svg + "x2", Cm(At(bounds.Left, bounds.Width, end.X))),
            new XAttribute(Svg + "y2", Cm(At(bounds.Top, bounds.Height, end.Y))));
        Glue(node, "start", connector.Start, written);
        Glue(node, "end", connector.End, written);
        if (connector.Path.Figures.Count > 0)
        {
            node.Add(new XAttribute(Svg + "viewBox", string.Create(CultureInfo.InvariantCulture, $"{left} {top} {width} {height}")));
            node.Add(new XAttribute(Svg + "d", paths.Write(absolute)));
        }
        return node;
    }

    public bool Reads(XElement node) => node.Name == Draw + "connector";

    public ObjectElement Read(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions)
    {
        var length = new OdfLength();
        var x1 = length.Parse((string?)node.Attribute(Svg + "x1"), Twip.Zero).Value;
        var y1 = length.Parse((string?)node.Attribute(Svg + "y1"), Twip.Zero).Value;
        var x2 = length.Parse((string?)node.Attribute(Svg + "x2"), Twip.Zero).Value;
        var y2 = length.Parse((string?)node.Attribute(Svg + "y2"), Twip.Zero).Value;
        var bounds = new DocumentRect(new Twip(Math.Min(x1, x2)), new Twip(Math.Min(y1, y2)), new Twip(Math.Abs(x2 - x1)), new Twip(Math.Abs(y2 - y1)));
        var path = Straight(x1, y1, x2, y2, bounds);

        if ((string?)node.Attribute(Svg + "d") is { } data && ViewBox(node) is { } box
            && new SvgPathParser().Parse(data) is { Figures.Count: > 0 } outline)
        {
            bounds = box;
            path = new OdfViewBox(node, 1).Normalize(outline);
        }

        var styleName = (string?)node.Attribute(Draw + "style-name");
        var style = styles.Read(styleName is not null && definitions.TryGetValue(styleName, out var found) ? found : null, definitions);
        return new ObjectElement
        {
            Id = id,
            Bounds = bounds,
            Content = new ConnectorObject
            {
                Id = id,
                Shape = (string?)node.Attribute(Draw + "type") switch
                {
                    "line" => ConnectorKind.Straight,
                    "curve" => ConnectorKind.Curved,
                    _ => ConnectorKind.Elbow,
                },
                Start = Endpoint(node, "start"),
                End = Endpoint(node, "end"),
                Style = style with { Fill = ShapeFill.None },
                Path = path,
            },
        };
    }

    private ConnectorEndpoint Endpoint(XElement node, string side) =>
        (string?)node.Attribute(Draw + $"{side}-shape") is { Length: > 0 } shape
            ? new ConnectorEndpoint(shape, glue.Site((string?)node.Attribute(Draw + $"{side}-glue-point")))
            : ConnectorEndpoint.Free;

    private void Glue(XElement node, string side, ConnectorEndpoint endpoint, Func<string, string?> written)
    {
        if (endpoint.ObjectId is not { Length: > 0 } target || written(target) is not { } shape) return;
        node.Add(new XAttribute(Draw + $"{side}-shape", shape));
        if (glue.Index(endpoint.Site) is { } index) node.Add(new XAttribute(Draw + $"{side}-glue-point", index));
    }

    private DocumentRect? ViewBox(XElement node)
    {
        var parts = ((string?)node.Attribute(Svg + "viewBox"))?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        var values = parts?.Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : double.NaN).ToArray();
        if (values is not { Length: 4 } || !values.All(double.IsFinite) || values[2] <= 0 || values[3] <= 0) return null;
        return new DocumentRect(Twips(values[0]), Twips(values[1]), Twips(values[2]), Twips(values[3]));
    }

    private ShapeOutline Straight(int x1, int y1, int x2, int y2, DocumentRect bounds)
    {
        double Relative(int value, int origin, int size) => size == 0 ? 0 : (value - origin) / (double)size;
        return new ShapeOutline([new ShapeFigure(
        [
            new MoveSegment(new ShapePoint(Relative(x1, bounds.Left.Value, bounds.Width.Value), Relative(y1, bounds.Top.Value, bounds.Height.Value))),
            new LineSegment(new ShapePoint(Relative(x2, bounds.Left.Value, bounds.Width.Value), Relative(y2, bounds.Top.Value, bounds.Height.Value))),
        ])]);
    }

    private Twip At(Twip origin, Twip size, double fraction) => new((int)Math.Round(origin.Value + fraction * size.Value));

    private int Hundredths(int twips) => (int)Math.Round(twips / Twip.PerCentimeter * 1000);

    private Twip Twips(double hundredths) => new((int)Math.Clamp(Math.Round(hundredths / 1000 * Twip.PerCentimeter), int.MinValue, int.MaxValue));
}
