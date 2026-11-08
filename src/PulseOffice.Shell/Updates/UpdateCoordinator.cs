using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Updates;

namespace PulseOffice.Shell.Updates;

public interface IUpdateCoordinator
{
    Task CheckInBackgroundAsync(CancellationToken ct = default);

    Task CheckNowAsync(CancellationToken ct = default);
}

public sealed class UpdateCoordinator(
    IUpdateService updates,
    IInstalledVersion installed,
    IDialogService dialogs,
    INotificationCenter notifications,
    IShellOpener opener,
    ITextCatalog texts,
    Func<IShellPresenter> presenter,
    ILogger<UpdateCoordinator> logger) : IUpdateCoordinator
{
    public const string CommandId = "shell.help.checkUpdates";

    private UpdateCheck? _pending;

    public async Task CheckInBackgroundAsync(CancellationToken ct = default)
    {
        if (!updates.AutomaticCheckIsDue) return;
        var check = await updates.CheckAsync(ct).ConfigureAwait(true);
        if (!check.IsNewer || check.Release is not { } release || updates.IsSkipped(release.Version)) return;

        _pending = check;
        notifications.Post(new Notification(
            texts.Format("updates.notification", release.Version.ToString()),
            NotificationSeverity.Success,
            new CommandNotificationAction(CommandId, "updates.notification.action")));
    }

    public async Task CheckNowAsync(CancellationToken ct = default)
    {
        var check = _pending ?? await updates.CheckAsync(ct).ConfigureAwait(true);
        _pending = null;

        switch (check.Kind)
        {
            case UpdateCheckKind.UpToDate:
                notifications.Post(new Notification(texts.Format("updates.upToDate", installed.Current.ToString()), NotificationSeverity.Success));
                return;
            case UpdateCheckKind.Failed:
                notifications.Post(new Notification(texts["updates.checkFailed"], NotificationSeverity.Warning));
                return;
        }

        if (check.Release is not { } release) return;
        await OfferAsync(check, release, ct).ConfigureAwait(true);
    }

    private async Task OfferAsync(UpdateCheck check, ReleaseInfo release, CancellationToken ct)
    {
        var offer = new UpdateOffer(release.Version.ToString(), installed.Current.ToString(), release.Notes, check.Kind == UpdateCheckKind.Installable);
        var view = new UpdateOfferView(offer, texts, (progress, token) => InstallAsync(check, progress, token));
        var choice = await dialogs.ShowAsync<object>(view,
            new DialogOptions { TitleKey = "updates.offer.title", PreferredWidth = 560 }, ct).ConfigureAwait(true);

        switch (choice)
        {
            case UpdateChoice.Skip:
                updates.Skip(release.Version);
                break;
            case UpdateChoice.OpenPage:
                opener.OpenUrl(release.Page.ToString());
                break;
            case UpdateChoice.Installed:
                presenter().RequestQuit();
                break;
        }
    }

    private async Task<string?> InstallAsync(UpdateCheck check, IProgress<double> progress, CancellationToken ct)
    {
        try
        {
            var package = await updates.DownloadAsync(check, progress, ct).ConfigureAwait(false);
            await updates.InstallAfterExitAsync(package, ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is UpdateVerificationException or IOException or HttpRequestException or InvalidOperationException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Update installation failed");
            return ex.Message;
        }
    }
}
