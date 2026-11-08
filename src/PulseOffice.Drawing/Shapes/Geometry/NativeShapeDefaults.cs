namespace PulseOffice.Drawing.Shapes.Geometry;

public sealed record NativeShapeDefault(string Type, IReadOnlyList<double> Modifiers);

public sealed class NativeShapeDefaultTable
{
    public IReadOnlyList<NativeShapeDefault> All { get; } =
    [
        new("round-rectangle", [3600]),
        new("isosceles-triangle", [10800]),
        new("parallelogram", [5400]),
        new("trapezoid", [5400]),
        new("hexagon", [5400]),
        new("octagon", [6326]),
        new("cross", [5400]),
        new("right-arrow", [16200, 5400]),
        new("pentagon-right", [16200]),
        new("cube", [5400]),
        new("can", [5400]),
        new("ring", [5400]),
        new("line-callout-1", [-8300, 24500, -1800, 4000]),
        new("line-callout-2", [-10000, 24500, -3600, 4000, -1800, 4000]),
        new("chevron", [16200]),
        new("forbidden", [2700]),
        new("star8", [2500]),
        new("rectangular-callout", [1400, 25920]),
        new("round-rectangular-callout", [1400, 25920]),
        new("round-callout", [1350, 25920]),
        new("paper", [18900]),
        new("left-arrow", [5400, 5400]),
        new("down-arrow", [16200, 5400]),
        new("up-arrow", [5400, 5400]),
        new("left-right-arrow", [4300, 5400]),
        new("up-down-arrow", [5400, 4300]),
        new("quad-arrow", [6500, 8600, 4300]),
        new("left-arrow-callout", [7200, 5400, 3600, 8100]),
        new("right-arrow-callout", [14400, 5400, 18000, 8100]),
        new("up-arrow-callout", [7200, 5400, 3600, 8100]),
        new("down-arrow-callout", [14400, 5400, 18000, 8100]),
        new("left-right-arrow-callout", [5400, 5500, 2700, 8100]),
        new("up-down-arrow-callout", [5400, 5500, 2700, 8100]),
        new("quad-arrow-callout", [5400, 8100, 2700, 9400]),
        new("quad-bevel", [2700]),
        new("left-bracket", [1800]),
        new("right-bracket", [1800]),
        new("star24", [2500]),
        new("striped-right-arrow", [16200, 5400]),
        new("notched-right-arrow", [16200, 5400]),
        new("block-arc", [180, 5400]),
        new("horizontal-scroll", [2700]),
        new("circular-arrow", [180, 0, 5500]),
        new("cloud-callout", [1350, 25920]),
        new("fontwork-plain-text", [10800]),
        new("fontwork-stop", [2700]),
        new("fontwork-triangle-up", [10800]),
        new("fontwork-triangle-down", [10800]),
        new("fontwork-chevron-up", [5400]),
        new("fontwork-chevron-down", [16200]),
        new("fontwork-arch-up-curve", [180]),
        new("fontwork-arch-down-curve", [0]),
        new("fontwork-circle-curve", [-179]),
        new("fontwork-open-circle-curve", [180]),
        new("fontwork-arch-up-pour", [180, 5400]),
        new("fontwork-arch-down-pour", [0, 5400]),
        new("fontwork-circle-pour", [-179, 5400]),
        new("fontwork-open-circle-pour", [180, 5400]),
        new("fontwork-curve-up", [9900]),
        new("fontwork-curve-down", [9900]),
        new("fontwork-fade-up-and-right", [9600]),
        new("fontwork-fade-up-and-left", [9600]),
        new("fontwork-wave", [1400, 10800]),
        new("fontwork-inflate", [2950]),
        new("fontwork-fade-right", [7200]),
        new("fontwork-fade-left", [7200]),
        new("fontwork-fade-up", [7200]),
        new("fontwork-fade-down", [7200]),
        new("fontwork-slant-up", [12000]),
        new("fontwork-slant-down", [12000]),
        new("line-callout-3", [-1800, 24500, -1800, 4000]),
        new("sun", [5400]),
        new("moon", [10800]),
        new("bracket-pair", [3700]),
        new("brace-pair", [1800]),
        new("star4", [8100]),
    ];
}

public interface INativeShapeDefaults
{
    bool Knows(string? type);

    bool AreDefault(string? type, IReadOnlyList<double> modifiers);
}

public sealed class NativeShapeDefaults(IReadOnlyList<NativeShapeDefault> rows) : INativeShapeDefaults
{
    private const double Tolerance = 0.5;

    private readonly IReadOnlyDictionary<string, NativeShapeDefault> _rows = rows.ToDictionary(row => row.Type, StringComparer.Ordinal);

    public NativeShapeDefaults() : this(new NativeShapeDefaultTable().All) { }

    public bool Knows(string? type) => type is not null && _rows.ContainsKey(type);

    public bool AreDefault(string? type, IReadOnlyList<double> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        if (type is null || !_rows.TryGetValue(type, out var row)) return true;
        for (var index = 0; index < modifiers.Count; index++)
            if (index < row.Modifiers.Count && Math.Abs(modifiers[index] - row.Modifiers[index]) > Tolerance) return false;
        return true;
    }
}
