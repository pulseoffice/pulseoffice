using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing.Freeform;

public readonly record struct PathHandle(int Figure, int Node, PathHandlePart Part)
{
    public static readonly PathHandle None = new(-1, -1, PathHandlePart.Node);

    public bool Exists => Figure >= 0 && Node >= 0;
}

public enum PathHandlePart
{
    Node,
    Before,
    After,
}

public interface IPathEditing
{
    PathHandle HandleAt(FreeformPath path, ShapePoint point, double tolerance);

    FreeformPath Move(FreeformPath path, PathHandle handle, ShapePoint to);

    FreeformPath InsertNode(FreeformPath path, int figure, int afterNode, ShapePoint point);

    FreeformPath DeleteNode(FreeformPath path, PathHandle handle);

    FreeformPath SetKind(FreeformPath path, PathHandle handle, PathNodeKind kind);

    FreeformPath ToggleClosed(FreeformPath path, int figure);
}

public sealed class PathEditing : IPathEditing
{
    private readonly HandleGeometry _geometry = new();
    private readonly IReadOnlyDictionary<PathNodeKind, NodeKindBehaviour> _kinds;
    private readonly IReadOnlyDictionary<PathHandlePart, HandleSide> _sides =
        new Dictionary<PathHandlePart, HandleSide> { [PathHandlePart.Before] = HandleSide.Before, [PathHandlePart.After] = HandleSide.After };

    public PathEditing() =>
        _kinds = new NodeKindBehaviour[] { new CornerNode(), new SmoothNode(_geometry), new SymmetricNode(_geometry) }
            .ToDictionary(behaviour => behaviour.Kind);

    public PathHandle HandleAt(FreeformPath path, ShapePoint point, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(path);

        var best = PathHandle.None;
        var bestDistance = tolerance;

        for (var f = 0; f < path.Figures.Count; f++)
            for (var n = 0; n < path.Figures[f].Nodes.Count; n++)
            {
                var node = path.Figures[f].Nodes[n];

                Consider(new PathHandle(f, n, PathHandlePart.Node), node.Point);
                if (node.Before is { } before) Consider(new PathHandle(f, n, PathHandlePart.Before), before);
                if (node.After is { } after) Consider(new PathHandle(f, n, PathHandlePart.After), after);
            }

        return best;

        void Consider(PathHandle handle, ShapePoint candidate)
        {
            var distance = Distance(candidate, point);
            if (distance > bestDistance) return;

            best = handle;
            bestDistance = distance;
        }
    }

    public FreeformPath Move(FreeformPath path, PathHandle handle, ShapePoint to)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Replace(path, handle, node => _sides.TryGetValue(handle.Part, out var side)
            ? KindOf(node.Kind).Drag(node, side, to)
            : Translate(node, to));
    }

    public FreeformPath InsertNode(FreeformPath path, int figure, int afterNode, ShapePoint point)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (figure < 0 || figure >= path.Figures.Count) return path;

        var nodes = path.Figures[figure].Nodes.ToList();
        if (nodes.Count == 0) return path;

        var index = Math.Clamp(afterNode + 1, 0, nodes.Count);
        nodes.Insert(index, new PathNode(point));

        return WithFigure(path, figure, path.Figures[figure] with { Nodes = nodes });
    }

    public FreeformPath DeleteNode(FreeformPath path, PathHandle handle)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!Valid(path, handle)) return path;

        var figure = path.Figures[handle.Figure];
        if (figure.Nodes.Count <= 2) return path;

        var nodes = figure.Nodes.ToList();
        nodes.RemoveAt(handle.Node);

        return WithFigure(path, handle.Figure, figure with { Nodes = nodes });
    }

    public FreeformPath SetKind(FreeformPath path, PathHandle handle, PathNodeKind kind)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Replace(path, handle, node => KindOf(kind).Adopt(node));
    }

    public FreeformPath ToggleClosed(FreeformPath path, int figure)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (figure < 0 || figure >= path.Figures.Count) return path;

        return WithFigure(path, figure, path.Figures[figure] with { Closed = !path.Figures[figure].Closed });
    }

    private NodeKindBehaviour KindOf(PathNodeKind kind) => _kinds.TryGetValue(kind, out var behaviour) ? behaviour : _kinds[PathNodeKind.Corner];

    private PathNode Translate(PathNode node, ShapePoint to)
    {
        var dx = to.X - node.Point.X;
        var dy = to.Y - node.Point.Y;

        return node with
        {
            Point = to,
            Before = node.Before is { } before ? new ShapePoint(before.X + dx, before.Y + dy) : null,
            After = node.After is { } after ? new ShapePoint(after.X + dx, after.Y + dy) : null,
        };
    }

    private bool Valid(FreeformPath path, PathHandle handle) =>
        handle.Exists
        && handle.Figure < path.Figures.Count
        && handle.Node < path.Figures[handle.Figure].Nodes.Count;

    private FreeformPath Replace(FreeformPath path, PathHandle handle, Func<PathNode, PathNode> change)
    {
        if (!Valid(path, handle)) return path;

        var figure = path.Figures[handle.Figure];
        var nodes = figure.Nodes.ToList();

        nodes[handle.Node] = change(nodes[handle.Node]);

        return WithFigure(path, handle.Figure, figure with { Nodes = nodes });
    }

    private FreeformPath WithFigure(FreeformPath path, int index, PathFigure figure)
    {
        var figures = path.Figures.ToList();
        figures[index] = figure;

        return path with { Figures = figures };
    }

    private double Distance(ShapePoint a, ShapePoint b) => _geometry.Distance(a, b);
}

internal sealed class HandleGeometry
{
    public ShapePoint Mirror(ShapePoint centre, ShapePoint point) => new((2 * centre.X) - point.X, (2 * centre.Y) - point.Y);

    public ShapePoint? Aligned(ShapePoint centre, ShapePoint moved, ShapePoint? opposite)
    {
        if (opposite is not { } other) return null;

        var length = Distance(centre, other);
        var dx = centre.X - moved.X;
        var dy = centre.Y - moved.Y;
        var span = Math.Sqrt((dx * dx) + (dy * dy));

        return span == 0 ? other : new ShapePoint(centre.X + (dx / span * length), centre.Y + (dy / span * length));
    }

    public double Distance(ShapePoint a, ShapePoint b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}

internal sealed class HandleSide
{
    public static readonly HandleSide Before = new(node => node.Before, (node, point) => node with { Before = point });
    public static readonly HandleSide After = new(node => node.After, (node, point) => node with { After = point });

    private readonly Func<PathNode, ShapePoint?> _get;
    private readonly Func<PathNode, ShapePoint?, PathNode> _set;

    private HandleSide(Func<PathNode, ShapePoint?> get, Func<PathNode, ShapePoint?, PathNode> set)
    {
        _get = get;
        _set = set;
    }

    public HandleSide Opposite => ReferenceEquals(this, Before) ? After : Before;

    public ShapePoint? Get(PathNode node) => _get(node);

    public PathNode Set(PathNode node, ShapePoint? point) => _set(node, point);
}

internal abstract class NodeKindBehaviour
{
    public abstract PathNodeKind Kind { get; }

    public abstract PathNode Drag(PathNode node, HandleSide side, ShapePoint to);

    public abstract PathNode Adopt(PathNode node);
}

internal sealed class CornerNode : NodeKindBehaviour
{
    public override PathNodeKind Kind => PathNodeKind.Corner;

    public override PathNode Drag(PathNode node, HandleSide side, ShapePoint to) => side.Set(node, to);

    public override PathNode Adopt(PathNode node) => new(node.Point) { Kind = Kind };
}

internal abstract class CurvedNode(HandleGeometry geometry) : NodeKindBehaviour
{
    protected HandleGeometry Geometry => geometry;

    public override PathNode Drag(PathNode node, HandleSide side, ShapePoint to) =>
        side.Opposite.Set(side.Set(node, to), Opposite(node, side, to));

    public override PathNode Adopt(PathNode node)
    {
        node = node with { Kind = Kind };
        var (before, after) = (node.Before, node.After);

        if (before is null && after is null)
        {
            before = new ShapePoint(node.Point.X - 0.05, node.Point.Y);
            after = new ShapePoint(node.Point.X + 0.05, node.Point.Y);
        }
        else
        {
            before ??= geometry.Mirror(node.Point, after!.Value);
            after ??= geometry.Mirror(node.Point, before.Value);
        }

        return node with { Before = before, After = after };
    }

    protected abstract ShapePoint? Opposite(PathNode node, HandleSide side, ShapePoint to);
}

internal sealed class SmoothNode(HandleGeometry geometry) : CurvedNode(geometry)
{
    public override PathNodeKind Kind => PathNodeKind.Smooth;

    protected override ShapePoint? Opposite(PathNode node, HandleSide side, ShapePoint to) =>
        Geometry.Aligned(node.Point, to, side.Opposite.Get(node));
}

internal sealed class SymmetricNode(HandleGeometry geometry) : CurvedNode(geometry)
{
    public override PathNodeKind Kind => PathNodeKind.Symmetric;

    protected override ShapePoint? Opposite(PathNode node, HandleSide side, ShapePoint to) => Geometry.Mirror(node.Point, to);
}
