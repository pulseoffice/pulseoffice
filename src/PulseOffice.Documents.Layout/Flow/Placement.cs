using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record ObjectFrame(PageGeometry Page, double ColumnLeft, double ColumnWidth, double ParagraphTop);

public interface IObjectPlacement
{
    LayoutRect Place(AnchoredObject item, ObjectFrame frame);
}

public sealed class ObjectPlacement : IObjectPlacement
{
    public LayoutRect Place(AnchoredObject item, ObjectFrame frame)
    {
        var width = item.Size.Width.ToPoints();
        var height = item.Size.Height.ToPoints();
        var x = item.Placement.X.ToPoints() + item.Placement.Horizontal switch
        {
            HorizontalRelation.Page => 0,
            HorizontalRelation.PageArea or HorizontalRelation.Margin => frame.Page.Content.X,
            _ => frame.ColumnLeft,
        };
        var y = item.Placement.Y.ToPoints() + item.Placement.Vertical switch
        {
            VerticalRelation.Page => 0,
            VerticalRelation.PageArea or VerticalRelation.Margin => frame.Page.Content.Y,
            _ => frame.ParagraphTop,
        };
        return new LayoutRect(x, y, width, height);
    }
}

public readonly record struct WrapSegment(double Left, double Right)
{
    public double Width => Right - Left;
}

public readonly record struct OutlinePoint(double X, double Y);

public sealed record WrapObstacle(LayoutRect Rect, WrapMode Mode)
{
    public IReadOnlyList<OutlinePoint>? Outline { get; init; }

    public PulseOffice.Foundation.Units.Margins Spread { get; init; } = PulseOffice.Foundation.Units.Margins.Zero;
}

public interface IObjectOutlines
{
    IReadOnlyList<OutlinePoint>? Outline(AnchoredObject item, LayoutRect rect);
}

public sealed class ObjectOutlines : IObjectOutlines
{
    private const int EllipseSteps = 48;

    public IReadOnlyList<OutlinePoint>? Outline(AnchoredObject item, LayoutRect rect)
    {
        if (!item.WrapContour || item.Anchor is AsCharacterAnchor) return null;
        var unit = item.Contour is { Points.Count: >= 3 } contour
            ? contour.Points.Select(point => new OutlinePoint(point.X, point.Y)).ToList()
            : Derived(item);
        if (unit is null) return null;
        var radians = double.IsFinite(item.RotationDegrees) ? item.RotationDegrees * Math.PI / 180 : 0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        return [.. unit.Select(point =>
        {
            var x = (item.FlipHorizontal ? 1 - point.X : point.X) * rect.Width + rect.X - cx;
            var y = (item.FlipVertical ? 1 - point.Y : point.Y) * rect.Height + rect.Y - cy;
            return new OutlinePoint(cx + x * cos - y * sin, cy + x * sin + y * cos);
        })];
    }

    private List<OutlinePoint>? Derived(AnchoredObject item) => item.Content switch
    {
        EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject { Shape: PulseOffice.Drawing.Shapes.ShapeKind.Ellipse } } => Ellipse(),
        EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject { Shape: PulseOffice.Drawing.Shapes.ShapeKind.Triangle } } => [new(0.5, 0), new(1, 1), new(0, 1)],
        EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject { Shape: PulseOffice.Drawing.Shapes.ShapeKind.Diamond } } => [new(0.5, 0), new(1, 0.5), new(0.5, 1), new(0, 0.5)],
        _ => null,
    };

    private List<OutlinePoint> Ellipse() =>
        [.. Enumerable.Range(0, EllipseSteps).Select(step => step * 2 * Math.PI / EllipseSteps).Select(angle => new OutlinePoint(0.5 + 0.5 * Math.Cos(angle), 0.5 + 0.5 * Math.Sin(angle)))];
}

public interface IWrapSegments
{
    IReadOnlyList<WrapSegment> Free(IReadOnlyList<WrapObstacle> obstacles, double columnLeft, double columnWidth, double top, double height);
}

public sealed class WrapSegments(PageFlowOptions options) : IWrapSegments
{
    public IReadOnlyList<WrapSegment> Free(IReadOnlyList<WrapObstacle> obstacles, double columnLeft, double columnWidth, double top, double height)
    {
        var right = columnLeft + columnWidth;
        var free = new List<WrapSegment> { new(columnLeft, right) };
        foreach (var obstacle in obstacles)
        {
            var rect = obstacle.Rect;
            if (rect.Y >= top + height || rect.Bottom <= top || rect.Right <= columnLeft || rect.X >= right) continue;
            if (Extent(obstacle, top, height) is not { } extent) continue;
            var blocked = obstacle.Mode switch
            {
                WrapMode.Through => (WrapSegment?)null,
                WrapMode.None or WrapMode.TopAndBottom => new WrapSegment(columnLeft, right),
                WrapMode.Left => new WrapSegment(extent.Left, right),
                WrapMode.Right => new WrapSegment(columnLeft, extent.Right),
                WrapMode.Optimal => extent.Left - columnLeft >= right - extent.Right ? new WrapSegment(extent.Left, right) : new WrapSegment(columnLeft, extent.Right),
                _ => extent,
            };
            if (blocked is { } cut) free = [.. free.SelectMany(segment => Subtract(segment, cut))];
        }
        return [.. free.Where(segment => segment.Width >= options.MinimumWrapWidth).Select(segment => new WrapSegment(segment.Left - columnLeft, segment.Right - columnLeft))];
    }

    private WrapSegment? Extent(WrapObstacle obstacle, double top, double height)
    {
        if (obstacle.Outline is not { Count: >= 3 } outline) return new WrapSegment(obstacle.Rect.X, obstacle.Rect.Right);
        var spread = obstacle.Spread;
        var low = top - spread.Bottom.ToPoints();
        var high = top + height + spread.Top.ToPoints();
        var left = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        for (var i = 0; i < outline.Count; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Count];
            var from = Math.Max(low, Math.Min(a.Y, b.Y));
            var to = Math.Min(high, Math.Max(a.Y, b.Y));
            if (from > to) continue;
            foreach (var y in new[] { from, to })
            {
                var x = Math.Abs(b.Y - a.Y) < 1e-9 ? a.X : a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                var minimum = Math.Abs(b.Y - a.Y) < 1e-9 ? Math.Min(a.X, b.X) : x;
                var maximum = Math.Abs(b.Y - a.Y) < 1e-9 ? Math.Max(a.X, b.X) : x;
                left = Math.Min(left, minimum);
                right = Math.Max(right, maximum);
            }
        }
        if (double.IsInfinity(left)) return null;
        return new WrapSegment(left - spread.Left.ToPoints(), right + spread.Right.ToPoints());
    }

    private IEnumerable<WrapSegment> Subtract(WrapSegment segment, WrapSegment cut)
    {
        if (cut.Right <= segment.Left || cut.Left >= segment.Right)
        {
            yield return segment;
            yield break;
        }
        if (cut.Left > segment.Left) yield return new WrapSegment(segment.Left, cut.Left);
        if (cut.Right < segment.Right) yield return new WrapSegment(cut.Right, segment.Right);
    }
}

public sealed record BalanceRequest(IReadOnlyList<FlowItem> Flow, int FlowIndex, int Line, int Offset, int Row, ColumnLayout Columns, double ColumnWidth, double Available)
{
    public double TurnedLimit { get; init; } = double.PositiveInfinity;
}

public interface IColumnBalancer
{
    double Balance(BalanceRequest request);
}

public sealed class ColumnBalancer(ITableComposer tables, PageFlowOptions options) : IColumnBalancer
{
    public double Balance(BalanceRequest request)
    {
        var atoms = Atoms(request);
        if (atoms.Count == 0) return 0;
        var total = atoms.Sum();
        if (total <= 0) return 0;
        var low = Math.Max(atoms.Max(), total / request.Columns.Count);
        var high = total;
        if (low > request.Available || !Fits(atoms, request.Available, request.Columns.Count)) return request.Available;
        high = Math.Min(high, request.Available);
        if (Fits(atoms, low, request.Columns.Count)) return low + 0.01;
        for (var i = 0; i < options.BalanceIterations && high - low > 0.01; i++)
        {
            var middle = (low + high) / 2;
            if (Fits(atoms, middle, request.Columns.Count)) high = middle;
            else low = middle;
        }
        return high + 0.01;
    }

    private bool Fits(IReadOnlyList<double> atoms, double height, int columns)
    {
        var column = 0;
        var used = 0.0;
        foreach (var atom in atoms)
        {
            if (used + atom > height + 0.001)
            {
                if (used == 0) return false;
                column++;
                used = 0;
                if (column >= columns) return false;
            }
            used += atom;
        }
        return true;
    }

    private List<double> Atoms(BalanceRequest request)
    {
        var atoms = new List<double>();
        var depth = 0;
        var previousAfter = 0.0;
        var capacity = request.Available * Math.Max(1, request.Columns.Count);
        var gathered = 0.0;
        for (var index = request.FlowIndex; index < request.Flow.Count && gathered <= capacity; index++)
        {
            var item = request.Flow[index];
            if (item is SectionStartItem) depth++;
            if (item is SectionEndItem)
            {
                if (depth == 0) break;
                depth--;
            }
            if (item is ParagraphEntry entry)
            {
                var first = index == request.FlowIndex;
                var lines = first ? entry.LinesFrom(request.ColumnWidth, request.Offset, request.Line) : entry.Lines(request.ColumnWidth);
                var gap = first && (request.Line > 0 || request.Offset > 0) ? 0 : atoms.Count == 0 ? entry.Flow.SpaceBefore : previousAfter + entry.Flow.SpaceBefore;
                for (var i = 0; i < lines.Count && gathered <= capacity; i++)
                {
                    atoms.Add(lines[i].Height + (i == 0 ? gap : 0));
                    gathered += atoms[^1];
                }
                previousAfter = entry.Flow.SpaceAfter;
            }
            else if (item is TableItem table)
            {
                var placement = tables.Place(table, request.ColumnWidth, request.TurnedLimit);
                var from = index == request.FlowIndex ? request.Row : 0;
                for (var row = from; row < placement.Rows.Count && gathered <= capacity;)
                {
                    var end = Math.Max(row + 1, placement.GroupEnd(row));
                    var height = 0.0;
                    for (var member = row; member < end && member < placement.Rows.Count; member++) height += placement.Rows[member].Height;
                    atoms.Add(height);
                    gathered += height;
                    row = end;
                }
                previousAfter = 0;
            }
        }
        return atoms;
    }
}
