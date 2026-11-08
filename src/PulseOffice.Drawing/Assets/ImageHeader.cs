using System.Buffers.Binary;

namespace PulseOffice.Drawing.Assets;

public sealed class ImageHeader
{
    private const int MaximumTiffEntries = 4096;

    public string? MediaType(ReadOnlySpan<byte> data) => data switch
    {
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] => "image/gif",
        [(byte)'B', (byte)'M', ..] => "image/bmp",
        [(byte)'I', (byte)'I', 0x2A, 0x00, ..] or [(byte)'M', (byte)'M', 0x00, 0x2A, ..] => "image/tiff",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
        _ => null,
    };

    public (int Width, int Height)? Size(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G')
            return (BigEndian(data[16..20]), BigEndian(data[20..24]));

        if (data.Length >= 10 && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')
            return (data[6] | (data[7] << 8), data[8] | (data[9] << 8));

        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8) return Jpeg(data);

        if (data.Length >= 26 && data[0] == (byte)'B' && data[1] == (byte)'M') return Bmp(data);

        if (data.Length >= 8 && ((data[0] == (byte)'I' && data[1] == (byte)'I') || (data[0] == (byte)'M' && data[1] == (byte)'M'))) return Tiff(data, data[0] == (byte)'I');

        if (data.Length >= 30 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return WebP(data);

        return null;
    }

    public const double ScreenDotsPerInch = 96;

    private const double MetresPerInch = 0.0254;
    private const double CentimetresPerInch = 2.54;

    public double DotsPerInch(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == (byte)'P')
        {
            var position = 8;
            while (position + 12 <= data.Length)
            {
                var length = BigEndian(data[position..(position + 4)]);
                if (length < 0 || length > data.Length - position - 12) break;
                var type = data[(position + 4)..(position + 8)];
                if (type.SequenceEqual("IDAT"u8)) break;
                if (type.SequenceEqual("pHYs"u8) && length >= 9 && position + 17 <= data.Length && data[position + 16] == 1)
                    return BigEndian(data[(position + 8)..(position + 12)]) is > 0 and var perMetre ? perMetre * MetresPerInch : ScreenDotsPerInch;
                position += 12 + length;
            }
            return ScreenDotsPerInch;
        }

        if (data.Length >= 18 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF && data[3] == 0xE0 && data[6..11].SequenceEqual("JFIF\0"u8))
        {
            var density = (data[14] << 8) | data[15];
            return data[13] switch
            {
                1 when density > 0 => density,
                2 when density > 0 => density * CentimetresPerInch,
                _ => ScreenDotsPerInch,
            };
        }

        return ScreenDotsPerInch;
    }

    public (int Width, int Height)? NaturalTwips(ReadOnlySpan<byte> data)
    {
        if (Size(data) is not { } pixels) return null;
        var dpi = DotsPerInch(data);
        return ((int)Math.Round(pixels.Width * ((double)PulseOffice.Foundation.Units.Twip.PerInch) / dpi), (int)Math.Round(pixels.Height * ((double)PulseOffice.Foundation.Units.Twip.PerInch) / dpi));
    }

    private (int Width, int Height)? Jpeg(ReadOnlySpan<byte> data)
    {
        var position = 2;
        while (position + 9 < data.Length)
        {
            if (data[position] != 0xFF) return null;
            var marker = data[position + 1];
            var length = (data[position + 2] << 8) | data[position + 3];
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return ((data[position + 7] << 8) | data[position + 8], (data[position + 5] << 8) | data[position + 6]);
            if (length < 2) return null;
            position += 2 + length;
        }

        return null;
    }

    private (int Width, int Height)? Bmp(ReadOnlySpan<byte> data)
    {
        var header = BinaryPrimitives.ReadInt32LittleEndian(data[14..18]);
        if (header == 12) return (BinaryPrimitives.ReadUInt16LittleEndian(data[18..20]), BinaryPrimitives.ReadUInt16LittleEndian(data[20..22]));
        var width = BinaryPrimitives.ReadInt32LittleEndian(data[18..22]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(data[22..26]);
        return (Magnitude(width), Magnitude(height));
    }

    private (int Width, int Height)? Tiff(ReadOnlySpan<byte> data, bool little)
    {
        if (Short(data, 2, little) != 42) return null;
        var directory = Long(data, 4, little);
        if (directory < 8 || directory > data.Length - 2) return null;
        var entries = Math.Min(Short(data, (int)directory, little), MaximumTiffEntries);
        int? width = null, height = null;
        for (var index = 0; index < entries; index++)
        {
            var at = (int)directory + 2 + index * 12;
            if (at + 12 > data.Length) break;
            var tag = Short(data, at, little);
            var type = Short(data, at + 2, little);
            var value = type == 3 ? Short(data, at + 8, little) : (int)Math.Min(int.MaxValue, Long(data, at + 8, little));
            if (tag == 256) width = value;
            else if (tag == 257) height = value;
            if (width is not null && height is not null) return (width.Value, height.Value);
        }

        return null;
    }

    private (int Width, int Height)? WebP(ReadOnlySpan<byte> data)
    {
        var chunk = data[12..16];
        if (chunk.SequenceEqual("VP8X"u8))
            return (1 + (data[24] | (data[25] << 8) | (data[26] << 16)), 1 + (data[27] | (data[28] << 8) | (data[29] << 16)));
        if (chunk.SequenceEqual("VP8L"u8))
        {
            if (data[20] != 0x2F) return null;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..25]);
            return ((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }
        if (chunk.SequenceEqual("VP8 "u8))
        {
            if (data[23] != 0x9D || data[24] != 0x01 || data[25] != 0x2A) return null;
            return (BinaryPrimitives.ReadUInt16LittleEndian(data[26..28]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[28..30]) & 0x3FFF);
        }

        return null;
    }

    private int Magnitude(int value) => value == int.MinValue ? int.MaxValue : Math.Abs(value);

    private int Short(ReadOnlySpan<byte> data, int at, bool little) =>
        at + 2 > data.Length ? 0 : little ? BinaryPrimitives.ReadUInt16LittleEndian(data[at..]) : BinaryPrimitives.ReadUInt16BigEndian(data[at..]);

    private long Long(ReadOnlySpan<byte> data, int at, bool little) =>
        at + 4 > data.Length ? 0 : little ? BinaryPrimitives.ReadUInt32LittleEndian(data[at..]) : BinaryPrimitives.ReadUInt32BigEndian(data[at..]);

    private int BigEndian(ReadOnlySpan<byte> bytes) =>
        (int)Math.Min(int.MaxValue, ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3]);
}
