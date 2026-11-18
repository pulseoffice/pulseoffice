using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class MediaFileExtensions
{
    public string For(MediaElement media)
    {
        ArgumentNullException.ThrowIfNull(media);
        var named = string.IsNullOrWhiteSpace(media.FileName) ? "" : Path.GetExtension(media.FileName.Trim());
        return named.Length > 1 ? named : FromType(media.MimeType);
    }

    public string FromType(string? mimeType) => mimeType switch
    {
        "video/mp4" => ".mp4",
        "video/quicktime" => ".mov",
        "video/webm" => ".webm",
        "audio/mpeg" => ".mp3",
        "audio/wav" or "audio/x-wav" => ".wav",
        "audio/ogg" => ".ogg",
        "audio/mp4" => ".m4a",
        _ => ".bin",
    };
}
