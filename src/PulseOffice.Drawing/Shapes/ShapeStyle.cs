using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Shapes;

public enum ShapeFillKind
{
    None,
    Solid,
    Gradient,
    Hatch,
    Bitmap,
}

public enum GradientKind
{
    Linear,
    Axial,
    Radial,
    Elliptical,
    Square,
    Rectangular,
}

public enum HatchKind
{
    Single,
    Double,
    Triple,
}

public enum ShapeStrokeStyle
{
    None,
    Solid,
    Dashed,
    Dotted,
    DashDot,
    DashDotDot,
    LongDash,
}

public enum LineCapStyle
{
    Flat,
    Round,
    Square,
}

public enum LineJoinStyle
{
    Miter,
    Round,
    Bevel,
}

public enum TextHorizontalPlacement
{
    Left,
    Centre,
    Right,
    Justify,
}

public enum TextVerticalPlacement
{
    Top,
    Middle,
    Bottom,
}

public enum BevelStyle
{
    None,
    Round,
    Angle,
    SoftRound,
    Cross,
    Relaxed,
}

public readonly record struct GradientStop(string ColorHex, double Position);

public sealed record ShapeGradient
{
    public GradientKind Kind { get; init; } = GradientKind.Linear;

    public double AngleDegrees { get; init; } = 90;

    public IReadOnlyList<GradientStop> Stops { get; init; } =
        [new("#2563EB", 0), new("#0F172A", 1)];

    public double CentreX { get; init; } = 0.5;

    public double CentreY { get; init; } = 0.5;

    public double Border { get; init; }

    public string StartHex => Stops.Count > 0 ? Stops[0].ColorHex : "#FFFFFF";

    public string EndHex => Stops.Count > 0 ? Stops[^1].ColorHex : "#000000";

    public static readonly ShapeGradient Default = new();
}

public sealed record ShapeHatch
{
    public HatchKind Kind { get; init; } = HatchKind.Single;

    public string ColorHex { get; init; } = "#1E3A8A";

    public double AngleDegrees { get; init; } = 45;

    public Twip Distance { get; init; } = Twip.FromPoints(4);

    public string? BackgroundHex { get; init; }

    public static readonly ShapeHatch Default = new();
}

public sealed record ShapeBitmap
{
    public required byte[] Data { get; init; }

    public string MediaType { get; init; } = "image/png";

    public bool Tiled { get; init; }

    public Twip TileWidth { get; init; }

    public Twip TileHeight { get; init; }

    public double TileWidthRatio { get; init; }

    public double TileHeightRatio { get; init; }

    public bool HasChosenTileSize => TileWidth.Value > 0 || TileHeight.Value > 0 || TileWidthRatio > 0 || TileHeightRatio > 0;

    public bool Equals(ShapeBitmap? other) =>
        other is not null && MediaType == other.MediaType && Tiled == other.Tiled && TileWidth == other.TileWidth && TileHeight == other.TileHeight
        && TileWidthRatio.Equals(other.TileWidthRatio) && TileHeightRatio.Equals(other.TileHeightRatio)
        && Data.AsSpan().SequenceEqual(other.Data);

    public override int GetHashCode() => HashCode.Combine(MediaType, Tiled, TileWidth, TileHeight, Data.Length);
}

public sealed record ShapeFill
{
    public ShapeFillKind Kind { get; init; } = ShapeFillKind.Solid;

    public string ColorHex { get; init; } = "#2563EB";

    public double Opacity { get; init; } = 1;

    public ShapeGradient Gradient { get; init; } = ShapeGradient.Default;

    public ShapeHatch Hatch { get; init; } = ShapeHatch.Default;

    public ShapeBitmap? Bitmap { get; init; }

    public bool IsVisible => Kind is not ShapeFillKind.None && Opacity > 0 && (Kind is not ShapeFillKind.Bitmap || Bitmap is not null);

    public static readonly ShapeFill Default = new();

    public static readonly ShapeFill None = new() { Kind = ShapeFillKind.None };
}

public sealed record ShapeDashPattern
{
    public const int MaximumDots = 255;

    public int Dots1 { get; init; } = 1;

    public double Dots1Length { get; init; } = 1;

    public int Dots2 { get; init; }

    public double Dots2Length { get; init; }

    public double Distance { get; init; } = 1;

    public bool Relative { get; init; } = true;

    public bool Round { get; init; }

    public ShapeStrokeStyle NearestPreset()
    {
        if (Dots2 >= 2) return ShapeStrokeStyle.DashDotDot;
        if (Dots2 == 1) return ShapeStrokeStyle.DashDot;
        var length = Relative ? Dots1Length * Twip.FromPoints(1).Value : Dots1Length;
        return length <= Twip.FromCentimeters(0.1).Value ? ShapeStrokeStyle.Dotted
            : length >= Twip.FromCentimeters(0.5).Value ? ShapeStrokeStyle.LongDash
            : ShapeStrokeStyle.Dashed;
    }

    public IReadOnlyList<double> InLineWidths(double lineWidthTwips)
    {
        var scale = Relative || lineWidthTwips <= 0 ? 1 : 1 / lineWidthTwips;
        var gap = double.IsFinite(Distance * scale) ? Math.Max(Distance * scale, 0.01) : 0.01;
        var pattern = new List<double>();
        for (var index = 0; index < Math.Min(Dots1, MaximumDots); index++) pattern.AddRange([Dot(Dots1Length * scale), gap]);
        for (var index = 0; index < Math.Min(Dots2, MaximumDots); index++) pattern.AddRange([Dot(Dots2Length * scale), gap]);
        return pattern.Count == 0 ? [1, gap] : pattern;
    }

    private static double Dot(double length) => length > 0 && double.IsFinite(length) ? length : 1;
}

public sealed record ShapeLine
{
    public string ColorHex { get; init; } = "#1E3A8A";

    public Twip Width { get; init; } = Twip.FromPoints(1);

    public ShapeStrokeStyle Dash { get; init; } = ShapeStrokeStyle.Solid;

    public ShapeDashPattern? CustomDash { get; init; }

    public double Opacity { get; init; } = 1;

    public LineCapStyle Cap { get; init; } = LineCapStyle.Flat;

    public LineJoinStyle Join { get; init; } = LineJoinStyle.Round;

    public ArrowEnd StartArrow { get; init; } = ArrowEnd.None;

    public ArrowEnd EndArrow { get; init; } = ArrowEnd.None;

    public double ArrowScale { get; init; } = 1;

    public bool IsVisible => Dash is not ShapeStrokeStyle.None && Width.Value > 0 && Opacity > 0;

    public static readonly ShapeLine Default = new();

    public static readonly ShapeLine None = new() { Dash = ShapeStrokeStyle.None };
}

public sealed record ShapeShadow
{
    public bool Enabled { get; init; }

    public string ColorHex { get; init; } = "#0F172A";

    public double Opacity { get; init; } = 0.35;

    public Twip OffsetX { get; init; } = Twip.FromPoints(3);

    public Twip OffsetY { get; init; } = Twip.FromPoints(3);

    public Twip Blur { get; init; } = Twip.FromPoints(4);

    public static readonly ShapeShadow Default = new();
}

public sealed record ShapeGlow
{
    public bool Enabled { get; init; }

    public string ColorHex { get; init; } = "#38BDF8";

    public Twip Radius { get; init; } = Twip.FromPoints(6);

    public double Opacity { get; init; } = 0.6;

    public static readonly ShapeGlow Default = new();
}

public enum ExtrusionKind
{
    None,
    Extrude,
    Lathe,
}

public sealed record ShapeExtrusion
{
    public ExtrusionKind Kind { get; init; } = ExtrusionKind.None;

    public Twip Depth { get; init; } = Twip.FromCentimeters(1);

    public bool Enabled => Kind is not ExtrusionKind.None && Depth.Value > 0;

    public static readonly ShapeExtrusion None = new();
}

public sealed record ShapeBevel
{
    public BevelStyle Style { get; init; } = BevelStyle.None;

    public Twip Depth { get; init; } = Twip.FromPoints(4);

    public bool Enabled => Style is not BevelStyle.None;

    public static readonly ShapeBevel Default = new();
}

public sealed record ShapeTextStyle
{
    public string Family { get; init; } = "Helvetica";

    public double Size { get; init; } = 11;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public string ColorHex { get; init; } = "#FFFFFF";

    public bool AutomaticColor { get; init; }

    public TextHorizontalPlacement Horizontal { get; init; } = TextHorizontalPlacement.Centre;

    public TextVerticalPlacement Vertical { get; init; } = TextVerticalPlacement.Middle;

    public bool Wrap { get; init; } = true;

    public Twip Inset { get; init; } = Twip.FromPoints(4);

    public double RotationDegrees { get; init; }

    public string? Language { get; init; }

    public static readonly ShapeTextStyle Default = new();
}

public sealed record ShapeStyle
{
    public ShapeFill Fill { get; init; } = ShapeFill.Default;

    public ShapeLine Line { get; init; } = ShapeLine.Default;

    public ShapeShadow Shadow { get; init; } = ShapeShadow.Default;

    public ShapeGlow Glow { get; init; } = ShapeGlow.Default;

    public ShapeBevel Bevel { get; init; } = ShapeBevel.Default;

    public ShapeExtrusion Extrusion { get; init; } = ShapeExtrusion.None;

    public ShapeTextStyle Text { get; init; } = ShapeTextStyle.Default;

    public Twip SoftEdge { get; init; } = Twip.Zero;

    public static readonly ShapeStyle Default = new();
}
