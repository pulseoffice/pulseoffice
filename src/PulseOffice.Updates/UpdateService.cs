using Microsoft.Extensions.Logging;
using PulseOffice.Platform;
using PulseOffice.Settings;

namespace PulseOffice.Updates;

public enum UpdateCheckKind
{
    UpToDate,
    Installable,
    ManualDownload,
    Failed,
}

public sealed record UpdateCheck(UpdateCheckKind Kind, ReleaseInfo? Release = null, UpdatePackage? Package = null, string? Problem = null)
{
    public bool IsNewer => Kind is UpdateCheckKind.Installable or UpdateCheckKind.ManualDownload;
}

public interface IUpdateService
{
    bool AutomaticCheckIsDue { get; }

    Task<UpdateCheck> CheckAsync(CancellationToken ct);

    bool IsSkipped(ReleaseVersion version);

    void Skip(ReleaseVersion version);

    Task<VerifiedPackage> DownloadAsync(UpdateCheck check, IProgress<double>? progress, CancellationToken ct);

    Task InstallAfterExitAsync(VerifiedPackage package, CancellationToken ct);

    Uri ReleasesPage { get; }
}

public sealed class UpdateService(
    IReleaseFeed feed,
    IUpdatePackageChoice packages,
    IUpdateDownloader downloader,
    IUpdateInstaller installer,
    IUpdateTrust trust,
    IInstalledVersion installed,
    IUpdateStateStore state,
    IUpdateEndpoints endpoints,
    ISettingsService settings,
    TimeProvider time,
    ILogger<UpdateService> logger) : IUpdateService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    public Uri ReleasesPage => endpoints.ReleasesPage;

    public bool AutomaticCheckIsDue
    {
        get
        {
            if (!settings.Get<bool>(UpdateSettingKeys.Automatic)) return false;
            var last = state.Load().LastCheck;
            var now = time.GetUtcNow();
            return last is not { } checkedAt || checkedAt > now || now - checkedAt >= Interval;
        }
    }

    public async Task<UpdateCheck> CheckAsync(CancellationToken ct)
    {
        try
        {
            var release = await feed.LatestAsync(ct).ConfigureAwait(false);
            Remember(stored => stored with { LastCheck = time.GetUtcNow() });
            if (release is null || release.Version <= installed.Current)
            {
                logger.LogInformation("Update check: {Version} is current", installed.Current);
                return new UpdateCheck(UpdateCheckKind.UpToDate, release);
            }

            var package = packages.Choose(release);
            var installable = package is { Signature: not null } && installer.CanInstall && trust.IsConfigured;
            logger.LogInformation("Update check: {Version} available, installable {Installable}", release.Version, installable);
            return new UpdateCheck(installable ? UpdateCheckKind.Installable : UpdateCheckKind.ManualDownload, release, package);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException
                                       or System.Text.Json.JsonException or TaskCanceledException or PulseOffice.Foundation.Network.DestinationRefusedException or UpdateVerificationException)
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogWarning(ex, "Update check failed");
            return new UpdateCheck(UpdateCheckKind.Failed, Problem: ex.Message);
        }
    }

    public bool IsSkipped(ReleaseVersion version) =>
        ReleaseVersion.TryParse(state.Load().SkippedVersion, out var skipped) && skipped == version;

    public void Skip(ReleaseVersion version) => Remember(stored => stored with { SkippedVersion = version.ToString() });

    public Task<VerifiedPackage> DownloadAsync(UpdateCheck check, IProgress<double>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (check.Kind != UpdateCheckKind.Installable || check.Release is null || check.Package is null)
            throw new InvalidOperationException("This update cannot be installed automatically.");
        return downloader.DownloadAsync(check.Release.Version, check.Package, progress, ct);
    }

    public async Task InstallAfterExitAsync(VerifiedPackage package, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(package);
        var staged = await installer.StageAsync(package.Path, ct).ConfigureAwait(false);
        installer.InstallAfterExit(staged);
        logger.LogInformation("Update {Version} staged; it installs when PulseOffice quits", package.Version);
    }

    private void Remember(Func<UpdateState, UpdateState> change)
    {
        try
        {
            state.Save(change(state.Load()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Update state could not be saved");
        }
    }
}
