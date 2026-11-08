using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Drawing.Shapes;

public sealed record VectorPaint
{
    public string? FillHex { get; init; } = "#000000";

    public string? StrokeHex { get; init; }

    public double StrokeWidth { get; init; } = 1;

    public static readonly VectorPaint Default = new();
}

public sealed record VectorPath(ShapeOutline Outline, VectorPaint Paint);

public sealed record VectorDrawing(IReadOnlyList<VectorPath> Paths)
{
    public static readonly VectorDrawing Empty = new([]);

    public bool IsEmpty => Paths.Count == 0;

    public VectorDrawing Scaled(double width, double height) =>
        new([.. Paths.Select(path => path with { Outline = path.Outline.Scaled(width, height) })]);

    public VectorDrawing Recoloured(string? hex) =>
        string.IsNullOrEmpty(hex)
            ? this
            : new VectorDrawing([.. Paths.Select(path => path with
            {
                Paint = path.Paint with
                {
                    FillHex = path.Paint.FillHex is null ? null : hex,
                    StrokeHex = path.Paint.StrokeHex is null ? null : hex,
                },
            })]);
}

public interface IVectorOutlineReader
{
    VectorDrawing Read(string svg);
}

public sealed class VectorOutlineReader : IVectorOutlineReader
{
    private readonly ISvgPathParser _paths;

    public VectorOutlineReader(ISvgPathParser paths, IArcFlattener? flattener = null)
    {
        _paths = paths;
        _flattener = flattener ?? new ArcFlattener();
    }

    private readonly IArcFlattener _flattener;
    private readonly SvgTransform _transforms = new();

    private AffineMatrix Transform(XElement element) =>
        element.AncestorsAndSelf().Reverse()
            .Aggregate(AffineMatrix.Identity, (total, node) => total.Then(_transforms.Parse((string?)node.Attribute("transform"))));

    public VectorOutlineReader() : this(new SvgPathParser()) { }

    public VectorDrawing Read(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg)) return VectorDrawing.Empty;

        XDocument document;

        try
        {
            using var reader = System.Xml.XmlReader.Create(new StringReader(svg),
                new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
            document = XDocument.Load(reader);
        }
        catch (System.Xml.XmlException)
        {
            return VectorDrawing.Empty;
        }

        if (document.Root is not { } root) return VectorDrawing.Empty;

        var box = ViewBox(root);
        var paths = new List<VectorPath>();

        foreach (var element in root.Descendants()
            .Where(node => node.Name.LocalName is "path" or "rect" or "circle" or "ellipse" or "line" or "polyline" or "polygon")
            .Where(node => !node.Ancestors().Any(parent => parent.Name.LocalName is "defs" or "clipPath" or "mask" or "symbol" or "marker" or "pattern")))
        {
            var outline = element.Name.LocalName switch
            {
                "rect" => RectOutline(element),
                "circle" => EllipseOutline(Num(element, "cx"), Num(element, "cy"), Num(element, "r"), Num(element, "r")),
                "ellipse" => EllipseOutline(Num(element, "cx"), Num(element, "cy"), Num(element, "rx"), Num(element, "ry")),
                "line" => PolyOutline([(Num(element, "x1"), Num(element, "y1")), (Num(element, "x2"), Num(element, "y2"))], closed: false),
                "polyline" => PolyOutline(Points(element), closed: false),
                "polygon" => PolyOutline(Points(element), closed: true),
                _ => (string?)element.Attribute("d") is { Length: > 0 } data ? _paths.Parse(data) : null,
            };

            if (outline is null || outline.Figures.Count == 0) continue;

            var matrix = Transform(element);
            if (!matrix.IsIdentity)
                outline = new SegmentMapper(matrix.Apply).Map(_flattener.Flatten(outline));

            paths.Add(new VectorPath(
                box is { } view ? Normalized(outline, view) : outline,
                PaintOf(element, box) is var paint && !matrix.IsIdentity ? paint with { StrokeWidth = paint.StrokeWidth * matrix.Scale } : paint));
        }

        return new VectorDrawing(paths);
    }

    private static ShapeOutline? RectOutline(XElement element)
    {
        var x = Num(element, "x");
        var y = Num(element, "y");
        var w = Num(element, "width");
        var h = Num(element, "height");
        if (w <= 0 || h <= 0) return null;

        return new ShapeOutline(
        [
            new ShapeFigure(
            [
                new MoveSegment(new ShapePoint(x, y)),
                new LineSegment(new ShapePoint(x + w, y)),
                new LineSegment(new ShapePoint(x + w, y + h)),
                new LineSegment(new ShapePoint(x, y + h)),
                CloseSegment.Instance,
            ]),
        ]);
    }

    private static ShapeOutline? EllipseOutline(double cx, double cy, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0) return null;

        const double k = 0.5522847498;
        var (ox, oy) = (rx * k, ry * k);

        return new ShapeOutline(
        [
            new ShapeFigure(
            [
                new MoveSegment(new ShapePoint(cx + rx, cy)),
                new CubicSegment(new ShapePoint(cx + rx, cy + oy), new ShapePoint(cx + ox, cy + ry), new ShapePoint(cx, cy + ry)),
                new CubicSegment(new ShapePoint(cx - ox, cy + ry), new ShapePoint(cx - rx, cy + oy), new ShapePoint(cx - rx, cy)),
                new CubicSegment(new ShapePoint(cx - rx, cy - oy), new ShapePoint(cx - ox, cy - ry), new ShapePoint(cx, cy - ry)),
                new CubicSegment(new ShapePoint(cx + ox, cy - ry), new ShapePoint(cx + rx, cy - oy), new ShapePoint(cx + rx, cy)),
                CloseSegment.Instance,
            ]),
        ]);
    }

    private static ShapeOutline? PolyOutline(IReadOnlyList<(double X, double Y)> points, bool closed)
    {
        if (points.Count < 2) return null;

        var segments = new List<ShapeSegment> { new MoveSegment(new ShapePoint(points[0].X, points[0].Y)) };
        segments.AddRange(points.Skip(1).Select(point => (ShapeSegment)new LineSegment(new ShapePoint(point.X, point.Y))));
        if (closed) segments.Add(CloseSegment.Instance);

        return new ShapeOutline([new ShapeFigure(segments)]);
    }

    private static IReadOnlyList<(double X, double Y)> Points(XElement element)
    {
        var numbers = ((string?)element.Attribute("points") ?? string.Empty)
            .Split([' ', ',', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : double.NaN)
            .ToList();

        if (numbers.Any(double.IsNaN)) return [];

        return [.. Enumerable.Range(0, numbers.Count / 2).Select(index => (numbers[index * 2], numbers[index * 2 + 1]))];
    }

    private static double Num(XElement element, string name) =>
        double.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)
            ? v : 0;

    private static VectorPaint PaintOf(
        XElement element, (double X, double Y, double Width, double Height)? box)
    {
        var fill = Inherited(element, "fill");
        var stroke = Inherited(element, "stroke");
        var width = Inherited(element, "stroke-width");

        var scale = box is { } view ? 1 / Math.Max(view.Width, view.Height) : 1;

        return new VectorPaint
        {
            FillHex = Colour(fill, fallback: stroke is null || IsNone(stroke) ? "#000000" : null),
            StrokeHex = Colour(stroke, fallback: null),
            StrokeWidth = (double.TryParse(
                width, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1) * scale,
        };
    }

    private static string? Inherited(XElement element, string name)
    {
        for (var node = element; node is not null; node = node.Parent)
            if ((string?)node.Attribute(name) is { Length: > 0 } value)
                return value;

        return null;
    }

    private static bool IsNone(string value) =>
        value.Equals("none", StringComparison.OrdinalIgnoreCase);

    private static string? Colour(string? value, string? fallback)
    {
        if (value is null) return fallback;
        if (IsNone(value)) return null;
        if (value.StartsWith('#')) return value;

        return value.Equals("currentColor", StringComparison.OrdinalIgnoreCase) ? "#000000" : value;
    }

    private static (double X, double Y, double Width, double Height)? ViewBox(XElement root)
    {
        var text = (string?)root.Attribute("viewBox");
        if (string.IsNullOrWhiteSpace(text))
        {
            var width = Length((string?)root.Attribute("width"));
            var height = Length((string?)root.Attribute("height"));
            return width > 0 && height > 0 ? (0, 0, width, height) : null;
        }

        var parts = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return null;

        var numbers = new double[4];

        for (var index = 0; index < 4; index++)
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[index]))
                return null;

        if (!numbers.All(double.IsFinite) || numbers[2] <= 0 || numbers[3] <= 0) return null;

        return (numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static double Length(string? text) =>
        double.TryParse(text?.Trim().TrimEnd('x', 'p', 'X', 'P'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0;

    private ShapeOutline Normalized(ShapeOutline outline, (double X, double Y, double Width, double Height) box) =>
        new SegmentMapper(
            point => new((point.X - box.X) / box.Width, (point.Y - box.Y) / box.Height),
            1 / box.Width, 1 / box.Height).Map(outline);
}
