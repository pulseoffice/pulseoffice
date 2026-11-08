namespace PulseOffice.Drawing.Shapes;

public enum ObjectHandle
{
    None,
    Body,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    Rotate,
}

public readonly record struct ObjectBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CentreX => X + Width / 2;
    public double CentreY => Y + Height / 2;

    public ObjectBox Normalized() => new(
        Math.Min(X, X + Width),
        Math.Min(Y, Y + Height),
        Math.Abs(Width),
        Math.Abs(Height));
}

public interface IObjectManipulator
{
    double HandleSize { get; }

    double RotationDistance { get; }

    IReadOnlyList<(ObjectHandle Handle, ObjectBox Area)> Handles(ObjectBox box, double unit = 1);

    ObjectHandle HandleAt(ObjectBox box, double rotationDegrees, double x, double y, double unit = 1);

    ObjectBox Resize(ObjectBox box, ObjectHandle handle, double deltaX, double deltaY, bool keepAspect);

    double Rotation(ObjectBox box, double x, double y, bool snap);

    ObjectTransformSession Begin(ObjectHandle handle, ObjectBox start, double startRotation);
}

public sealed class ObjectManipulator : IObjectManipulator
{
    private const double MinimumSide = 8;
    private const double SnapDegrees = 15;

    public double HandleSize { get; init; } = 7;

    public double RotationDistance { get; init; } = 22;

    public IReadOnlyList<(ObjectHandle Handle, ObjectBox Area)> Handles(ObjectBox box, double unit = 1)
    {
        var side = HandleSize * unit;
        var half = side / 2;

        ObjectBox At(double x, double y) => new(x - half, y - half, side, side);

        return
        [
            (ObjectHandle.TopLeft, At(box.X, box.Y)),
            (ObjectHandle.Top, At(box.CentreX, box.Y)),
            (ObjectHandle.TopRight, At(box.Right, box.Y)),
            (ObjectHandle.Right, At(box.Right, box.CentreY)),
            (ObjectHandle.BottomRight, At(box.Right, box.Bottom)),
            (ObjectHandle.Bottom, At(box.CentreX, box.Bottom)),
            (ObjectHandle.BottomLeft, At(box.X, box.Bottom)),
            (ObjectHandle.Left, At(box.X, box.CentreY)),
            (ObjectHandle.Rotate, At(box.CentreX, box.Y - RotationDistance * unit)),
        ];
    }

    public ObjectTransformSession Begin(ObjectHandle handle, ObjectBox start, double startRotation) =>
        new(this, handle, start, startRotation);

    public ObjectHandle HandleAt(ObjectBox box, double rotationDegrees, double x, double y, double unit = 1)
    {
        var (localX, localY) = ToLocal(box, rotationDegrees, x, y);

        foreach (var (handle, area) in Handles(box, unit))
            if (localX >= area.X && localX <= area.Right && localY >= area.Y && localY <= area.Bottom)
                return handle;

        return localX >= box.X && localX <= box.Right && localY >= box.Y && localY <= box.Bottom
            ? ObjectHandle.Body
            : ObjectHandle.None;
    }

    public ObjectBox Resize(ObjectBox box, ObjectHandle handle, double deltaX, double deltaY, bool keepAspect)
    {
        var left = box.X;
        var top = box.Y;
        var right = box.Right;
        var bottom = box.Bottom;

        switch (handle)
        {
            case ObjectHandle.Body:
                return box with { X = left + deltaX, Y = top + deltaY };

            case ObjectHandle.TopLeft: left += deltaX; top += deltaY; break;
            case ObjectHandle.Top: top += deltaY; break;
            case ObjectHandle.TopRight: right += deltaX; top += deltaY; break;
            case ObjectHandle.Right: right += deltaX; break;
            case ObjectHandle.BottomRight: right += deltaX; bottom += deltaY; break;
            case ObjectHandle.Bottom: bottom += deltaY; break;
            case ObjectHandle.BottomLeft: left += deltaX; bottom += deltaY; break;
            case ObjectHandle.Left: left += deltaX; break;

            default: return box;
        }

        var width = Math.Max(MinimumSide, right - left);
        var height = Math.Max(MinimumSide, bottom - top);

        if (keepAspect && box.Width > 0 && box.Height > 0 && IsCorner(handle))
        {
            var ratio = box.Width / box.Height;

            if (width / height > ratio) width = height * ratio;
            else height = width / ratio;
        }

        var x = handle is ObjectHandle.TopLeft or ObjectHandle.Left or ObjectHandle.BottomLeft
            ? right - width
            : left;

        var y = handle is ObjectHandle.TopLeft or ObjectHandle.Top or ObjectHandle.TopRight
            ? bottom - height
            : top;

        return new ObjectBox(x, y, width, height);
    }

    public double Rotation(ObjectBox box, double x, double y, bool snap)
    {
        var degrees = Math.Atan2(y - box.CentreY, x - box.CentreX) * 180 / Math.PI + 90;

        degrees = ((degrees % 360) + 360) % 360;

        return snap ? Math.Round(degrees / SnapDegrees) * SnapDegrees % 360 : degrees;
    }

    private static bool IsCorner(ObjectHandle handle) =>
        handle is ObjectHandle.TopLeft or ObjectHandle.TopRight
            or ObjectHandle.BottomLeft or ObjectHandle.BottomRight;

    private static (double X, double Y) ToLocal(ObjectBox box, double rotationDegrees, double x, double y)
    {
        if (Math.Abs(rotationDegrees) < 0.001) return (x, y);

        var radians = -rotationDegrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        var dx = x - box.CentreX;
        var dy = y - box.CentreY;

        return (box.CentreX + dx * cos - dy * sin, box.CentreY + dx * sin + dy * cos);
    }
}
