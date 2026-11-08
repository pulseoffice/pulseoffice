using System.Globalization;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Shapes;

public interface IDimensionLineGeometry
{
    DimensionLayout Layout(ShapePoint start, ShapePoint end, DimensionLine line);

    DimensionLayout Layout(DimensionLine line, double width, double height);

    DimensionFrame Frame(ShapePoint start, ShapePoint end, DimensionLine line);

    DimensionLine Fitted(DimensionLine line, double width, double height);

    ShapeOutline Outline(DimensionLine line, double width, double height);

    ShapeOutline Heads(DimensionLine line, double width, double height, ArrowEnd startArrow, ArrowEnd endArrow, double size);

    string Label(DimensionLine line, double measuredPoints, IFormatProvider culture);

    ShapePoint LabelCentre(DimensionLine line, double width, double height, double textHeight);

    double ReadableAngle(ShapePoint from, ShapePoint to);

    bool IsKnownUnit(string? unit);
}

public sealed class DimensionLineGeometry(IArrowGeometry arrows) : IDimensionLineGeometry
{
    private const double Tiny = 1e-9;

    private readonly IReadOnlyDictionary<string, (double PerPoint, string Symbol)> _units = new Dictionary<string, (double, string)>(StringComparer.Ordinal)
    {
        ["mm"] = (25.4 / 72, "mm"),
        ["cm"] = (2.54 / 72, "cm"),
        ["m"] = (0.0254 / 72, "m"),
        ["km"] = (0.0000254 / 72, "km"),
        ["pt"] = (1, "pt"),
        ["pc"] = (1 / 12.0, "pc"),
        ["inch"] = (1 / 72.0, "\""),
        ["ft"] = (1 / 864.0, "ft"),
        ["mi"] = (1 / 4561920.0, "mi"),
    };

    public DimensionLineGeometry() : this(new ArrowGeometry())
    {
    }

    public bool IsKnownUnit(string? unit) => unit is not null && _units.ContainsKey(unit);

    public DimensionLayout Layout(ShapePoint start, ShapePoint end, DimensionLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var (ux, uy) = length > Tiny ? (dx / length, dy / length) : (1.0, 0.0);
        var side = line.Placing is DimensionPlacing.Above ? 1 : -1;
        var (nx, ny) = (uy * side, -ux * side);

        var distance = line.LineDistance.ToPoints();
        var reach = distance + line.GuideOverhang.ToPoints();
        var gap = line.GuideDistance.ToPoints();
        ShapePoint Along(ShapePoint origin, double offset) => new(origin.X + nx * offset, origin.Y + ny * offset);

        return new DimensionLayout(
            Along(start, distance),
            Along(end, distance),
            Along(start, gap - line.StartGuide.ToPoints()),
            Along(start, reach),
            Along(end, gap - line.EndGuide.ToPoints()),
            Along(end, reach),
            length);
    }

    public DimensionLayout Layout(DimensionLine line, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Layout(line.Start.Scaled(width, height), line.End.Scaled(width, height), Fitted(line, width, height));
    }

    public DimensionLine Fitted(DimensionLine line, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 0 || height < 0) return line;
        var start = line.Start.Scaled(width, height);
        var end = line.End.Scaled(width, height);
        var natural = Layout(start, end, line);
        var scale = new[]
            {
                (start, natural.LineStart), (end, natural.LineEnd),
                (start, natural.FirstGuideFrom), (start, natural.FirstGuideTo),
                (end, natural.SecondGuideFrom), (end, natural.SecondGuideTo),
            }
            .SelectMany(pair => new[]
            {
                Room(pair.Item1.X, pair.Item2.X - pair.Item1.X, width),
                Room(pair.Item1.Y, pair.Item2.Y - pair.Item1.Y, height),
            })
            .Where(double.IsFinite)
            .DefaultIfEmpty(1)
            .Min();
        scale = Math.Max(0, scale);
        if (Math.Abs(scale - 1) < 1e-6) return line;
        Twip Scaled(Twip length) => Twip.FromPoints(length.ToPoints() * scale);
        return line with
        {
            LineDistance = Scaled(line.LineDistance),
            GuideOverhang = Scaled(line.GuideOverhang),
            GuideDistance = Scaled(line.GuideDistance),
            StartGuide = Scaled(line.StartGuide),
            EndGuide = Scaled(line.EndGuide),
        };
    }

    public DimensionFrame Frame(ShapePoint start, ShapePoint end, DimensionLine line)
    {
        var points = Layout(start, end, line).Points.Append(start).Append(end).ToList();
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var width = points.Max(point => point.X) - left;
        var height = points.Max(point => point.Y) - top;
        ShapePoint Relative(ShapePoint point) => new(width > Tiny ? (point.X - left) / width : 0, height > Tiny ? (point.Y - top) / height : 0);
        return new DimensionFrame(left, top, width, height, Relative(start), Relative(end));
    }

    public ShapeOutline Outline(DimensionLine line, double width, double height)
    {
        var layout = Layout(line, width, height);
        return Normalized(new ShapeOutline(
        [
            Open(layout.LineStart, layout.LineEnd),
            Open(layout.FirstGuideFrom, layout.FirstGuideTo),
            Open(layout.SecondGuideFrom, layout.SecondGuideTo),
        ]), width, height);
    }

    public ShapeOutline Heads(DimensionLine line, double width, double height, ArrowEnd startArrow, ArrowEnd endArrow, double size)
    {
        var layout = Layout(line, width, height);
        var heads = arrows.Head(startArrow, layout.LineStart, layout.LineEnd, size).Figures
            .Concat(arrows.Head(endArrow, layout.LineEnd, layout.LineStart, size).Figures)
            .Select(figure => figure with { Filled = true, Stroked = false });
        return Normalized(new ShapeOutline([.. heads]), width, height);
    }

    public string Label(DimensionLine line, double measuredPoints, IFormatProvider culture)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Label is { } label) return label;
        var (perPoint, symbol) = _units.TryGetValue(line.Unit, out var unit) ? unit : _units["cm"];
        var value = (double.IsFinite(measuredPoints) ? measuredPoints : 0) * perPoint;
        var text = value.ToString("F" + Math.Clamp(line.DecimalPlaces, 0, 10).ToString(CultureInfo.InvariantCulture), culture);
        return line.ShowUnit ? text + " " + symbol : text;
    }

    public ShapePoint LabelCentre(DimensionLine line, double width, double height, double textHeight)
    {
        var layout = Layout(line, width, height);
        var start = line.Start.Scaled(width, height);
        var (outX, outY) = (layout.LineStart.X - start.X, layout.LineStart.Y - start.Y);
        var reach = Math.Sqrt(outX * outX + outY * outY);
        var angle = layout.ReadableAngleDegrees * Math.PI / 180;
        var (normalX, normalY) = reach > 0.001 ? (outX / reach, outY / reach) : (Math.Sin(angle), -Math.Cos(angle));
        return new ShapePoint(layout.Middle.X + normalX * textHeight / 2, layout.Middle.Y + normalY * textHeight / 2);
    }

    public double ReadableAngle(ShapePoint from, ShapePoint to)
    {
        var degrees = Math.Atan2(to.Y - from.Y, to.X - from.X) * 180 / Math.PI;
        if (degrees >= 90) return degrees - 180;
        return degrees < -90 ? degrees + 180 : degrees;
    }

    private double Room(double origin, double reach, double extent)
    {
        if (reach > Tiny) return (extent - origin) / reach;
        return reach < -Tiny ? origin / -reach : double.PositiveInfinity;
    }

    private ShapeFigure Open(ShapePoint from, ShapePoint to) =>
        new([new MoveSegment(from), new LineSegment(to)]) { Filled = false };

    private ShapeOutline Normalized(ShapeOutline outline, double width, double height) =>
        outline.Scaled(width > Tiny ? 1 / width : 0, height > Tiny ? 1 / height : 0);
}
