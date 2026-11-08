using PulseOffice.Drawing.Assets;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ImageResolutionTests
{
    private readonly ImageHeader _header = new();

    private byte[] Png(int? pixelsPerMetre)
    {
        var bytes = new List<byte> { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };
        void Chunk(string type, byte[] body)
        {
            bytes.AddRange([(byte)(body.Length >> 24), (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length]);
            bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(type));
            bytes.AddRange(body);
            bytes.AddRange([0, 0, 0, 0]);
        }
        Chunk("IHDR", [0, 0, 0, 19, 0, 0, 0, 19, 8, 2, 0, 0, 0]);
        if (pixelsPerMetre is { } ppm)
        {
            var p = new[] { (byte)(ppm >> 24), (byte)(ppm >> 16), (byte)(ppm >> 8), (byte)ppm };
            Chunk("pHYs", [.. p, .. p, 1]);
        }
        Chunk("IDAT", []);
        return [.. bytes];
    }

    [Fact]
    public void La_risoluzione_dichiarata_nel_PNG_dà_la_misura_naturale()
    {
        Assert.Equal(72, _header.DotsPerInch(Png(2835)), 0);
        Assert.Equal((380, 380), _header.NaturalTwips(Png(2835)));
        Assert.Equal(ImageHeader.ScreenDotsPerInch, _header.DotsPerInch(Png(null)));
        Assert.Equal((285, 285), _header.NaturalTwips(Png(null)));
    }
}
