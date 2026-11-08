using System.Text;

namespace PulseOffice.Formats.Interchange.Text.Plain;

public sealed record DetectedText(string Text, Encoding Encoding, bool HadByteOrderMark);

public interface ITextEncodingDetector
{
    DetectedText Decode(byte[] data);
}

public sealed class TextEncodingDetector(ICodePages codePages) : ITextEncodingDetector
{
    private readonly UTF8Encoding _strictUtf8 = new(false, true);

    public DetectedText Decode(byte[] data)
    {
        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xFE && data[2] == 0 && data[3] == 0)
            return new DetectedText(new UTF32Encoding(false, false).GetString(data, 4, data.Length - 4), new UTF32Encoding(false, true), true);
        if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 0xFE && data[3] == 0xFF)
            return new DetectedText(new UTF32Encoding(true, false).GetString(data, 4, data.Length - 4), new UTF32Encoding(true, true), true);
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return new DetectedText(Encoding.UTF8.GetString(data, 3, data.Length - 3), new UTF8Encoding(true), true);
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return new DetectedText(Encoding.Unicode.GetString(data, 2, data.Length - 2), Encoding.Unicode, true);
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return new DetectedText(Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2), Encoding.BigEndianUnicode, true);

        if (LooksLikeUtf16(data, out var bigEndian))
        {
            var encoding = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
            return new DetectedText(encoding.GetString(data), encoding, false);
        }

        try
        {
            return new DetectedText(_strictUtf8.GetString(data), new UTF8Encoding(false), false);
        }
        catch (DecoderFallbackException)
        {
            var windows = codePages.Get(1252);
            return new DetectedText(windows.GetString(data), windows, false);
        }
    }

    private bool LooksLikeUtf16(byte[] data, out bool bigEndian)
    {
        bigEndian = false;
        var sample = Math.Min(data.Length & ~1, 4096);
        if (sample < 4) return false;
        int evenZeros = 0, oddZeros = 0;
        for (var i = 0; i < sample; i += 2)
        {
            if (data[i] == 0) evenZeros++;
            if (data[i + 1] == 0) oddZeros++;
        }
        var pairs = sample / 2;
        if (oddZeros > pairs * 0.4 && evenZeros < pairs * 0.05) return true;
        if (evenZeros > pairs * 0.4 && oddZeros < pairs * 0.05)
        {
            bigEndian = true;
            return true;
        }
        return false;
    }
}
