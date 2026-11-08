using PulseOffice.Drawing.Assets.Filters;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class PremultipliedRoundingTests
{
    [Fact]
    public void UnGrigioSemitrasparenteNonSiScurisceConLaScalaDiGrigi()
    {
        var image = new BgraImage([100, 100, 100, 128], 1, 1);

        new GrayscaleFilter().Apply(image);

        Assert.Equal(100, image.Pixels[0]);
        Assert.Equal(100, image.Pixels[2]);
    }
}
