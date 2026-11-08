using PulseOffice.Drawing.Assets;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ImageFiltersTests
{
    private static byte[] Pixel(byte b, byte g, byte r, byte a = 255) => [b, g, r, a];

    [Fact]
    public void InvertFlipsChannelsNotAlpha()
    {
        var p = Pixel(10, 20, 30, 255);
        new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog().For(ImageFilter.Invert).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(p, 1, 1));
        Assert.Equal(245, p[0]);
        Assert.Equal(235, p[1]);
        Assert.Equal(225, p[2]);
        Assert.Equal(255, p[3]);
    }

    [Fact]
    public void InvertireNonIlluminaLeZoneTrasparenti()
    {
        var transparent = Pixel(0, 0, 0, 0);
        var half = Pixel(10, 20, 30, 200);
        var catalog = new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog();
        catalog.For(ImageFilter.Invert).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(transparent, 1, 1));
        catalog.For(ImageFilter.Invert).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(half, 1, 1));

        Assert.Equal([0, 0, 0, 0], transparent);
        Assert.All(half[..3], channel => Assert.True(channel <= 200));
    }

    [Fact]
    public void GrayscaleEqualisesChannels()
    {
        var p = Pixel(10, 128, 240);
        new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog().For(ImageFilter.Grayscale).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(p, 1, 1));
        Assert.Equal(p[0], p[1]);
        Assert.Equal(p[1], p[2]);
    }

    [Fact]
    public void SepiaWarmsAWhitePixel()
    {
        var p = Pixel(255, 255, 255);
        new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog().For(ImageFilter.Sepia).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(p, 1, 1));
        Assert.True(p[2] >= p[1] && p[1] >= p[0]);
    }

    [Fact]
    public void MosaicAveragesABlock()
    {
        var pixels = new byte[2 * 2 * 4];
        var colors = new[] { (byte)0, (byte)0, (byte)90, (byte)90 };
        for (var i = 0; i < 4; i++)
        {
            pixels[i * 4] = colors[i];
            pixels[i * 4 + 1] = colors[i];
            pixels[i * 4 + 2] = colors[i];
            pixels[i * 4 + 3] = 255;
        }

        new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog().For(ImageFilter.Mosaic).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(pixels, 2, 2));

        for (var i = 0; i < 4; i++)
            Assert.Equal(45, pixels[i * 4]);
    }

    [Fact]
    public void NoneLeavesPixelsUntouched()
    {
        var p = Pixel(10, 20, 30);
        new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog().For(ImageFilter.None).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(p, 1, 1));
        Assert.Equal([10, 20, 30, 255], p);
    }

    private static PulseOffice.Drawing.Assets.Filters.BgraImage Square(int size, Func<int, int, byte> value)
    {
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var o = (y * size + x) * 4;
                pixels[o] = pixels[o + 1] = pixels[o + 2] = value(x, y);
                pixels[o + 3] = 255;
            }
        return new PulseOffice.Drawing.Assets.Filters.BgraImage(pixels, size, size);
    }

    private static void Run(ImageFilter filter, PulseOffice.Drawing.Assets.Filters.BgraImage image) =>
        new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog().For(filter).Apply(image);

    private static byte Centre(PulseOffice.Drawing.Assets.Filters.BgraImage image) =>
        image.Pixels[(image.Height / 2 * image.Width + image.Width / 2) * 4];

    [Fact]
    public void OgniFiltroHaUnaStrategiaPropria() =>
        Assert.All(Enum.GetValues<ImageFilter>().Where(f => f != ImageFilter.None), filter =>
            Assert.IsNotType<PulseOffice.Drawing.Assets.Filters.IdentityFilter>(
                new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog().For(filter)));

    [Fact]
    public void AmmorbidireUnPuntoIsolatoLoAbbassa()
    {
        var image = Square(5, (x, y) => x == 2 && y == 2 ? (byte)255 : (byte)0);
        Run(ImageFilter.Smooth, image);
        Assert.InRange(Centre(image), 50, 70);
    }

    [Fact]
    public void RimuovereIlRumoreCancellaUnPuntoIsolato()
    {
        var image = Square(5, (x, y) => x == 2 && y == 2 ? (byte)255 : (byte)40);
        Run(ImageFilter.RemoveNoise, image);
        Assert.Equal(40, Centre(image));
    }

    [Fact]
    public void LaNitidezzaAccentuaUnPuntoPiuChiaro()
    {
        var image = Square(5, (x, y) => x == 2 && y == 2 ? (byte)140 : (byte)100);
        Run(ImageFilter.Sharpen, image);
        Assert.True(Centre(image) > 140);
    }

    [Fact]
    public void IlCarboncinoLasciaBiancaUnAreaUniformeEScurisceIBordi()
    {
        var flat = Square(5, (_, _) => 90);
        Run(ImageFilter.Charcoal, flat);
        Assert.Equal(255, Centre(flat));

        var edge = Square(6, (x, _) => x < 3 ? (byte)0 : (byte)255);
        Run(ImageFilter.Charcoal, edge);
        Assert.True(edge.Pixels[(2 * 6 + 3) * 4] < 50);
    }

    [Fact]
    public void IlRilievoPortaUnAreaUniformeAlGrigioMedio()
    {
        var image = Square(5, (_, _) => 200);
        Run(ImageFilter.Relief, image);
        Assert.Equal(128, Centre(image));
    }

    [Theory]
    [InlineData(ImageFilter.Posterize, 100, 85)]
    [InlineData(ImageFilter.Solarize, 200, 55)]
    [InlineData(ImageFilter.Solarize, 60, 60)]
    public void IFiltriPerPuntoSonoPrevedibili(ImageFilter filter, byte input, byte expected)
    {
        var p = Pixel(input, input, input);
        Run(filter, new PulseOffice.Drawing.Assets.Filters.BgraImage(p, 1, 1));
        Assert.Equal(expected, p[0]);
    }

    [Fact]
    public void IlDuotonoPortaIlNeroAlColoreScuroEIlBiancoAlChiaro()
    {
        var black = Pixel(0, 0, 0);
        var white = Pixel(255, 255, 255);
        var catalog = new PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog();
        var duotone = new PulseOffice.Drawing.Assets.ImageDuotone("#102030", "#F0E0D0");
        catalog.Duotone(duotone).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(black, 1, 1));
        catalog.Duotone(duotone).Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(white, 1, 1));

        Assert.Equal([0x30, 0x20, 0x10, 255], black);
        Assert.Equal([0xD0, 0xE0, 0xF0, 255], white);
    }
}
