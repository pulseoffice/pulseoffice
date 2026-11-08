namespace PulseOffice.Drawing.Shapes;

public sealed class ObjectTransformSession(IObjectManipulator manipulator, ObjectHandle handle, ObjectBox start, double startRotation)
{
    public ObjectHandle Handle => handle;

    public ObjectBox Start { get; } = start;

    public double StartRotation => startRotation;

    public ObjectBox Preview { get; private set; } = start;

    public double? RotationPreview { get; private set; }

    public double Rotation => RotationPreview ?? startRotation;

    public bool Rotating => handle is ObjectHandle.Rotate;

    public bool Moving => handle is ObjectHandle.Body;

    public void Track(double deltaX, double deltaY, double x, double y, bool constrain)
    {
        if (Rotating) RotationPreview = manipulator.Rotation(Start, x, y, constrain);
        else Preview = manipulator.Resize(Start, handle, deltaX, deltaY, constrain);
    }

    public void Settle(ObjectBox box) => Preview = box;

    public bool Moved(double tolerance) =>
        Math.Abs(Preview.X - Start.X) > tolerance
        || Math.Abs(Preview.Y - Start.Y) > tolerance
        || Math.Abs(Preview.Width - Start.Width) > tolerance
        || Math.Abs(Preview.Height - Start.Height) > tolerance;

    public bool Rotated(double tolerance) => RotationPreview is { } degrees && Math.Abs(degrees - startRotation) > tolerance;
}
