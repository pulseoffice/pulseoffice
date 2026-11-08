namespace PulseOffice.Formats.Pdf;

public sealed record JpegFacts(int Width, int Height, int Components, int Precision, bool Adobe, byte[]? IccProfile);

public interface IJpegInspector
{
    JpegFacts? Inspect(ReadOnlySpan<byte> data);
}

public sealed class JpegInspector : IJpegInspector
{
    private const int MaximumProfileBytes = 4 * 1024 * 1024;
    private ReadOnlySpan<byte> IccSignature => "ICC_PROFILE\0"u8;
    private ReadOnlySpan<byte> AdobeSignature => "Adobe"u8;

    public JpegFacts? Inspect(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return null;

        var position = 2;
        var adobe = false;
        var chunks = new SortedDictionary<int, byte[]>();
        var profileBytes = 0;
        (int Width, int Height, int Components, int Precision)? frame = null;

        while (position + 4 <= data.Length)
        {
            if (data[position] != 0xFF) return null;
            var marker = data[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                position += 2;
                continue;
            }
            if (marker is 0xD9 or 0xDA) break;

            var length = (data[position + 2] << 8) | data[position + 3];
            if (length < 2 || position + 2 + length > data.Length) return null;
            var segment = data.Slice(position + 4, length - 2);

            if (marker is 0xC0 or 0xC1 or 0xC2 && segment.Length >= 6)
                frame = ((segment[3] << 8) | segment[4], (segment[1] << 8) | segment[2], segment[5], segment[0]);
            else if (marker is 0xC3 or (>= 0xC5 and <= 0xC7) or (>= 0xC9 and <= 0xCB) or (>= 0xCD and <= 0xCF))
                return null;
            else if (marker == 0xEE && segment.StartsWith(AdobeSignature))
                adobe = true;
            else if (marker == 0xE2 && segment.Length > IccSignature.Length + 2 && segment.StartsWith(IccSignature))
            {
                var sequence = segment[IccSignature.Length];
                var payload = segment[(IccSignature.Length + 2)..];
                profileBytes += payload.Length;
                if (profileBytes > MaximumProfileBytes) return null;
                chunks[sequence] = payload.ToArray();
            }

            position += 2 + length;
        }

        if (frame is not { } found || found.Width <= 0 || found.Height <= 0 || found.Precision != 8 || found.Components is not (1 or 3 or 4)) return null;

        byte[]? profile = null;
        if (chunks.Count > 0)
        {
            profile = new byte[profileBytes];
            var offset = 0;
            foreach (var chunk in chunks.Values)
            {
                chunk.CopyTo(profile, offset);
                offset += chunk.Length;
            }
        }

        return new JpegFacts(found.Width, found.Height, found.Components, found.Precision, adobe, profile);
    }
}
