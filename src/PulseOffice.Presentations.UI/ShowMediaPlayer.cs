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
    private readonly MediaFileExtensions _extensions = new();

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
        var name = string.IsNullOrWhiteSpace(media.FileName) ? $"media{_extensions.For(media)}" : media.FileName;
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
}
