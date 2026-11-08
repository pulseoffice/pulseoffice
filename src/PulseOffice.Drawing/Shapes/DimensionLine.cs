using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Shapes;

public enum DimensionPlacing
{
    Above,
    Below,
}

public sealed record DimensionLine
{
    public const string AutomaticUnit = "automatic";
    public const string AutomaticAlignment = "automatic";

    public required ShapePoint Start { get; init; }

    public required ShapePoint End { get; init; }

    public Twip LineDistance { get; init; } = Twip.FromCentimeters(0.8);

    public Twip GuideOverhang { get; init; } = Twip.FromCentimeters(0.2);

    public Twip GuideDistance { get; init; } = Twip.FromCentimeters(0.1);

    public Twip StartGuide { get; init; }

    public Twip EndGuide { get; init; }

    public DimensionPlacing Placing { get; init; } = DimensionPlacing.Above;

    public bool Parallel { get; init; } = true;

    public string Unit { get; init; } = AutomaticUnit;

    public bool ShowUnit { get; init; }

    public int DecimalPlaces { get; init; } = 2;

    public string HorizontalAlignment { get; init; } = AutomaticAlignment;

    public string VerticalAlignment { get; init; } = AutomaticAlignment;

    public string? Label { get; init; }
}

public sealed record DimensionLayout(
    ShapePoint LineStart,
    ShapePoint LineEnd,
    ShapePoint FirstGuideFrom,
    ShapePoint FirstGuideTo,
    ShapePoint SecondGuideFrom,
    ShapePoint SecondGuideTo,
    double MeasuredLength)
{
    public ShapePoint Middle => new((LineStart.X + LineEnd.X) / 2, (LineStart.Y + LineEnd.Y) / 2);

    public double LineLength => Math.Sqrt(Math.Pow(LineEnd.X - LineStart.X, 2) + Math.Pow(LineEnd.Y - LineStart.Y, 2));

    public double ReadableAngleDegrees
    {
        get
        {
            var degrees = Math.Atan2(LineEnd.Y - LineStart.Y, LineEnd.X - LineStart.X) * 180 / Math.PI;
            if (degrees >= 90) degrees -= 180;
            else if (degrees < -90) degrees += 180;
            return degrees;
        }
    }

    public IReadOnlyList<ShapePoint> Points => [LineStart, LineEnd, FirstGuideFrom, FirstGuideTo, SecondGuideFrom, SecondGuideTo];
}

public sealed record DimensionFrame(double X, double Y, double Width, double Height, ShapePoint Start, ShapePoint End);
