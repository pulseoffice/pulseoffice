using System.Globalization;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public interface IShowMediaPlayer
{
    Task PlayAsync(MediaElement media, string? documentPath);
}

public sealed class ShowMediaPlayer(
    IDeckMediaFiles mediaFiles,
    IShellOpener opener,
    IEmbeddedFileLauncher launcher,
    IStatusBarService status,
    ITextCatalog texts) : IShowMediaPlayer
{
    public async Task PlayAsync(MediaElement media, string? documentPath)
    {
        ArgumentNullException.ThrowIfNull(media);
        switch (mediaFiles.Source(media, documentPath))
        {
            case LinkedMediaFile { Exists: true } file:
                ReportUnopened(opener.OpenFile(file.Path), file.Path);
                break;
            case BlockedMediaFile blocked:
                Warn("deck.media.blocked", Path.GetFileName(blocked.Path));
                break;
            case LinkedMediaFile missing:
                Warn("deck.media.linkMissing", missing.Path);
                break;
            case LinkedMediaAddress address:
                ReportUnopened(opener.OpenUrl(address.Address), address.Address);
                break;
            default:
                await PlayEmbeddedAsync(media).ConfigureAwait(true);
                break;
        }
    }

    private async Task PlayEmbeddedAsync(MediaElement media)
    {
        var name = string.IsNullOrWhiteSpace(media.FileName) ? $"media{Extension(media.MimeType)}" : media.FileName;
        try
        {
            ReportUnopened(await launcher.OpenAsync(media.Data, name).ConfigureAwait(true), Path.GetFileName(name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warn("deck.media.openFailed", Path.GetFileName(name));
        }
    }

    private void ReportUnopened(OperationResult<bool> opened, string target)
    {
        if (!opened.Succeeded) Warn("deck.media.openFailed", target);
    }

    private void Warn(string key, string argument) =>
        status.Report(string.Format(CultureInfo.CurrentCulture, texts[key], argument), NotificationSeverity.Warning);

    private static string Extension(string? mimeType) => mimeType switch
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
