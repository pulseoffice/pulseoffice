using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Drawing.Assets.Gif;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class GifCodecTests
{
    private static BgraImage Pattern(int width, int height, int seed)
    {
        var pixels = new byte[width * height * 4];
        var random = new Random(seed);
        for (var index = 0; index < width * height; index++)
        {
            var level = random.Next(6);
            pixels[index * 4] = (byte)(level * 51);
            pixels[index * 4 + 1] = (byte)(random.Next(7) * 255 / 6);
            pixels[index * 4 + 2] = (byte)(random.Next(6) * 51);
            pixels[index * 4 + 3] = 255;
            if (index % 17 == 0) Array.Clear(pixels, index * 4, 4);
        }
        return new BgraImage(pixels, width, height);
    }

    [Fact]
    public void IFotogrammiTornanoUgualiConIRitardiEIGiri()
    {
        var codec = new GifCodec();
        var first = Pattern(120, 90, 1);
        var second = Pattern(120, 90, 2);

        var animation = codec.Decode(codec.Encode([new GifFrame(first, 300), new GifFrame(second, 500)], loops: 0));

        Assert.NotNull(animation);
        Assert.Equal(2, animation.Frames.Count);
        Assert.Equal(0, animation.Loops);
        Assert.Equal(300, animation.Frames[0].DelayMilliseconds);
        Assert.Equal(500, animation.Frames[1].DelayMilliseconds);
        Assert.Equal(first.Pixels, animation.Frames[0].Image.Pixels);
    }

    [Fact]
    public void IlTempoSceglieIlFotogrammaGiusto()
    {
        var image = new BgraImage(new byte[4], 1, 1);
        var animation = new GifAnimation(1, 1, [new GifFrame(image, 100), new GifFrame(image with { }, 200)], 0);

        Assert.Same(animation.Frames[0], animation.At(50));
        Assert.Same(animation.Frames[1], animation.At(150));
        Assert.Same(animation.Frames[0], animation.At(320));
    }
}
