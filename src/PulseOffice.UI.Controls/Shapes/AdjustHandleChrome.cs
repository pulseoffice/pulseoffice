using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record AdjustHandleGrip(int Handle, ShapeObject Start)
{
    public ShapeObject Preview { get; init; } = Start;

    public bool Changed => !ReferenceEquals(Preview, Start);
}

public sealed record AdjustFrame(Rect Box, double Rotation = 0, bool FlipHorizontal = false, bool FlipVertical = false)
{
    public bool Usable => Box.Width > 0 && Box.Height > 0;

    public Matrix Placement(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var centre = Box.Center;
        var horizontal = FlipHorizontal ^ shape.FlipHorizontal;
        var vertical = FlipVertical ^ shape.FlipVertical;
        var placement = Matrix.CreateScale(Box.Width, Box.Height) * Matrix.CreateTranslation(Box.X, Box.Y);
        if (horizontal || vertical)
            placement *= Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateScale(horizontal ? -1 : 1, vertical ? -1 : 1) * Matrix.CreateTranslation(centre.X, centre.Y);
        if (Rotation != 0)
            placement *= Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(Rotation * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y);
        return placement;
    }
}

public interface IAdjustHandleChrome
{
    IReadOnlyList<Point> Points(ShapeObject shape, AdjustFrame frame);

    AdjustHandleGrip? Grip(ShapeObject shape, AdjustFrame frame, Point point, double tolerance);

    AdjustHandleGrip Drag(AdjustHandleGrip grip, AdjustFrame frame, Point point);

    void Draw(DrawingContext context, ShapeObject shape, AdjustFrame frame, double size);

    void DrawPreview(DrawingContext context, AdjustHandleGrip grip, AdjustFrame frame, double size, double thickness);
}

public sealed class AdjustHandleChrome(IShapeAdjustHandles handles, IShapeGeometry geometry) : IAdjustHandleChrome
{
    private readonly IBrush _fill = new ImmutableSolidColorBrush(Color.FromRgb(250, 204, 21));
    private readonly IBrush _edge = new ImmutableSolidColorBrush(Color.FromRgb(161, 98, 7));
    private readonly IBrush _trace = new ImmutableSolidColorBrush(Color.FromArgb(220, 161, 98, 7));
    private readonly OutlineGeometry _outlines = new();

    public AdjustHandleChrome() : this(new ShapeAdjustHandles(), new ShapeGeometry())
    {
    }

    public IReadOnlyList<Point> Points(ShapeObject shape, AdjustFrame frame)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(frame);
        if (!frame.Usable) return [];
        var placement = frame.Placement(shape);
        return [.. handles.Handles(shape, frame.Box.Width, frame.Box.Height)
            .Select(handle => new Point(handle.Position.X, handle.Position.Y).Transform(placement))];
    }

    public AdjustHandleGrip? Grip(ShapeObject shape, AdjustFrame frame, Point point, double tolerance)
    {
        var points = Points(shape, frame);
        for (var index = points.Count - 1; index >= 0; index--)
            if (Math.Abs(points[index].X - point.X) <= tolerance && Math.Abs(points[index].Y - point.Y) <= tolerance)
                return new AdjustHandleGrip(index, shape);
        return null;
    }

    public AdjustHandleGrip Drag(AdjustHandleGrip grip, AdjustFrame frame, Point point)
    {
        ArgumentNullException.ThrowIfNull(grip);
        ArgumentNullException.ThrowIfNull(frame);
        if (!frame.Usable || !frame.Placement(grip.Start).TryInvert(out var inverse)) return grip;
        var local = point.Transform(inverse);
        return grip with { Preview = handles.Drag(grip.Start, grip.Handle, new ShapePoint(local.X, local.Y), frame.Box.Width, frame.Box.Height) };
    }

    public void Draw(DrawingContext context, ShapeObject shape, AdjustFrame frame, double size)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pen = new Pen(_edge, size / 4);
        foreach (var point in Points(shape, frame)) context.DrawGeometry(_fill, pen, Diamond(point, size));
    }

    public void DrawPreview(DrawingContext context, AdjustHandleGrip grip, AdjustFrame frame, double size, double thickness)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(grip);
        ArgumentNullException.ThrowIfNull(frame);
        if (!frame.Usable) return;
        var placement = frame.Placement(grip.Preview);
        var outline = geometry.Outline(grip.Preview, frame.Box.Width, frame.Box.Height);
        var traced = _outlines.Build(outline, point => new Point(point.X, point.Y).Transform(placement), filled: false);
        context.DrawGeometry(null, new Pen(_trace, thickness, new DashStyle([4, 3], 0)), traced);
        Draw(context, grip.Preview, frame, size);
    }

    private StreamGeometry Diamond(Point centre, double size)
    {
        var diamond = new StreamGeometry();
        using (var path = diamond.Open())
        {
            path.BeginFigure(new Point(centre.X, centre.Y - size), true);
            path.LineTo(new Point(centre.X + size, centre.Y));
            path.LineTo(new Point(centre.X, centre.Y + size));
            path.LineTo(new Point(centre.X - size, centre.Y));
            path.EndFigure(true);
        }
        return diamond;
    }
}
