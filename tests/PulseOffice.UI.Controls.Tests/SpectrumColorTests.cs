using PulseOffice.UI.Controls.Spectrum;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SpectrumColorTests
{
    [Theory]
    [InlineData(255, 0, 0, 0, 1, 1)]
    [InlineData(0, 255, 0, 120, 1, 1)]
    [InlineData(0, 0, 255, 240, 1, 1)]
    [InlineData(255, 255, 255, 0, 0, 1)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void SiConverteDaRgb(byte r, byte g, byte b, double hue, double saturation, double value)
    {
        var hsv = SpectrumColor.From(new RgbColor(r, g, b));

        Assert.Equal(hue, hsv.Hue, 3);
        Assert.Equal(saturation, hsv.Saturation, 3);
        Assert.Equal(value, hsv.Value, 3);
    }

    [Fact]
    public void OgniColoreTornaUgualeDopoIlGiro()
    {
        for (var r = 0; r < 256; r += 17)
        for (var g = 0; g < 256; g += 17)
        for (var b = 0; b < 256; b += 17)
        {
            var colour = new RgbColor((byte)r, (byte)g, (byte)b);
            Assert.Equal(colour, SpectrumColor.From(colour).ToRgb());
        }
    }

    [Fact]
    public void LaTinta360EUgualeAZero() =>
        Assert.Equal(new RgbColor(255, 0, 0), new SpectrumColor(360, 1, 1).ToRgb());

    [Fact]
    public void IValoriFuoriIntervalloSiLimitano() =>
        Assert.Equal(new RgbColor(255, 255, 255), new SpectrumColor(0, 0, 1).WithShade(-1, 2).ToRgb());
}
