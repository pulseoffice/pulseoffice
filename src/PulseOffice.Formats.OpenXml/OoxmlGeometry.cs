using System.Globalization;
using PulseOffice.Drawing.Shapes;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlGeometry
{
    private const long Grid = 21600;
    private readonly IArcFlattener _flattener;
    private readonly IReadOnlyDictionary<ShapeShade, A.PathFillModeValues> _shades = new Dictionary<ShapeShade, A.PathFillModeValues>
    {
        [ShapeShade.Darken] = A.PathFillModeValues.Darken,
        [ShapeShade.DarkenLess] = A.PathFillModeValues.DarkenLess,
        [ShapeShade.Lighten] = A.PathFillModeValues.Lighten,
        [ShapeShade.LightenLess] = A.PathFillModeValues.LightenLess,
    };

    public OoxmlGeometry(IArcFlattener flattener) => _flattener = flattener;

    public OoxmlGeometry() : this(new ArcFlattener()) { }

    public A.CustomGeometry CustomGeometry(ShapeOutline outline)
    {
        var scaled = _flattener.Flatten(outline).Scaled(Grid, Grid);
        var list = new A.PathList();

        new PathReplay().Replay(scaled, new CustomPathSink(list, this));
        foreach (var (path, figure) in list.Elements<A.Path>().Zip(scaled.Figures))
        {
            if (!figure.Filled) path.Fill = A.PathFillModeValues.None;
            else if (_shades.TryGetValue(figure.Shade, out var shade)) path.Fill = shade;
            if (!figure.Stroked) path.Stroke = false;
        }

        return new A.CustomGeometry(new A.AdjustValueList(), list);
    }

    public ShapeOutline OutlineOf(A.CustomGeometry geometry)
    {
        var figures = new List<ShapeFigure>();

        foreach (var path in geometry.Descendants<A.Path>())
        {
            var width = path.Width?.Value ?? Grid;
            var height = path.Height?.Value ?? Grid;
            ShapePoint Map(A.Point? point) => new(Fraction(Parse(point?.X), width), Fraction(Parse(point?.Y), height));

            var segments = new List<ShapeSegment>();
            foreach (var element in path.ChildElements)
                switch (element)
                {
                    case A.MoveTo move: segments.Add(new MoveSegment(Map(move.Point))); break;
                    case A.LineTo line: segments.Add(new LineSegment(Map(line.Point))); break;
                    case A.CubicBezierCurveTo cubic:
                        var points = cubic.Elements<A.Point>().ToList();
                        if (points.Count >= 3) segments.Add(new CubicSegment(Map(points[0]), Map(points[1]), Map(points[2])));
                        break;
                    case A.CloseShapePath: segments.Add(CloseSegment.Instance); break;
                }

            if (segments.Count > 1)
                figures.Add(new ShapeFigure(segments)
                {
                    Filled = path.Fill?.Value != A.PathFillModeValues.None,
                    Stroked = path.Stroke?.Value ?? true,
                    Shade = path.Fill?.Value is { } fill ? _shades.FirstOrDefault(pair => pair.Value == fill).Key : ShapeShade.Normal,
                });
        }

        return new ShapeOutline(figures);
    }

    private A.Point Point(ShapePoint point) => new()
    {
        X = ((long)Math.Round(point.X)).ToString(CultureInfo.InvariantCulture),
        Y = ((long)Math.Round(point.Y)).ToString(CultureInfo.InvariantCulture),
    };

    private double Fraction(double coordinate, double extent) => extent > 0 ? coordinate / extent : 0;

    private double Parse(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private sealed class CustomPathSink(A.PathList list, OoxmlGeometry geometry) : PathSink
    {
        private A.Path _path = new();

        public override void BeginFigure() => _path = new A.Path { Width = Grid, Height = Grid };

        public override void MoveTo(ShapePoint to) => _path.AppendChild(new A.MoveTo(geometry.Point(to)));

        public override void LineTo(ShapePoint to) => _path.AppendChild(new A.LineTo(geometry.Point(to)));

        public override void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to) =>
            _path.AppendChild(new A.CubicBezierCurveTo(geometry.Point(first), geometry.Point(second), geometry.Point(to)));

        public override void ArcTo(ShapePoint from, ArcSegment arc)
        {
        }

        public override void Close() => _path.AppendChild(new A.CloseShapePath());

        public override void EndFigure() => list.AppendChild(_path);
    }
}
