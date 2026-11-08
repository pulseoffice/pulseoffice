using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.UI.Controls.Drawing;

public enum DrawingTool
{
    None,
    Polygon,
    PolygonConstrained,
    Freehand,
    Curve,
    TextBox,
    Frame,
    Connector,
}

public interface IDrawingToolState
{
    DrawingTool Tool { get; }

    bool Filled { get; }

    bool EditingPoints { get; }

    bool HelplinesWhileMoving { get; }

    void Choose(DrawingTool tool, bool filled);

    void Finish();

    void ToggleEditingPoints();

    void ToggleHelplines();

    event EventHandler? Changed;
}

public sealed class DrawingToolState : IDrawingToolState
{
    public DrawingTool Tool { get; private set; }

    public bool Filled { get; private set; }

    public bool EditingPoints { get; private set; }

    public bool HelplinesWhileMoving { get; private set; }

    public event EventHandler? Changed;

    public void Choose(DrawingTool tool, bool filled)
    {
        Tool = tool;
        Filled = filled;
        EditingPoints = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Finish()
    {
        if (Tool == DrawingTool.None) return;
        Tool = DrawingTool.None;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleEditingPoints()
    {
        EditingPoints = !EditingPoints;
        if (EditingPoints) Tool = DrawingTool.None;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleHelplines()
    {
        HelplinesWhileMoving = !HelplinesWhileMoving;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record SketchResult(FreeformPath Path, bool Closed);

public sealed class FreeformSketch(IPathAuthoring authoring, double nearness = 6)
{
    private readonly List<ShapePoint> _points = [];
    private ShapePoint? _cursor;

    public bool Active => _points.Count > 0;

    public IReadOnlyList<ShapePoint> Points => _points;

    public bool Begin(DrawingTool tool, ShapePoint point, int clicks, bool filled, out SketchResult? done)
    {
        done = null;
        if (tool is DrawingTool.None or DrawingTool.TextBox) return false;
        var candidate = tool == DrawingTool.PolygonConstrained && _points.Count > 0 ? authoring.Constrain(_points[^1], point) : point;
        if (tool is DrawingTool.Freehand or DrawingTool.Frame or DrawingTool.Connector)
        {
            _points.Clear();
            _points.Add(candidate);
            return true;
        }
        if (clicks > 1 || _points.Count > 1 && Near(candidate, _points[0]))
        {
            done = Complete(tool, filled, _points.Count > 2 && Near(candidate, _points[0]));
            return true;
        }
        _points.Add(candidate);
        return true;
    }

    public void Move(DrawingTool tool, ShapePoint point, bool pressed)
    {
        if (tool is DrawingTool.Freehand)
        {
            if (pressed && _points.Count > 0) _points.Add(point);
            return;
        }
        if (tool is DrawingTool.Frame or DrawingTool.Connector)
        {
            if (pressed && _points.Count > 0) _cursor = point;
            return;
        }
        _cursor = tool == DrawingTool.PolygonConstrained && _points.Count > 0 ? authoring.Constrain(_points[^1], point) : point;
    }

    public SketchResult? Release(DrawingTool tool, bool filled)
    {
        if (tool is DrawingTool.Freehand && _points.Count > 0) return Complete(tool, filled, false);
        if (tool is DrawingTool.Frame or DrawingTool.Connector && _points.Count > 0 && _cursor is { } end)
        {
            _points.Add(end);
            return Complete(tool, filled, false);
        }
        return null;
    }

    public SketchResult? Complete(DrawingTool tool, bool filled, bool closed)
    {
        var points = _points.ToList();
        Cancel();
        if (points.Count < 2) return null;
        var path = tool switch
        {
            DrawingTool.Freehand => authoring.Freehand(points, 2.0, closed, filled),
            DrawingTool.Curve => authoring.Curve(points, closed, filled),
            _ => authoring.Polygon(points, closed, filled),
        };
        return path.Figures.Count > 0 ? new SketchResult(path, closed) : null;
    }

    public void Cancel()
    {
        _points.Clear();
        _cursor = null;
    }

    public void Paint(DrawingContext context, Func<ShapePoint, Point> map, IBrush accent, double thickness = 1.5)
    {
        if (_points.Count == 0) return;
        var pen = new Pen(accent, thickness) { DashStyle = DashStyle.Dash };
        for (var index = 1; index < _points.Count; index++) context.DrawLine(pen, map(_points[index - 1]), map(_points[index]));
        if (_cursor is { } cursor) context.DrawLine(pen, map(_points[^1]), map(cursor));
        foreach (var point in _points) context.DrawEllipse(Brushes.White, new Pen(accent, thickness), map(point), 3 * thickness / 1.5, 3 * thickness / 1.5);
    }

    private bool Near(ShapePoint a, ShapePoint b) => Math.Abs(a.X - b.X) <= nearness && Math.Abs(a.Y - b.Y) <= nearness;
}
