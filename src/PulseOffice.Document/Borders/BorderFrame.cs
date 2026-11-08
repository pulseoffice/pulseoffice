using System.Collections.Immutable;

namespace PulseOffice.Document.Borders;

public enum BorderDash
{
    Solid,
    Dotted,
    Dashed,
    Double,
}

public enum BorderPosition
{
    Top,
    Bottom,
    Left,
    Right,
    InsideHorizontal,
    InsideVertical,
    DiagonalDown,
    DiagonalUp,
}

public sealed record BorderStroke(BorderDash Dash, double WidthPoints, string ColorHex);

public sealed record BorderFrame
{
    public static readonly BorderFrame Empty = new();

    private ImmutableDictionary<BorderPosition, BorderStroke> _strokes = ImmutableDictionary<BorderPosition, BorderStroke>.Empty;

    public BorderStroke? Stroke(BorderPosition position) => _strokes.GetValueOrDefault(position);

    public bool Has(BorderPosition position) => _strokes.ContainsKey(position);

    public IEnumerable<BorderPosition> Positions => _strokes.Keys.OrderBy(position => position);

    public bool IsEmpty => _strokes.Count == 0;

    public BorderFrame With(BorderPosition position, BorderStroke? stroke) => this with
    {
        _strokes = stroke is null ? _strokes.Remove(position) : _strokes.SetItem(position, stroke),
    };

    public bool Equals(BorderFrame? other) =>
        other is not null && _strokes.Count == other._strokes.Count
        && _strokes.All(pair => other._strokes.TryGetValue(pair.Key, out var stroke) && stroke == pair.Value);

    public override int GetHashCode() => _strokes.Count;
}

public sealed record BorderFrameScope(bool InsideHorizontal, bool InsideVertical, bool Diagonals)
{
    public IReadOnlyList<BorderDash> Dashes { get; init; } = [BorderDash.Solid, BorderDash.Dotted, BorderDash.Dashed, BorderDash.Double];

    public bool IsRange => InsideHorizontal || InsideVertical;

    public bool Allows(BorderPosition position) => position switch
    {
        BorderPosition.InsideHorizontal => InsideHorizontal,
        BorderPosition.InsideVertical => InsideVertical,
        BorderPosition.DiagonalDown or BorderPosition.DiagonalUp => Diagonals,
        _ => true,
    };

    public IEnumerable<BorderPosition> Positions => Enum.GetValues<BorderPosition>().Where(Allows);
}
