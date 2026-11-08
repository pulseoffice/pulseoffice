using PulseOffice.Drawing.Assets.Filters;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ColorReplacementTests
{
    private static BgraImage Pixels(params (byte B, byte G, byte R, byte A)[] pixels) =>
        new([.. pixels.SelectMany(p => new[] { p.B, p.G, p.R, p.A })], pixels.Length, 1);

    [Fact]
    public void UnColoreDentroLaTolleranzaVieneSostituito()
    {
        var image = Pixels((0, 0, 250, 255), (0, 0, 200, 255));
        var filter = new ColorReplacementFilter([new ColorReplacementRule(0xFF0000, 0.05, 0x00FF00)]);

        filter.Apply(image);

        Assert.Equal(1, filter.Replaced);
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, image.Pixels[..4]);
        Assert.Equal(200, image.Pixels[6]);
    }

    [Fact]
    public void LaPrimaRegolaCheCorrispondeVince()
    {
        var image = Pixels((0, 0, 255, 255));
        new ColorReplacementFilter(
        [
            new ColorReplacementRule(0xFF0000, 0, 0x0000FF),
            new ColorReplacementRule(0xFF0000, 0, 0x00FF00),
        ]).Apply(image);

        Assert.Equal(255, image.Pixels[0]);
        Assert.Equal(0, image.Pixels[1]);
    }

    [Fact]
    public void SenzaColoreNuovoIlPixelDiventaTrasparente()
    {
        var image = Pixels((255, 255, 255, 255));
        new ColorReplacementFilter([new ColorReplacementRule(0xFFFFFF, 0, null)]).Apply(image);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, image.Pixels);
    }

    [Fact]
    public void UnPixelGiaTrasparenteNonSiTocca()
    {
        var image = Pixels((0, 0, 0, 0));
        var filter = new ColorReplacementFilter([new ColorReplacementRule(0x000000, 0, 0xFF0000)]);
        filter.Apply(image);
        Assert.Equal(0, filter.Replaced);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, image.Pixels);
    }

    [Fact]
    public void IlColoreNuovoRispettaLaTrasparenzaParziale()
    {
        var image = Pixels((0, 0, 128, 128));
        new ColorReplacementFilter([new ColorReplacementRule(0xFF0000, 0, 0x0000FF)]).Apply(image);
        Assert.Equal(new byte[] { 128, 0, 0, 128 }, image.Pixels);
    }
}
