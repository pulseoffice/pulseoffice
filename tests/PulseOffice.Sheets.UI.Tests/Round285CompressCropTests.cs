using PulseOffice.Testing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Assets.Gif;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round285CompressCropTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    [Fact]
    public Task Compressing_with_discard_drops_the_cropped_pixels_and_clears_the_crop() => Session.Dispatch(() =>
    {
        var pixels = new BitmapPixels();
        var image = new BgraImage(new byte[40 * 20 * 4], 40, 20);
        for (var i = 3; i < image.Pixels.Length; i += 4) image.Pixels[i] = 255;
        using var bitmap = pixels.Create(image);
        var raster = new RasterImageObject
        {
            Id = "c", Data = pixels.EncodePng(bitmap), MediaType = "image/png", PixelWidth = 40, PixelHeight = 20, Crop = (0.25, 0, 0.25, 0.5),
        };
        var files = new RasterImageFiles(null!, null!, null!, pixels, null!, new RasterMediaTypes(), new GifCodec(), new ChosenFileReader());

        var result = files.Compress(raster, 3840, 80, discardCrop: true);

        Assert.True(result.Changed);
        Assert.Equal(default, result.Image.Crop);
        Assert.Equal(20, result.Image.PixelWidth);
        Assert.Equal(10, result.Image.PixelHeight);
        Assert.Equal((20, 10), pixels.Dimensions(result.Image.Data.ToArray()));
    }, CancellationToken.None);
}
