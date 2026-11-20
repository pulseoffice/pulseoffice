using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Objects;

public sealed class Round287PictureCompressionTests
{
    private readonly PictureCompression _compression = new(null!, null!, null!, null!);

    private static CompressiblePicture Picture(int width, int height, double inches, (double, double, double, double) crop = default) =>
        new("p", new RasterImageObject { Id = "p", Data = new byte[] { 1 }, MediaType = "image/png", PixelWidth = width, PixelHeight = height, Crop = crop },
            new DocumentSize(Twip.FromInches(inches), Twip.FromInches(inches * height / width)));

    [Fact]
    public void The_resolution_is_measured_on_the_displayed_size()
    {
        Assert.Equal(440, _compression.MaximumEdge(Picture(4000, 2000, 2), new CompressionChoice(220, 80, false, false)));
    }

    [Fact]
    public void A_picture_already_below_the_resolution_is_not_enlarged_and_keep_resolution_keeps_it()
    {
        Assert.Equal(400, _compression.MaximumEdge(Picture(400, 200, 4), new CompressionChoice(220, 80, false, false)));
        Assert.Equal(4000, _compression.MaximumEdge(Picture(4000, 2000, 2), new CompressionChoice(null, 80, false, false)));
    }

    [Fact]
    public void A_kept_crop_needs_the_hidden_part_too_and_a_discarded_one_does_not()
    {
        var cropped = Picture(4000, 2000, 2, (0.25, 0, 0.25, 0));

        Assert.Equal(880, _compression.MaximumEdge(cropped, new CompressionChoice(220, 80, false, false)));
        Assert.Equal(440, _compression.MaximumEdge(cropped, new CompressionChoice(220, 80, true, false)));
    }

    [Fact]
    public void Word_resolutions_are_offered()
    {
        Assert.Equal(new int?[] { 330, 220, 150, 96, null }, _compression.Resolutions);
    }
}
