using Xunit;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Foundation.Network;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Settings;

namespace PulseOffice.Updates.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("0.1.0+abc", 0, 1, 0)]
    [InlineData("2.10", 2, 10, 0)]
    [InlineData("V3.0.1-beta", 3, 0, 1)]
    public void Parses_tags_and_informational_versions(string text, int major, int minor, int patch)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("1.-2.3")]
    public void Rejects_what_is_not_a_version(string text) => Assert.False(ReleaseVersion.TryParse(text, out _));

    [Fact]
    public void Compares_numerically_not_as_text() =>
        Assert.True(new ReleaseVersion(0, 10, 0) > new ReleaseVersion(0, 9, 9));
}

public sealed class ReleaseDocumentTests
{
    private readonly GitHubReleaseDocument _document = new();

    [Fact]
    public void Reads_tag_notes_page_and_https_assets()
    {
        var release = _document.Read(Encoding.UTF8.GetBytes("""
            {"tag_name":"v0.2.0","body":"Fixes","html_url":"https://github.com/pulseoffice/pulseoffice/releases/tag/v0.2.0",
             "draft":false,"prerelease":false,
             "assets":[{"name":"SHA256SUMS","browser_download_url":"https://github.com/a/SHA256SUMS","size":10},
                       {"name":"evil","browser_download_url":"http://github.com/evil","size":10}]}
            """));

        Assert.NotNull(release);
        Assert.Equal(new ReleaseVersion(0, 2, 0), release.Version);
        Assert.Equal("Fixes", release.Notes);
        Assert.Equal(["SHA256SUMS"], release.Assets.Select(asset => asset.Name));
    }

    [Theory]
    [InlineData("\"draft\":true")]
    [InlineData("\"prerelease\":true")]
    public void Ignores_drafts_and_prereleases(string flag) =>
        Assert.Null(_document.Read(Encoding.UTF8.GetBytes(
            "{\"tag_name\":\"v9.0.0\",\"html_url\":\"https://github.com/x\"," + flag + "}")));
}

public sealed class PackageChoiceTests
{
    private static ReleaseInfo Release(params string[] names) => new(
        new ReleaseVersion(1, 0, 0), "v1.0.0", string.Empty, new Uri("https://github.com/x"),
        [.. names.Select(name => new ReleaseAsset(name, new Uri("https://github.com/" + name), 100))]);

    [Theory]
    [InlineData(HostSystemKind.MacOS, Architecture.Arm64, "PulseOffice-1.0.0-macos-arm64.dmg")]
    [InlineData(HostSystemKind.MacOS, Architecture.X64, "PulseOffice-1.0.0-macos-x64.dmg")]
    [InlineData(HostSystemKind.Windows, Architecture.X64, "PulseOffice-1.0.0-windows-x64-setup.exe")]
    public void Picks_the_package_of_the_running_system(HostSystemKind kind, Architecture architecture, string expected)
    {
        var choice = new UpdatePackageChoice(new FixedHostSystem(kind, architecture: architecture));
        var package = choice.Choose(Release(expected, "SHA256SUMS", "SHA256SUMS.sig", "other.dmg"));
        Assert.Equal(expected, package?.Asset.Name);
        Assert.Equal("SHA256SUMS.sig", package?.Signature?.Name);
    }

    [Theory]
    [InlineData(Architecture.X64, "PulseOffice-1.0.0-linux-x86_64.AppImage")]
    [InlineData(Architecture.Arm64, "PulseOffice-1.0.0-linux-aarch64.AppImage")]
    public void Linux_updates_the_appimage_of_its_architecture(Architecture architecture, string expected) =>
        Assert.Equal(expected, new UpdatePackageChoice(new FixedHostSystem(HostSystemKind.Linux, architecture: architecture))
            .Choose(Release(expected, "SHA256SUMS", "SHA256SUMS.sig"))?.Asset.Name);

    [Fact]
    public void Without_checksums_there_is_no_package() =>
        Assert.Null(new UpdatePackageChoice(new FixedHostSystem(HostSystemKind.MacOS, architecture: Architecture.Arm64))
            .Choose(Release("PulseOffice-1.0.0-macos-arm64.dmg")));
}

public sealed class VerificationTests
{
    [Fact]
    public void Checksum_list_reads_both_sha256sum_styles_and_refuses_paths()
    {
        var hash = new string('a', 64);
        var list = new Sha256SumsList().Read($"{hash}  one.dmg\n{hash} *two.exe\r\n{hash}  ../three\nshort line\n");
        Assert.Equal(["one.dmg", "two.exe"], list.Keys.Order());
    }

    [Fact]
    public void Trust_accepts_only_the_matching_key_and_content()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var content = Encoding.UTF8.GetBytes("checksums");
        var signature = signer.SignData(content, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        using var trust = new EcdsaUpdateTrust(new FixedUpdateTrustKey(signer.ExportSubjectPublicKeyInfoPem()));
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stranger = new EcdsaUpdateTrust(new FixedUpdateTrustKey(other.ExportSubjectPublicKeyInfoPem()));

        Assert.True(trust.Verifies(content, signature));
        Assert.False(trust.Verifies(Encoding.UTF8.GetBytes("checksumz"), signature));
        Assert.False(stranger.Verifies(content, signature));
    }

    [Fact]
    public void Without_a_key_nothing_is_trusted()
    {
        using var trust = new EcdsaUpdateTrust(new FixedUpdateTrustKey(null));
        Assert.False(trust.IsConfigured);
        Assert.False(trust.Verifies([1], [2]));
    }
}

public sealed class DownloaderTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("pulse-updates-").FullName;
    private readonly ECDsa _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _package = Encoding.UTF8.GetBytes("the new application");

    public void Dispose()
    {
        _signer.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    private (UpdateDownloader Downloader, UpdatePackage Package) Build(byte[] served, string? listOverride = null, string host = "github.com")
    {
        var name = "PulseOffice-1.0.0-macos-arm64.dmg";
        var list = listOverride ?? Convert.ToHexStringLower(SHA256.HashData(_package)) + "  " + name + "\n";
        var listBytes = Encoding.UTF8.GetBytes(list);
        var signature = _signer.SignData(Encoding.UTF8.GetBytes(Convert.ToHexStringLower(SHA256.HashData(_package)) + "  " + name + "\n"),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        var files = new Dictionary<string, byte[]> { ["/pkg"] = served, ["/sums"] = listBytes, ["/sig"] = signature };
        var trust = new EcdsaUpdateTrust(new FixedUpdateTrustKey(_signer.ExportSubjectPublicKeyInfoPem()));
        var downloader = new UpdateDownloader(new FakeClients(files), new GitHubUpdateEndpoints(), trust, new Sha256SumsList(), new FixedPaths(_home));
        var package = new UpdatePackage(
            new ReleaseAsset(name, new Uri($"https://{host}/pkg"), _package.Length),
            new ReleaseAsset("SHA256SUMS", new Uri("https://github.com/sums"), listBytes.Length),
            new ReleaseAsset("SHA256SUMS.sig", new Uri("https://github.com/sig"), signature.Length));
        return (downloader, package);
    }

    [Fact]
    public async Task A_signed_matching_package_lands_in_the_cache()
    {
        var (downloader, package) = Build(_package);
        var verified = await downloader.DownloadAsync(new ReleaseVersion(1, 0, 0), package, null, TestContext.Current.CancellationToken);
        Assert.Equal(_package, await File.ReadAllBytesAsync(verified.Path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_tampered_package_is_refused_and_removed()
    {
        var tampered = (byte[])_package.Clone();
        tampered[0] ^= 1;
        var (downloader, package) = Build(tampered);
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            downloader.DownloadAsync(new ReleaseVersion(1, 0, 0), package, null, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task An_unsigned_checksum_list_is_refused()
    {
        var (downloader, package) = Build(_package, listOverride: new string('0', 64) + "  PulseOffice-1.0.0-macos-arm64.dmg\n");
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            downloader.DownloadAsync(new ReleaseVersion(1, 0, 0), package, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Downloads_from_other_hosts_are_refused()
    {
        var (downloader, package) = Build(_package, host: "example.com");
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            downloader.DownloadAsync(new ReleaseVersion(1, 0, 0), package, null, TestContext.Current.CancellationToken));
    }
}

public sealed class ServiceTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("pulse-updates-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private (UpdateService Service, ManualClock Clock) Build(ReleaseInfo? latest, bool automatic = true, bool canInstall = true)
    {
        var user = new InMemorySettingsStore(SettingScope.User);
        var settings = new SettingsService([new UpdateSettings()], [user], new TextCatalog(UiLanguage.English), NullLogger<SettingsService>.Instance);
        settings.Set(UpdateSettingKeys.Automatic, automatic);
        var clock = new ManualClock(new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = new UpdateService(
            new FixedFeed(latest),
            new UpdatePackageChoice(new FixedHostSystem(HostSystemKind.MacOS, architecture: Architecture.Arm64)),
            new UpdateDownloader(new FakeClients(new Dictionary<string, byte[]>()), new GitHubUpdateEndpoints(), new EcdsaUpdateTrust(new FixedUpdateTrustKey(null)), new Sha256SumsList(), new FixedPaths(_home)),
            new FixedInstaller(canInstall),
            new EcdsaUpdateTrust(new FixedUpdateTrustKey(key.ExportSubjectPublicKeyInfoPem())),
            new FixedInstalledVersion(new ReleaseVersion(0, 1, 0)),
            new JsonUpdateStateStore(new FixedPaths(_home)),
            new GitHubUpdateEndpoints(),
            settings,
            clock,
            NullLogger<UpdateService>.Instance);
        return (service, clock);
    }

    private static ReleaseInfo Release(string version) => new(
        ReleaseVersion.TryParse(version, out var parsed) ? parsed : default, "v" + version, "notes", new Uri("https://github.com/x"),
        [.. new[] { $"PulseOffice-{version}-macos-arm64.dmg", "SHA256SUMS", "SHA256SUMS.sig" }
            .Select(name => new ReleaseAsset(name, new Uri("https://github.com/" + name), 10))]);

    [Fact]
    public async Task Checks_at_most_once_a_day()
    {
        var (service, clock) = Build(Release("0.2.0"));
        Assert.True(service.AutomaticCheckIsDue);
        await service.CheckAsync(TestContext.Current.CancellationToken);
        Assert.False(service.AutomaticCheckIsDue);
        clock.Advance(TimeSpan.FromHours(23));
        Assert.False(service.AutomaticCheckIsDue);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.True(service.AutomaticCheckIsDue);
    }

    [Fact]
    public void Turning_the_setting_off_stops_automatic_checks() =>
        Assert.False(Build(Release("0.2.0"), automatic: false).Service.AutomaticCheckIsDue);

    [Fact]
    public async Task A_newer_signed_release_is_installable()
    {
        var check = await Build(Release("0.2.0")).Service.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UpdateCheckKind.Installable, check.Kind);
    }

    [Fact]
    public async Task Same_or_older_release_is_up_to_date()
    {
        Assert.Equal(UpdateCheckKind.UpToDate, (await Build(Release("0.1.0")).Service.CheckAsync(TestContext.Current.CancellationToken)).Kind);
        Assert.Equal(UpdateCheckKind.UpToDate, (await Build(null).Service.CheckAsync(TestContext.Current.CancellationToken)).Kind);
    }

    [Fact]
    public async Task Where_nothing_can_be_installed_the_user_downloads_by_hand()
    {
        var check = await Build(Release("0.2.0"), canInstall: false).Service.CheckAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UpdateCheckKind.ManualDownload, check.Kind);
    }

    [Fact]
    public void A_skipped_version_is_remembered()
    {
        var (service, _) = Build(Release("0.2.0"));
        service.Skip(new ReleaseVersion(0, 2, 0));
        Assert.True(service.IsSkipped(new ReleaseVersion(0, 2, 0)));
        Assert.False(service.IsSkipped(new ReleaseVersion(0, 3, 0)));
    }
}

internal sealed class FakeClients(IReadOnlyDictionary<string, byte[]> files) : IOutboundHttpClients
{
    public HttpClient Create(OutboundAudience audience, TimeSpan timeout) => new(new Handler(files));

    private sealed class Handler(IReadOnlyDictionary<string, byte[]> files) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(files.TryGetValue(request.RequestUri!.AbsolutePath, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = request }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
    }
}

internal sealed class FixedPaths(string home) : IUserDataPaths
{
    public string ConfigurationDirectory => Path.Combine(home, "config");

    public string CacheDirectory => Path.Combine(home, "cache");

    public string SettingsFile => Configuration("settings.json");

    public string RecoveryDirectory => Path.Combine(CacheDirectory, "recovery");

    public string Configuration(string name) => Path.Combine(ConfigurationDirectory, name);
}

internal sealed class FixedFeed(ReleaseInfo? latest) : IReleaseFeed
{
    public Task<ReleaseInfo?> LatestAsync(CancellationToken ct) => Task.FromResult(latest);
}

internal sealed class FixedInstaller(bool canInstall) : IUpdateInstaller
{
    public bool CanInstall => canInstall;

    public Task<StagedUpdate> StageAsync(string packagePath, CancellationToken ct) => Task.FromResult(new StagedUpdate(packagePath, packagePath));

    public void InstallAfterExit(StagedUpdate staged)
    {
    }
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class EmbeddedKeyTests
{
    [Fact]
    public void The_build_carries_a_p256_update_key()
    {
        using var trust = new EcdsaUpdateTrust(new EmbeddedUpdateTrustKey());
        Assert.True(trust.IsConfigured);
    }
}
