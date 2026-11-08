using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.UI.Controls.Shapes;

public enum FrameOutline
{
    Solid,
    Dashed,
}

public enum FrameHandleFill
{
    Accent,
    Hollow,
}

public enum FrameRotationMark
{
    Square,
    Circle,
}

public sealed record FrameStyle(
    FrameOutline Outline,
    FrameHandleFill Handles,
    FrameRotationMark Rotation,
    double OutlineThickness,
    double HandleThickness,
    double Inflate,
    double Corner);

public interface IFrameStyles
{
    FrameStyle Filled { get; }

    FrameStyle Dashed { get; }
}

public sealed class FrameStyles : IFrameStyles
{
    public FrameStyle Filled { get; } = new(FrameOutline.Solid, FrameHandleFill.Accent, FrameRotationMark.Square, 1.5, 1, 0, 0);

    public FrameStyle Dashed { get; } = new(FrameOutline.Dashed, FrameHandleFill.Hollow, FrameRotationMark.Circle, 1.5, 1.2, 2, 2);
}

public sealed record FrameScene(ObjectBox Box, double Rotation, IBrush Accent, double Unit = 1, bool Editable = true)
{
    public Matrix Turn => Math.Abs(Rotation) < 0.001
        ? Matrix.Identity
        : Matrix.CreateTranslation(-Box.CentreX, -Box.CentreY)
            * Matrix.CreateRotation(Rotation * Math.PI / 180)
            * Matrix.CreateTranslation(Box.CentreX, Box.CentreY);
}

public interface IObjectFrameChrome
{
    FrameStyle Style { get; }

    IReadOnlyList<(ObjectHandle Handle, Rect Area)> Handles(FrameScene scene);

    ObjectHandle HandleAt(FrameScene scene, Point point);

    void Draw(DrawingContext context, FrameScene scene);

    void DrawSecondary(DrawingContext context, FrameScene scene);
}

public sealed class ObjectFrameChrome(IObjectManipulator manipulator, FrameStyle style) : IObjectFrameChrome
{
    private readonly IBrush _paper = new ImmutableSolidColorBrush(Colors.White);

    public FrameStyle Style => style;

    public IReadOnlyList<(ObjectHandle Handle, Rect Area)> Handles(FrameScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!scene.Editable) return [];
        return [.. manipulator.Handles(scene.Box, scene.Unit).Select(entry => (entry.Handle, ToRect(entry.Area)))];
    }

    public ObjectHandle HandleAt(FrameScene scene, Point point)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return manipulator.HandleAt(scene.Box, scene.Rotation, point.X, point.Y, scene.Unit);
    }

    public void Draw(DrawingContext context, FrameScene scene)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scene);
        using var turn = context.PushTransform(scene.Turn);
        DrawOutline(context, scene, OutlinePen(scene, style.OutlineThickness));
        if (!scene.Editable) return;

        var edge = new Pen(style.Handles == FrameHandleFill.Hollow ? scene.Accent : _paper, style.HandleThickness * scene.Unit);
        var fill = style.Handles == FrameHandleFill.Hollow ? _paper : scene.Accent;
        var handles = Handles(scene);
        var rotate = handles.First(entry => entry.Handle == ObjectHandle.Rotate).Area;

        context.DrawLine(
            new Pen(scene.Accent, style.HandleThickness * scene.Unit),
            new Point(scene.Box.CentreX, scene.Box.Y),
            new Point(rotate.Center.X, rotate.Bottom));

        foreach (var (handle, area) in handles)
        {
            if (handle == ObjectHandle.Rotate && style.Rotation == FrameRotationMark.Circle)
                context.DrawEllipse(fill, edge, area.Center, area.Width / 2, area.Height / 2);
            else
                context.DrawRectangle(fill, edge, area);
        }
    }

    public void DrawSecondary(DrawingContext context, FrameScene scene)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scene);
        using var turn = context.PushTransform(scene.Turn);
        var pen = new Pen(scene.Accent, scene.Unit, new DashStyle([3, 3], 0));
        DrawOutline(context, scene, pen);
    }

    private Pen OutlinePen(FrameScene scene, double thickness) =>
        style.Outline == FrameOutline.Dashed
            ? new Pen(scene.Accent, thickness * scene.Unit, new DashStyle([4, 3], 0))
            : new Pen(scene.Accent, thickness * scene.Unit);

    private void DrawOutline(DrawingContext context, FrameScene scene, Pen pen)
    {
        var outline = ToRect(scene.Box).Inflate(style.Inflate * scene.Unit);
        var corner = style.Corner * scene.Unit;
        context.DrawRectangle(null, pen, outline, corner, corner);
    }

    private Rect ToRect(ObjectBox box) => new(box.X, box.Y, box.Width, box.Height);
}
