using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Shapes;

public interface IGradientStopEditing
{
    ShapeGradient Add(ShapeGradient gradient, double position);

    ShapeGradient Remove(ShapeGradient gradient, int index);

    ShapeGradient Move(ShapeGradient gradient, int index, double position);

    ShapeGradient Recolor(ShapeGradient gradient, int index, string colorHex);

    ShapeGradient Reverse(ShapeGradient gradient);

    string ColorAt(ShapeGradient gradient, double position);
}

public sealed class GradientStopEditing : IGradientStopEditing
{
    private const int MinimumStops = 2;

    public ShapeGradient Add(ShapeGradient gradient, double position)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        if (double.IsNaN(position)) return gradient;
        var at = Math.Clamp(position, 0, 1);
        return Sorted(gradient, [.. gradient.Stops, new GradientStop(ColorAt(gradient, at), at)]);
    }

    public ShapeGradient Remove(ShapeGradient gradient, int index)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        if (gradient.Stops.Count <= MinimumStops || index < 0 || index >= gradient.Stops.Count) return gradient;
        return gradient with { Stops = [.. gradient.Stops.Where((_, i) => i != index)] };
    }

    public ShapeGradient Move(ShapeGradient gradient, int index, double position)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        if (index < 0 || index >= gradient.Stops.Count || double.IsNaN(position)) return gradient;
        return Sorted(gradient, [.. gradient.Stops.Select((stop, i) => i == index ? stop with { Position = Math.Clamp(position, 0, 1) } : stop)]);
    }

    public ShapeGradient Recolor(ShapeGradient gradient, int index, string colorHex)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        if (index < 0 || index >= gradient.Stops.Count || string.IsNullOrWhiteSpace(colorHex)) return gradient;
        return gradient with { Stops = [.. gradient.Stops.Select((stop, i) => i == index ? stop with { ColorHex = colorHex } : stop)] };
    }

    public ShapeGradient Reverse(ShapeGradient gradient)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        return Sorted(gradient, [.. gradient.Stops.Select(stop => stop with { Position = 1 - stop.Position })]);
    }

    public string ColorAt(ShapeGradient gradient, double position)
    {
        ArgumentNullException.ThrowIfNull(gradient);
        var stops = gradient.Stops.OrderBy(stop => stop.Position).ToList();
        if (stops.Count == 0) return "#FFFFFF";
        if (double.IsNaN(position) || position <= stops[0].Position) return stops[0].ColorHex;
        if (position >= stops[^1].Position) return stops[^1].ColorHex;
        var right = stops.FindIndex(stop => stop.Position >= position);
        var a = stops[right - 1];
        var b = stops[right];
        var span = b.Position - a.Position;
        var t = span <= 0 ? 0 : (position - a.Position) / span;
        return Mix(a.ColorHex, b.ColorHex, t);
    }

    private ShapeGradient Sorted(ShapeGradient gradient, IEnumerable<GradientStop> stops) =>
        gradient with { Stops = [.. stops.OrderBy(stop => stop.Position)] };

    private string Mix(string from, string to, double t)
    {
        var a = Rgb(from);
        var b = Rgb(to);
        int Channel(int shift) => (int)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);
        return $"#{Channel(16):X2}{Channel(8):X2}{Channel(0):X2}";
    }

    private int Rgb(string hex)
    {
        var digits = hex.TrimStart('#');
        if (digits.Length == 8) digits = digits[2..];
        return int.TryParse(digits, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}

public sealed record FillPattern(string Key, HatchKind Kind, double AngleDegrees, double DistancePoints);

public interface IFillPatternCatalog
{
    IReadOnlyList<FillPattern> Patterns { get; }

    ShapeHatch Apply(FillPattern pattern, ShapeHatch current);

    FillPattern? Match(ShapeHatch hatch);
}

public sealed class FillPatternCatalog : IFillPatternCatalog
{
    private const double Tolerance = 0.01;

    public IReadOnlyList<FillPattern> Patterns { get; } =
    [
        new("horizontal", HatchKind.Single, 0, 4),
        new("vertical", HatchKind.Single, 90, 4),
        new("diagonalUp", HatchKind.Single, 45, 4),
        new("diagonalDown", HatchKind.Single, 135, 4),
        new("narrowHorizontal", HatchKind.Single, 0, 2),
        new("narrowVertical", HatchKind.Single, 90, 2),
        new("wideDiagonal", HatchKind.Single, 45, 8),
        new("grid", HatchKind.Double, 0, 4),
        new("smallGrid", HatchKind.Double, 0, 2),
        new("diagonalGrid", HatchKind.Double, 45, 4),
        new("trellis", HatchKind.Triple, 45, 4),
        new("dense", HatchKind.Triple, 0, 2),
    ];

    public ShapeHatch Apply(FillPattern pattern, ShapeHatch current)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(current);
        return current with { Kind = pattern.Kind, AngleDegrees = pattern.AngleDegrees, Distance = Twip.FromPoints(pattern.DistancePoints) };
    }

    public FillPattern? Match(ShapeHatch hatch)
    {
        ArgumentNullException.ThrowIfNull(hatch);
        return Patterns.FirstOrDefault(pattern => pattern.Kind == hatch.Kind
            && Math.Abs(pattern.AngleDegrees - hatch.AngleDegrees) < Tolerance
            && Math.Abs(pattern.DistancePoints - hatch.Distance.ToPoints()) < 0.1);
    }
}
