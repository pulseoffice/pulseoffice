using System.Globalization;
using System.Text;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing.Freeform;

public interface IShapeConversion
{
    FreeformPath ToCurve(ShapeOutline outline);

    FreeformPath ToPolygon(ShapeOutline outline, int stepsPerCurve = 12);

    string ToSvg(ShapeOutline outline, ShapeStyle style, double width, double height);
}

public sealed class ShapeConversion(IArcFlattener arcs, IShapePathWriter paths) : IShapeConversion
{
    private const int MaximumStepsPerCurve = 256;

    public ShapeConversion() : this(new ArcFlattener(), new ShapePathWriter()) { }

    public FreeformPath ToCurve(ShapeOutline outline)
    {
        ArgumentNullException.ThrowIfNull(outline);
        var path = FreeformPath.FromOutline(arcs.Flatten(outline));
        return path with { Filled = path.Figures.Any(figure => figure.Closed) };
    }

    public FreeformPath ToPolygon(ShapeOutline outline, int stepsPerCurve = 12)
    {
        var curve = ToCurve(outline);
        var steps = Math.Clamp(stepsPerCurve, 2, MaximumStepsPerCurve);
        var figures = curve.Figures.Select(figure =>
        {
            var nodes = new List<PathNode>();
            var count = figure.Nodes.Count;
            var joins = figure.Closed ? count : count - 1;
            if (count > 0) nodes.Add(new PathNode(figure.Nodes[0].Point));
            for (var i = 0; i < joins; i++)
            {
                var from = figure.Nodes[i];
                var to = figure.Nodes[(i + 1) % count];
                if (from.After is null && to.Before is null)
                {
                    if (i + 1 < count) nodes.Add(new PathNode(to.Point));
                    continue;
                }

                for (var step = 1; step <= steps; step++)
                {
                    var point = Cubic(from.Point, from.After ?? from.Point, to.Before ?? to.Point, to.Point, (double)step / steps);
                    if (step < steps || i + 1 < count) nodes.Add(new PathNode(point));
                }
            }

            return new PathFigure(nodes) { Closed = figure.Closed };
        }).ToList();

        return new FreeformPath(figures) { Filled = curve.Filled };
    }

    public string ToSvg(ShapeOutline outline, ShapeStyle style, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(style);
        var fill = style.Fill.IsVisible ? style.Fill.ColorHex : "none";
        var stroke = style.Line.IsVisible ? style.Line.ColorHex : "none";
        var strokeWidth = style.Line.Width.Value / 1000.0;
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\" width=\"{width:0.##}\" height=\"{height:0.##}\" preserveAspectRatio=\"none\">");
        builder.Append(CultureInfo.InvariantCulture,
            $"<path d=\"{paths.Write(outline)}\" fill=\"{fill}\" fill-opacity=\"{style.Fill.Opacity:0.###}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth:0.####}\"/>");
        builder.Append("</svg>");
        return builder.ToString();
    }

    private ShapePoint Cubic(ShapePoint p0, ShapePoint p1, ShapePoint p2, ShapePoint p3, double t)
    {
        var u = 1 - t;
        return new ShapePoint(
            u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
            u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y);
    }
}
