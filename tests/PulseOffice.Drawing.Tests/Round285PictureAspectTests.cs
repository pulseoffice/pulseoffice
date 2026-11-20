using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round285PictureAspectTests
{
    private readonly ObjectManipulator _manipulator = new();

    private static readonly ObjectBox Start = new(0, 0, 200, 100);

    [Fact]
    public void Pictures_keep_their_proportions_on_a_corner_drag_and_shift_frees_them()
    {
        var picture = new RasterImageObject { Id = "p", Data = new byte[] { 1 }, MediaType = "image/png", PixelWidth = 2, PixelHeight = 1 };
        var session = _manipulator.Begin(ObjectHandle.BottomRight, Start, 0, _manipulator.KeepsAspect(picture));

        session.Track(100, 0, 300, 100, constrain: false);
        Assert.Equal(2, session.Preview.Width / session.Preview.Height, 6);

        session.Track(100, 0, 300, 100, constrain: true);
        Assert.Equal(300, session.Preview.Width, 6);
        Assert.Equal(100, session.Preview.Height, 6);
    }

    [Fact]
    public void Shapes_resize_freely_unless_shift_is_held()
    {
        var session = _manipulator.Begin(ObjectHandle.BottomRight, Start, 0, _manipulator.KeepsAspect(null));

        session.Track(100, 0, 300, 100, constrain: false);

        Assert.Equal(300, session.Preview.Width, 6);
        Assert.Equal(100, session.Preview.Height, 6);
    }
}
