using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round287AspectLockDragTests
{
    private readonly ObjectManipulator _manipulator = new();

    [Fact]
    public void A_picture_with_the_lock_off_resizes_freely()
    {
        var picture = new RasterImageObject { Id = "p", Data = new byte[] { 1 }, MediaType = "image/png" }.WithAspectLock(false);

        Assert.False(_manipulator.KeepsAspect(picture));
    }

    [Fact]
    public void A_shape_with_the_lock_on_keeps_its_proportions()
    {
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle }.WithAspectLock(true);
        var session = _manipulator.Begin(ObjectHandle.BottomRight, new ObjectBox(0, 0, 200, 100), 0, _manipulator.KeepsAspect(shape));

        session.Track(100, 0, 300, 100, constrain: false);

        Assert.Equal(2, session.Preview.Width / session.Preview.Height, 6);
    }
}
