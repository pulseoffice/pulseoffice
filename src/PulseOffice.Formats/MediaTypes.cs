using System.Text;

namespace PulseOffice.Formats;

public sealed class MediaTypes
{
    public const string StarMediaType = "application/vnd.sun.star.media";

    private readonly IReadOnlyList<(string MediaType, string Extension)> _types =
    [
        ("video/mp4", "mp4"), ("video/webm", "webm"), ("video/quicktime", "mov"), ("video/x-msvideo", "avi"), ("video/x-matroska", "mkv"),
        ("audio/mpeg", "mp3"), ("audio/wav", "wav"), ("audio/x-wav", "wav"), ("audio/ogg", "ogg"), ("audio/aiff", "aiff"),
        ("audio/x-aiff", "aiff"), ("audio/mp4", "m4a"), ("audio/flac", "flac"),
    ];

    private readonly IReadOnlyList<string> _generic = [StarMediaType, "audio/unknown", "video/unknown", "application/octet-stream", "application/vnd.sun.star.media"];

    public string Extension(string mediaType) =>
        _types.Where(type => string.Equals(type.MediaType, mediaType, StringComparison.OrdinalIgnoreCase)).Select(type => type.Extension).DefaultIfEmpty("bin").First();

    public string? FromExtension(string? path)
    {
        var extension = Path.GetExtension(path ?? string.Empty).TrimStart('.');
        return _types.FirstOrDefault(type => string.Equals(type.Extension, extension, StringComparison.OrdinalIgnoreCase)).MediaType;
    }

    public bool IsGeneric(string? mediaType) =>
        string.IsNullOrEmpty(mediaType) || _generic.Contains(mediaType, StringComparer.OrdinalIgnoreCase);

    public string Resolve(string? declared, string? path, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsGeneric(declared)) return declared!;

        var byExtension = FromExtension(path);
        if (byExtension is not null) return byExtension;

        return Sniff(data) ?? (declared?.StartsWith("audio", StringComparison.OrdinalIgnoreCase) == true ? "audio/mpeg" : "video/mp4");
    }

    public bool IsAudio(string mediaType) => mediaType.StartsWith("audio", StringComparison.OrdinalIgnoreCase);

    private string? Sniff(byte[] data)
    {
        if (data.Length < 12) return null;
        var head = Encoding.ASCII.GetString(data, 0, 12);
        if (head.StartsWith("FORM", StringComparison.Ordinal) && head[8..12] is "AIFF" or "AIFC") return "audio/aiff";
        if (head.StartsWith("RIFF", StringComparison.Ordinal)) return head[8..12] == "WAVE" ? "audio/wav" : head[8..12] == "AVI " ? "video/x-msvideo" : null;
        if (head.StartsWith("OggS", StringComparison.Ordinal)) return "audio/ogg";
        if (head.StartsWith("fLaC", StringComparison.Ordinal)) return "audio/flac";
        if (head.StartsWith("ID3", StringComparison.Ordinal) || (data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)) return "audio/mpeg";
        if (data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3) return "video/webm";
        if (head[4..8] == "ftyp")
        {
            var brand = head[8..12];
            return brand.StartsWith("M4A", StringComparison.Ordinal) ? "audio/mp4" : brand == "qt  " ? "video/quicktime" : "video/mp4";
        }

        return null;
    }
}
