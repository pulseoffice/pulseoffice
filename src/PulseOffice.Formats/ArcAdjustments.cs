using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Formats;

public sealed class ArcAdjustments
{
    private const double UnitsPerDegree = 60000;
    private const double FullTurn = 360;

    public bool Applies(ShapeKind kind) => kind is ShapeKind.Arc or ShapeKind.Pie or ShapeKind.Chord;

    public (long First, long Second) ToOoxml(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return (Units(FullTurn - shape.ArcEndDegrees), Units(FullTurn - shape.ArcStartDegrees));
    }

    public (double Start, double End) FromOoxml(ShapeKind kind, long? first, long? second)
    {
        var (defaultFirst, defaultSecond) = kind switch
        {
            ShapeKind.Arc => (16200000L, 0L),
            ShapeKind.Chord => (2700000L, 16200000L),
            _ => (0L, 16200000L),
        };
        return (Normal(FullTurn - (second ?? defaultSecond) / UnitsPerDegree), Normal(FullTurn - (first ?? defaultFirst) / UnitsPerDegree));
    }

    private long Units(double degrees) => (long)Math.Round(Normal(degrees) * UnitsPerDegree);

    private double Normal(double degrees) => ((degrees % FullTurn) + FullTurn) % FullTurn;
}
