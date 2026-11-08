using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal interface IGridPaints
{
    IBrush? Brush(string? hex);

    IBrush Brush(Color color);

    IPen Pen(Color color, double thickness, IDashStyle? dash = null);
}

internal sealed class GridPaints : IGridPaints
{
    private const int Capacity = 1024;

    private readonly Dictionary<string, IBrush?> _parsed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Color, IBrush> _brushes = [];
    private readonly Dictionary<(Color Color, double Thickness, IDashStyle? Dash), IPen> _pens = [];

    public IBrush? Brush(string? hex)
    {
        if (hex is null) return null;
        if (_parsed.TryGetValue(hex, out var known)) return known;

        if (_parsed.Count >= Capacity) _parsed.Clear();

        var brush = Color.TryParse(hex, out var color) ? Brush(color) : null;
        _parsed[hex] = brush;
        return brush;
    }

    public IBrush Brush(Color color)
    {
        if (_brushes.TryGetValue(color, out var brush)) return brush;

        if (_brushes.Count >= Capacity) _brushes.Clear();

        brush = new ImmutableSolidColorBrush(color);
        _brushes[color] = brush;
        return brush;
    }

    public IPen Pen(Color color, double thickness, IDashStyle? dash = null)
    {
        var key = (color, thickness, dash);
        if (_pens.TryGetValue(key, out var pen)) return pen;

        if (_pens.Count >= Capacity) _pens.Clear();

        pen = new ImmutablePen(new ImmutableSolidColorBrush(color), thickness, dash?.ToImmutable());
        _pens[key] = pen;
        return pen;
    }
}
