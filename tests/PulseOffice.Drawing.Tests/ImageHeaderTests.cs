using PulseOffice.Drawing.Assets;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class ImageHeaderTests
{
    [Fact]
    public void LaMisuraDiUnPngSiLeggeDallIntestazione()
    {
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0x4E, 0x20, 0, 0, 0x27, 0x10];

        Assert.Equal((20000, 10000), new ImageHeader().Size(png));
    }

    [Fact]
    public void LaMisuraDiUnGifSiLeggeDallIntestazione()
    {
        byte[] gif = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0x10, 0x00, 0x08, 0x00];

        Assert.Equal((16, 8), new ImageHeader().Size(gif));
    }
}
