using PulseOffice.Drawing.Assets;
using Xunit;

namespace PulseOffice.Robustness.Tests.Images;

public sealed class RasterHeaderTests
{
    private readonly ImageHeader _header = new();

    private byte[] Bmp(int width, int height)
    {
        var data = new byte[54];
        data[0] = (byte)'B';
        data[1] = (byte)'M';
        data[14] = 40;
        BitConverter.TryWriteBytes(data.AsSpan(18), width);
        BitConverter.TryWriteBytes(data.AsSpan(22), height);
        return data;
    }

    private byte[] Tiff(bool littleEndian, int width, int height)
    {
        var data = new byte[8 + 2 + 2 * 12 + 4];
        void Short(int at, int value) { if (littleEndian) { data[at] = (byte)value; data[at + 1] = (byte)(value >> 8); } else { data[at] = (byte)(value >> 8); data[at + 1] = (byte)value; } }
        void Long(int at, int value) { if (littleEndian) BitConverter.TryWriteBytes(data.AsSpan(at), value); else { data[at] = (byte)(value >> 24); data[at + 1] = (byte)(value >> 16); data[at + 2] = (byte)(value >> 8); data[at + 3] = (byte)value; } }
        data[0] = data[1] = (byte)(littleEndian ? 'I' : 'M');
        Short(2, 42);
        Long(4, 8);
        Short(8, 2);
        Short(10, 256); Short(12, 4); Long(14, 1); Long(18, width);
        Short(22, 257); Short(24, 4); Long(26, 1); Long(30, height);
        return data;
    }

    private byte[] WebP(string chunk, int width, int height)
    {
        var data = new byte[30];
        "RIFF"u8.CopyTo(data);
        "WEBP"u8.CopyTo(data.AsSpan(8));
        System.Text.Encoding.ASCII.GetBytes(chunk).CopyTo(data, 12);
        switch (chunk)
        {
            case "VP8X":
                data[24] = (byte)(width - 1); data[25] = (byte)((width - 1) >> 8); data[26] = (byte)((width - 1) >> 16);
                data[27] = (byte)(height - 1); data[28] = (byte)((height - 1) >> 8); data[29] = (byte)((height - 1) >> 16);
                break;
            case "VP8L":
                data[20] = 0x2F;
                var bits = (uint)(width - 1) | ((uint)(height - 1) << 14);
                BitConverter.TryWriteBytes(data.AsSpan(21), bits);
                break;
            case "VP8 ":
                data[23] = 0x9D; data[24] = 0x01; data[25] = 0x2A;
                data[26] = (byte)width; data[27] = (byte)((width >> 8) & 0x3F);
                data[28] = (byte)height; data[29] = (byte)((height >> 8) & 0x3F);
                break;
        }
        return data;
    }

    [Fact]
    public void BitmapDimensionsAreKnownBeforeDecoding()
    {
        Assert.Equal((60000, 60000), _header.Size(Bmp(60000, 60000)));
        Assert.Equal((70000, 50000), _header.Size(Bmp(70000, -50000)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TiffDimensionsAreKnownBeforeDecoding(bool littleEndian) =>
        Assert.Equal((90000, 80000), _header.Size(Tiff(littleEndian, 90000, 80000)));

    [Theory]
    [InlineData("VP8X", 16000, 16000)]
    [InlineData("VP8L", 16000, 16000)]
    [InlineData("VP8 ", 16000, 16000)]
    public void WebPDimensionsAreKnownBeforeDecoding(string chunk, int width, int height) =>
        Assert.Equal((width, height), _header.Size(WebP(chunk, width, height)));

    [Fact]
    public void TruncatedOrHostileHeadersNeverThrow()
    {
        var seeds = new[] { Bmp(10, 10), Tiff(true, 10, 10), Tiff(false, 10, 10), WebP("VP8X", 10, 10), WebP("VP8L", 10, 10), WebP("VP8 ", 10, 10),
            [0x89, (byte)'P', (byte)'N', (byte)'G', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], [0xFF, 0xD8, 0xFF, 0xC0, 0, 1, 0, 0xFF, 0xFF, 0xFF, 0xFF] };
        var random = new Random(77);
        foreach (var seed in seeds)
        {
            for (var cut = 0; cut <= seed.Length; cut++) _header.Size(seed.AsSpan(0, cut));
            for (var round = 0; round < 500; round++)
            {
                var mutated = (byte[])seed.Clone();
                mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
                if (_header.Size(mutated) is { } size)
                {
                    Assert.True(size.Width >= 0);
                    Assert.True(size.Height >= 0);
                }
            }
        }
        var tiffLoop = Tiff(true, 10, 10);
        tiffLoop[4] = 200;
        _header.Size(tiffLoop);
    }
}
