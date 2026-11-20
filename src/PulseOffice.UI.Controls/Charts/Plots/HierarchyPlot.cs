using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;

namespace PulseOffice.UI.Controls.Charts.Plots;

public sealed class HierarchyPlot : ChartPlotBase
{
    private const double BannerHeight = 16;

    private readonly IChartHierarchy _hierarchy;

    public HierarchyPlot(IChartHierarchy hierarchy) => _hierarchy = hierarchy;

    public HierarchyPlot() : this(new ChartHierarchy()) { }

    public override bool Handles(ChartType type) => type is
        ChartType.Treemap or ChartType.Funnel or ChartType.Sunburst or ChartType.Sankey;

    public override bool DrawsAxes => false;

    public override void Draw(ChartFrame frame)
    {
        var values = Slice(frame);
        if (values.Count == 0) return;

        switch (frame.Definition.Type)
        {
            case ChartType.Funnel:
                DrawFunnel(frame, values);
                break;

            case ChartType.Sunburst:
                DrawSunburst(frame, _hierarchy.Roots(frame.Data));
                break;

            case ChartType.Treemap:
                DrawTreemap(frame, _hierarchy.Roots(frame.Data), frame.Plot, -1);
                break;

            default:
                DrawTreemap(frame, [.. values.Select(entry => new ChartHierarchyNode(Category(frame, entry.Index), entry.Value, entry.Index, []))], frame.Plot, -1);
                break;
        }
    }

    private List<(double Value, int Index)> Slice(ChartFrame frame) =>
        [.. (frame.Data.Series.FirstOrDefault()?.Values ?? [])
            .Select((value, index) => (Value: double.IsFinite(value) ? Math.Abs(value) : 0, Index: index))
            .Where(entry => entry.Value > 0)];

    private string Category(ChartFrame frame, int point) =>
        point >= 0 && point < frame.Data.Categories.Count ? frame.Data.Categories[point] : string.Empty;

    private void DrawTreemap(ChartFrame frame, IReadOnlyList<ChartHierarchyNode> nodes, Rect area, int colour)
    {
        var total = nodes.Sum(node => node.Value);
        if (total <= 0 || area.Width <= 0 || area.Height <= 0) return;

        var remaining = area;
        var left = total;

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var fraction = left <= 0 ? 1 : node.Value / left;
            var horizontal = remaining.Width >= remaining.Height;

            var slice = index == nodes.Count - 1
                ? remaining
                : horizontal
                    ? new Rect(remaining.X, remaining.Y, remaining.Width * fraction, remaining.Height)
                    : new Rect(remaining.X, remaining.Y, remaining.Width, remaining.Height * fraction);

            var tint = colour < 0 ? (node.IsLeaf ? node.Point : index) : colour;
            if (node.IsLeaf) DrawLeaf(frame, node, slice, tint);
            else DrawParent(frame, node, slice, tint);

            remaining = horizontal
                ? new Rect(slice.Right, remaining.Y, Math.Max(0, remaining.Width - slice.Width), remaining.Height)
                : new Rect(remaining.X, slice.Bottom, remaining.Width, Math.Max(0, remaining.Height - slice.Height));

            left -= node.Value;
        }
    }

    private void DrawLeaf(ChartFrame frame, ChartHierarchyNode node, Rect slice, int colour)
    {
        frame.Context.DrawRectangle(Brush(frame.Data, colour, 0.85), new Pen(frame.Marks.Separator, 1), slice.Deflate(1));
        if (slice.Width > 40 && slice.Height > 18) Label(frame, node.Label, new Point(slice.X + 4, slice.Y + 4), 10);
    }

    private void DrawParent(ChartFrame frame, ChartHierarchyNode node, Rect slice, int colour)
    {
        var labels = frame.Definition.Statistical.ParentLabels;
        var banner = labels == ChartParentLabels.Banner && slice.Height > BannerHeight * 2;
        if (banner)
        {
            var strip = new Rect(slice.X, slice.Y, slice.Width, BannerHeight);
            frame.Context.DrawRectangle(Brush(frame.Data, colour), new Pen(frame.Marks.Separator, 1), strip.Deflate(1));
            if (slice.Width > 30) Label(frame, node.Label, new Point(strip.X + 4, strip.Y + 2), 9);
            DrawTreemap(frame, node.Children, new Rect(slice.X, slice.Y + BannerHeight, slice.Width, slice.Height - BannerHeight), colour);
            return;
        }

        DrawTreemap(frame, node.Children, slice, colour);
        frame.Context.DrawRectangle(null, new Pen(frame.Marks.Separator, 2), slice.Deflate(0.5));
        if (labels == ChartParentLabels.Overlapping && slice.Width > 40 && slice.Height > 18)
            Label(frame, node.Label, new Point(slice.X + 4, slice.Y + slice.Height / 2 - 6), 11);
    }

    private void DrawFunnel(ChartFrame frame, IReadOnlyList<(double Value, int Index)> values)
    {
        var largest = values.Max(entry => entry.Value);
        if (largest <= 0) return;

        var height = frame.Plot.Height / values.Count;

        for (var index = 0; index < values.Count; index++)
        {
            var width = frame.Plot.Width * (values[index].Value / largest);

            frame.Context.FillRectangle(
                Brush(frame.Data, values[index].Index, 0.85),
                new Rect(
                    frame.Plot.Center.X - width / 2,
                    frame.Plot.Y + height * index + 2,
                    width,
                    Math.Max(1, height - 4)));
        }
    }

    private void DrawSunburst(ChartFrame frame, IReadOnlyList<ChartHierarchyNode> roots)
    {
        var total = roots.Sum(node => node.Value);
        if (total <= 0) return;

        var depth = Depth(roots);
        var centre = frame.Plot.Center;
        var radius = Math.Min(frame.Plot.Width, frame.Plot.Height) / 2 * 0.9;
        var hole = radius * 0.25;
        var ring = (radius - hole) / Math.Max(1, depth);

        DrawRing(frame, roots, centre, hole, ring, -Math.PI / 2, 2 * Math.PI, total, -1);
    }

    private int Depth(IReadOnlyList<ChartHierarchyNode> nodes) =>
        nodes.Count == 0 ? 0 : 1 + nodes.Max(node => Depth(node.Children));

    private void DrawRing(ChartFrame frame, IReadOnlyList<ChartHierarchyNode> nodes, Point centre, double inner, double ring,
        double start, double span, double total, int colour)
    {
        if (total <= 0) return;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var sweep = span * node.Value / total;
            var tint = colour < 0 ? (node.IsLeaf ? node.Point : index) : colour;
            DrawSegment(frame, centre, inner, inner + ring, start, sweep, Brush(frame.Data, tint, node.IsLeaf ? 0.9 : 1));
            if (!node.IsLeaf) DrawRing(frame, node.Children, centre, inner + ring, ring, start, sweep, node.Value, tint);
            start += sweep;
        }
    }

    private void DrawSegment(ChartFrame frame, Point centre, double inner, double radius, double start, double sweep, IBrush brush)
    {
        if (sweep >= 2 * Math.PI - 1e-9)
        {
            var outer = new EllipseGeometry(new Rect(centre.X - radius, centre.Y - radius, radius * 2, radius * 2));
            var hole = new EllipseGeometry(new Rect(centre.X - inner, centre.Y - inner, inner * 2, inner * 2));
            frame.Context.DrawGeometry(brush, new Pen(frame.Marks.Separator, 1), new CombinedGeometry(GeometryCombineMode.Exclude, outer, hole));
            return;
        }

        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(Polar(centre, inner, start), true);
            sink.LineTo(Polar(centre, radius, start));
            sink.ArcTo(Polar(centre, radius, start + sweep), new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
            sink.LineTo(Polar(centre, inner, start + sweep));
            sink.ArcTo(Polar(centre, inner, start), new Size(inner, inner), 0, sweep > Math.PI, SweepDirection.CounterClockwise);
            sink.EndFigure(true);
        }
        frame.Context.DrawGeometry(brush, new Pen(frame.Marks.Separator, 1), geometry);
    }
}
