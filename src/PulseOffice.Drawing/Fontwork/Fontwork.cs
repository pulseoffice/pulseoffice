using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Drawing.Fontwork;

public enum FontworkWarp
{
    Plain,
    ArchUp,
    ArchDown,
    Circle,
    Wave,
    SlantUp,
    SlantDown,
    Triangle,
}

public sealed record FontworkObject : EmbeddedObject, IDrawingObject
{
    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitFontwork(this);

    public required string Text { get; init; }

    public FontworkWarp Warp { get; init; } = FontworkWarp.ArchUp;

    public ShapeStyle Style { get; init; } = ShapeStyle.Default;

    public string FontFamily { get; init; } = "Inter";

    public bool Bold { get; init; } = true;

    public bool Italic { get; init; }

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Shape;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];
}

public readonly record struct PathPlacement(double X, double Y, double AngleRadians);

public sealed class FontworkPath
{
    private const int Samples = 240;

    public PathPlacement At(FontworkWarp warp, double t, double aspect = 1)
    {
        var (x, y) = Point(warp, t);
        var (nx, ny) = Point(warp, Math.Min(1, t + 0.001));
        var (px, py) = Point(warp, Math.Max(0, t - 0.001));
        return new PathPlacement(x, y, Math.Atan2((ny - py) * aspect, nx - px));
    }

    public double Length(FontworkWarp warp, double aspect = 1)
    {
        var total = 0.0;
        for (var i = 1; i <= Samples; i++)
        {
            var (ax, ay) = Point(warp, (i - 1) / (double)Samples);
            var (bx, by) = Point(warp, i / (double)Samples);
            total += Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay) * aspect * aspect);
        }
        return total;
    }

    public IReadOnlyList<PathPlacement> Place(FontworkWarp warp, IReadOnlyList<double> advances, double aspect = 1)
    {
        ArgumentNullException.ThrowIfNull(advances);
        var lengths = new double[Samples + 1];
        for (var i = 1; i <= Samples; i++)
        {
            var (ax, ay) = Point(warp, (i - 1) / (double)Samples);
            var (bx, by) = Point(warp, i / (double)Samples);
            lengths[i] = lengths[i - 1] + Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay) * aspect * aspect);
        }

        var total = lengths[^1];
        var used = advances.Sum();
        var scale = used > total ? total / used : 1;
        var gap = 0.0;
        var cursor = Math.Max(0, (total - used * scale) / 2);

        var result = new List<PathPlacement>(advances.Count);
        foreach (var advance in advances)
        {
            var centre = cursor + advance * scale / 2;
            result.Add(At(warp, Parameter(lengths, centre), aspect));
            cursor += advance * scale + gap;
        }
        return result;
    }

    private double Parameter(double[] lengths, double distance)
    {
        for (var i = 1; i < lengths.Length; i++)
            if (lengths[i] >= distance)
            {
                var span = lengths[i] - lengths[i - 1];
                var local = span <= 0 ? 0 : (distance - lengths[i - 1]) / span;
                return (i - 1 + local) / (lengths.Length - 1);
            }
        return 1;
    }

    private (double X, double Y) Point(FontworkWarp warp, double t) => warp switch
    {
        FontworkWarp.ArchUp => (0.5 - 0.5 * Math.Cos(Math.PI * t), 0.95 - 0.8 * Math.Sin(Math.PI * t)),
        FontworkWarp.ArchDown => (0.5 - 0.5 * Math.Cos(Math.PI * t), 0.05 + 0.8 * Math.Sin(Math.PI * t)),
        FontworkWarp.Circle => (0.5 + 0.45 * Math.Cos(Math.PI + 2 * Math.PI * t), 0.5 + 0.45 * Math.Sin(Math.PI + 2 * Math.PI * t)),
        FontworkWarp.Wave => (t, 0.6 + 0.2 * Math.Sin(2 * Math.PI * t)),
        FontworkWarp.SlantUp => (t, 0.85 - 0.6 * t),
        FontworkWarp.SlantDown => (t, 0.25 + 0.6 * t),
        FontworkWarp.Triangle => (t, t < 0.5 ? 0.85 - 1.2 * t : 0.25 + 1.2 * (t - 0.5)),
        _ => (t, 0.7),
    };
}
