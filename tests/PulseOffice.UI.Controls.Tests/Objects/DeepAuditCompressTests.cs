using PulseOffice.Testing;
using Avalonia.Headless;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Assets.Gif;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Objects;

public sealed class DeepAuditCompressTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static BgraImage Frame(byte shade)
    {
        var pixels = new byte[64 * 64 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = shade;
            pixels[i + 1] = (byte)(i % 251);
            pixels[i + 2] = (byte)(i % 13 * 19);
            pixels[i + 3] = 255;
        }
        return new BgraImage(pixels, 64, 64);
    }

    [Fact]
    public Task Comprimere_una_GIF_animata_non_la_riduce_a_un_fotogramma_fermo() => Session.Dispatch(() =>
    {
        var codec = new GifCodec();
        var gif = codec.Encode([new GifFrame(Frame(10), 100), new GifFrame(Frame(200), 100)], 0);
        var raster = new RasterImageObject { Id = "g", Data = gif, MediaType = "image/gif", PixelWidth = 64, PixelHeight = 64 };
        var files = new RasterImageFiles(null!, null!, null!, new BitmapPixels(), null!, new PulseOffice.Drawing.Assets.RasterMediaTypes(), codec, new ChosenFileReader());

        var result = files.Compress(raster, 8, 50);

        Assert.False(result.Changed);
        Assert.Same(raster, result.Image);
    }, CancellationToken.None);
}
