using PulseOffice.Formats;
using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfShapeVocabulary
{
    public System.Xml.Linq.XNamespace PulseShape { get; } = "urn:pulseoffice:shape";

    public TokenMap<BevelStyle> Bevels { get; } = new(BevelStyle.Round, "round",
        (BevelStyle.Angle, "angle"), (BevelStyle.SoftRound, "soft-round"), (BevelStyle.Cross, "cross"),
        (BevelStyle.Relaxed, "relaxed"), (BevelStyle.None, "none"));

    public TokenMap<ShapeFillKind> Fills { get; } = new(ShapeFillKind.Solid, "solid",
        (ShapeFillKind.None, "none"), (ShapeFillKind.Gradient, "gradient"), (ShapeFillKind.Hatch, "hatch"), (ShapeFillKind.Bitmap, "bitmap"));

    public TokenMap<GradientKind> Gradients { get; } = new(GradientKind.Linear, "linear",
        (GradientKind.Axial, "axial"), (GradientKind.Radial, "radial"), (GradientKind.Elliptical, "ellipsoid"),
        (GradientKind.Square, "square"), (GradientKind.Rectangular, "rectangular"));

    public OdfGradientAngles GradientAngles { get; } = new();

    public TokenMap<HatchKind> Hatches { get; } = new(HatchKind.Single, "single",
        (HatchKind.Double, "double"), (HatchKind.Triple, "triple"));

    public TokenMap<LineJoinStyle> Joins { get; } = new(LineJoinStyle.Round, "round",
        (LineJoinStyle.Miter, "miter"), (LineJoinStyle.Bevel, "bevel"));

    public TokenMap<LineCapStyle> Caps { get; } = new(LineCapStyle.Flat, "butt",
        (LineCapStyle.Round, "round"), (LineCapStyle.Square, "square"));

    public TokenMap<TextHorizontalPlacement> ParagraphAlignment { get; } = new(TextHorizontalPlacement.Centre, "center",
        (TextHorizontalPlacement.Left, "start"), (TextHorizontalPlacement.Right, "end"), (TextHorizontalPlacement.Justify, "justify"));

    public TokenMap<TextHorizontalPlacement> AreaAlignment { get; } = new(TextHorizontalPlacement.Centre, "center",
        (TextHorizontalPlacement.Left, "left"), (TextHorizontalPlacement.Right, "right"));

    public TokenMap<TextVerticalPlacement> VerticalAlignment { get; } = new(TextVerticalPlacement.Middle, "middle",
        (TextVerticalPlacement.Top, "top"), (TextVerticalPlacement.Bottom, "bottom"));
}

internal sealed class OdfGradientAngles
{
    private const double QuarterTurn = 90;
    private const double FullTurn = 360;

    public double FromOdf(double odfDegrees) => Normal(QuarterTurn - odfDegrees);

    public double ToOdf(double drawingDegrees) => Normal(QuarterTurn - drawingDegrees);

    public double Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var text = value.Trim();
        var (number, factor) = text switch
        {
            _ when text.EndsWith("grad", StringComparison.Ordinal) => (text[..^4], 0.9),
            _ when text.EndsWith("deg", StringComparison.Ordinal) => (text[..^3], 1.0),
            _ when text.EndsWith("rad", StringComparison.Ordinal) => (text[..^3], 180 / Math.PI),
            _ => (text, 0.1),
        };
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed * factor) ? parsed * factor : 0;
    }

    public string Format(double odfDegrees) =>
        ((int)Math.Round(odfDegrees * 10)).ToString(CultureInfo.InvariantCulture);

    private double Normal(double degrees) => ((degrees % FullTurn) + FullTurn) % FullTurn;
}

internal sealed class OdfMeasure
{
    public string Colour(string hex) => "#" + hex.TrimStart('#').PadLeft(6, '0')[^6..].ToLowerInvariant();

    public string Length(Twip value) => $"{Number(value.ToCentimeters())}cm";

    public string Number(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);

    public string Percent(double value) => $"{(int)Math.Round(Math.Clamp(value, 0, 1) * 100)}%";

    public double ParsePercent(string? text, double fallback) =>
        text is not null && double.TryParse(text.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value / 100
            : fallback;

    public double ParseFraction(string? text, double fallback) => Math.Clamp(ParsePercent(text, fallback), 0, 1);

    public double ParseNumber(string? text, double fallback) =>
        text is not null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value
            : fallback;

    public double ParseAngle(string? text, double fallback) => ParseNumber(text, fallback) % FullTurn;

    private const double FullTurn = 360;

    public Twip ParseLength(string? text, Twip fallback)
    {
        return new OdfLength().Parse(text, fallback);
    }
}

internal abstract class ArrowMarker
{
    public abstract ArrowEnd End { get; }

    public abstract string Path { get; }

    public string Name => $"pulseoffice-{End.ToString().ToLowerInvariant()}";

    public bool Names(string marker) => marker.EndsWith(End.ToString(), StringComparison.OrdinalIgnoreCase);
}

internal sealed class ArrowHeadMarker : ArrowMarker
{
    public override ArrowEnd End => ArrowEnd.Arrow;
    public override string Path => "M10 0 L20 30 L0 30 Z";
}

internal sealed class OpenArrowMarker : ArrowMarker
{
    public override ArrowEnd End => ArrowEnd.OpenArrow;
    public override string Path => "M10 0 L20 30 L10 22 L0 30 Z";
}

internal sealed class CircleMarker : ArrowMarker
{
    public override ArrowEnd End => ArrowEnd.Circle;
    public override string Path => "M10 0 C15 0 20 7 20 15 C20 23 15 30 10 30 C5 30 0 23 0 15 C0 7 5 0 10 0 Z";
}

internal sealed class DiamondMarker : ArrowMarker
{
    public override ArrowEnd End => ArrowEnd.Diamond;
    public override string Path => "M10 0 L20 15 L10 30 L0 15 Z";
}

internal sealed class ArrowMarkers
{
    private readonly ArrowMarker _default = new ArrowHeadMarker();
    private readonly IReadOnlyList<ArrowMarker> _specific = [new CircleMarker(), new DiamondMarker(), new OpenArrowMarker()];

    public ArrowMarker For(ArrowEnd end) => _specific.FirstOrDefault(marker => marker.End == end) ?? _default;

    public ArrowEnd Read(string? name) =>
        name is null ? ArrowEnd.None : (_specific.FirstOrDefault(marker => marker.Names(name)) ?? _default).End;
}
