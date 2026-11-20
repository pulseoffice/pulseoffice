using Avalonia;
using Avalonia.Media;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI;

public interface ISparklinePainter
{
    void Paint(DrawingContext context, Rect bounds, SparklinePoints points, SparklineKind kind, SparklineStyle style);
}

public sealed class SparklinePainter(ISparklineBars bars) : ISparklinePainter
{
    public SparklinePainter() : this(new SparklineBars())
    {
    }

    public void Paint(DrawingContext context, Rect bounds, SparklinePoints points, SparklineKind kind, SparklineStyle style)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(style);
        if (points.IsEmpty) return;

        var area = bounds.Deflate(new Thickness(2));
        if (area.Width <= 2 || area.Height <= 2) return;

        var colour = Brush(style.ColorHex);
        var negative = Brush(style.NegativeHex);
        var markers = MarkersOf(points, style, Brush(style.MarkerHex));
        if (style.ShowAxis && kind != SparklineKind.Stacked) Axis(context, area, points, style);

        if (kind == SparklineKind.Line)
        {
            Line(context, area, points, style, colour, markers);
            return;
        }

        var count = points.Values.Count;
        var width = area.Width / Math.Max(1, count);
        var slots = area.Width - width;
        foreach (var bar in bars.Layout(points, kind, area.Top, area.Bottom))
            context.FillRectangle(
                bar.Negative && (style.ShowNegative || kind == SparklineKind.Stacked) ? negative : markers.At(points, bar.Index) ?? colour,
                new Rect(area.X + points.Position(bar.Index) * slots + width * 0.15, bar.Top, Math.Max(1, width * 0.7), bar.Bottom - bar.Top));
    }

    private void Axis(DrawingContext context, Rect area, SparklinePoints points, SparklineStyle style)
    {
        if (points.Minimum >= 0 || points.Maximum <= 0) return;
        var span = Math.Max(1e-9, points.Maximum - points.Minimum);
        var y = Math.Floor(area.Bottom - (0 - points.Minimum) / span * area.Height) + 0.5;
        context.DrawLine(new Pen(Brush(style.AxisHex), 0.75), new Point(area.Left, y), new Point(area.Right, y));
    }

    private void Line(DrawingContext context, Rect area, SparklinePoints points, SparklineStyle style, IBrush colour, Markers markers)
    {
        var count = points.Values.Count;
        var span = Math.Max(1e-9, points.Maximum - points.Minimum);
        Point At(int index, double value) => new(
            area.X + area.Width * points.Position(index),
            area.Bottom - Math.Clamp((value - points.Minimum) / span, 0, 1) * area.Height);

        var geometry = new StreamGeometry();
        var started = false;
        var open = false;
        using (var draw = geometry.Open())
        {
            for (var index = 0; index < count; index++)
            {
                if (points.Values[index] is not { } value)
                {
                    if (!points.SpansGaps && open)
                    {
                        draw.EndFigure(false);
                        open = false;
                    }
                    continue;
                }
                var point = At(index, value);
                if (!open)
                {
                    draw.BeginFigure(point, false);
                    started = true;
                    open = true;
                }
                else
                {
                    draw.LineTo(point);
                }
            }
            if (open) draw.EndFigure(false);
        }
        if (!started) return;
        context.DrawGeometry(null, new Pen(colour, style.LineWidth), geometry);

        var radius = Math.Clamp(area.Height / 10, 1.5, 3);
        for (var index = 0; index < count; index++)
        {
            if (points.Values[index] is not { } value) continue;
            if (markers.Line(points, index) is { } marker) context.DrawEllipse(marker, null, At(index, value), radius, radius);
        }
    }

    private Markers MarkersOf(SparklinePoints points, SparklineStyle style, IBrush marker)
    {
        var high = double.NegativeInfinity;
        var low = double.PositiveInfinity;
        var first = -1;
        var last = -1;
        for (var index = 0; index < points.Values.Count; index++)
        {
            if (points.Values[index] is not { } value) continue;
            high = Math.Max(high, value);
            low = Math.Min(low, value);
            if (first < 0) first = index;
            last = index;
        }

        return new Markers(style, marker, high, low, first, last, Brush);
    }

    private sealed record Markers(SparklineStyle Style, IBrush Marker, double High, double Low, int First, int Last, Func<string?, IBrush> Paint)
    {
        public IBrush? At(SparklinePoints points, int index)
        {
            if (points.Values[index] is not { } value) return null;
            if (Style.ShowHighPoint && value >= High) return Own(Style.HighHex);
            if (Style.ShowLowPoint && value <= Low) return Own(Style.LowHex);
            if (Style.ShowFirstPoint && index == First) return Own(Style.FirstHex);
            if (Style.ShowLastPoint && index == Last) return Own(Style.LastHex);
            return null;
        }

        public IBrush? Line(SparklinePoints points, int index) =>
            At(points, index) ?? (Style.ShowMarkers && points.Values[index] is not null ? Marker : null);

        private IBrush Own(string? hex) => hex is null ? Marker : Paint(hex);
    }

    private IBrush Brush(string? hex) =>
        hex is not null && Color.TryParse(hex, out var colour) ? new SolidColorBrush(colour) : Brushes.SteelBlue;
}
