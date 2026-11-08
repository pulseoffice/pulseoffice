namespace PulseOffice.Formats.Interchange.Text.Rtf;

public sealed class RtfPicture
{
    private readonly MemoryStream _data = new();
    private int? _nibble;
    private int _pixelWidth;
    private int _pixelHeight;
    private int _goalWidth;
    private int _goalHeight;
    private int _scaleX = 100;
    private int _scaleY = 100;

    public string? MediaType { get; private set; }

    public long Length => _data.Length;

    public void Set(string word, int value)
    {
        switch (word)
        {
            case "picw": _pixelWidth = value; break;
            case "pich": _pixelHeight = value; break;
            case "picwgoal": _goalWidth = value; break;
            case "pichgoal": _goalHeight = value; break;
            case "picscalex": _scaleX = Math.Clamp(value, 1, 10000); break;
            case "picscaley": _scaleY = Math.Clamp(value, 1, 10000); break;
            case "pngblip": MediaType = "image/png"; break;
            case "jpegblip": MediaType = "image/jpeg"; break;
            default: MediaType = null; break;
        }
    }

    public void Append(byte[] bytes) => _data.Write(bytes);

    public void AppendHex(ReadOnlySpan<byte> text)
    {
        foreach (var character in text)
        {
            var value = character switch
            {
                >= (byte)'0' and <= (byte)'9' => character - '0',
                >= (byte)'a' and <= (byte)'f' => character - 'a' + 10,
                >= (byte)'A' and <= (byte)'F' => character - 'A' + 10,
                _ => -1,
            };
            if (value < 0) continue;
            if (_nibble is { } high)
            {
                _data.WriteByte((byte)(high * 16 + value));
                _nibble = null;
            }
            else _nibble = value;
        }
    }

    public byte[] Data() => _data.ToArray();

    public int Width(int? pixels) => Twips(_goalWidth, _pixelWidth, pixels, _scaleX);

    public int Height(int? pixels) => Twips(_goalHeight, _pixelHeight, pixels, _scaleY);

    private int Twips(int goal, int declared, int? measured, int scale)
    {
        var basis = goal > 0 ? goal : (long)(measured ?? declared) * 15;
        return (int)Math.Clamp((long)basis * scale / 100, 0, int.MaxValue);
    }
}
