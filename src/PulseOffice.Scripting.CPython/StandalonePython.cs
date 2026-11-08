using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PulseOffice.Platform;

namespace PulseOffice.Scripting.CPython;

public sealed record StandaloneAsset(string Name, Uri Download, long Bytes, Version Version, string? Sha256);

public interface IStandalonePythonCatalog
{
    StandaloneAsset? Pick(string releaseJson, string? checksums);

    Uri LatestRelease { get; }
}

public sealed class StandalonePythonCatalog(IHostSystem host) : IStandalonePythonCatalog
{
    public StandalonePythonCatalog()
        : this(new RuntimeHostSystem())
    {
    }

    private readonly PythonVersionRange _range = new();

    public Uri LatestRelease { get; } = new("https://api.github.com/repos/astral-sh/python-build-standalone/releases/latest");

    public string Triple => (RuntimeInformation.OSArchitecture, Platform()) switch
    {
        (Architecture.Arm64, "darwin") => "aarch64-apple-darwin",
        (_, "darwin") => "x86_64-apple-darwin",
        (Architecture.Arm64, "windows") => "aarch64-pc-windows-msvc",
        (_, "windows") => "x86_64-pc-windows-msvc",
        (Architecture.Arm64, _) => "aarch64-unknown-linux-gnu",
        _ => "x86_64-unknown-linux-gnu",
    };

    public StandaloneAsset? Pick(string releaseJson, string? checksums)
    {
        using var document = JsonDocument.Parse(releaseJson);
        if (!document.RootElement.TryGetProperty("assets", out var assets)) return null;

        var suffix = $"-{Triple}-install_only.tar.gz";
        var candidates = new List<StandaloneAsset>();
        foreach (var asset in assets.EnumerateArray())
        {
            var name = Text(asset, "name");
            if (!name.StartsWith("cpython-", StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal)) continue;
            if (name.Contains("freethreaded", StringComparison.Ordinal) || name.Contains("debug", StringComparison.Ordinal)) continue;
            var build = name.IndexOf('+', StringComparison.Ordinal);
            if (build <= "cpython-".Length) continue;
            if (!Version.TryParse(name["cpython-".Length..build], out var version) || !_range.IsSupported(version)) continue;
            if (Released(Text(asset, "browser_download_url")) is not { } download) continue;
            if (Checksum(checksums, name) is not { } sha256) continue;
            candidates.Add(new StandaloneAsset(name, download,
                asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0, version, sha256));
        }

        return candidates
            .OrderBy(candidate => candidate.Version.Major == PythonVersionRange.PreferredVersion.Major && candidate.Version.Minor == PythonVersionRange.PreferredVersion.Minor ? 0 : 1)
            .ThenByDescending(candidate => candidate.Version)
            .FirstOrDefault();
    }

    public string? ChecksumsAsset(string releaseJson)
    {
        using var document = JsonDocument.Parse(releaseJson);
        if (!document.RootElement.TryGetProperty("assets", out var assets)) return null;
        return assets.EnumerateArray()
            .Where(asset => Text(asset, "name") == "SHA256SUMS")
            .Select(asset => Released(Text(asset, "browser_download_url"))?.AbsoluteUri)
            .FirstOrDefault(address => address is not null);
    }

    public Uri ReleaseDownloads { get; } = new("https://github.com/astral-sh/python-build-standalone/releases/download/");

    private Uri? Released(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var download)
        && download.Scheme == Uri.UriSchemeHttps
        && download.IsDefaultPort
        && string.IsNullOrEmpty(download.UserInfo)
        && download.Host.Equals(ReleaseDownloads.Host, StringComparison.OrdinalIgnoreCase)
        && download.AbsolutePath.StartsWith(ReleaseDownloads.AbsolutePath, StringComparison.Ordinal)
            ? download
            : null;

    private string Text(JsonElement asset, string property) =>
        asset.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private string? Checksum(string? checksums, string name) =>
        checksums?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == name)
            .Select(parts => parts[0].ToLowerInvariant())
            .FirstOrDefault();

    private string Platform() =>
        host.IsMacOS ? "darwin" : host.IsWindows ? "windows" : "linux";
}

public interface IStandalonePythonStore
{
    string Root { get; }

    PythonRuntimeDescriptor? Installed();

    Task<PythonRuntimeDescriptor?> InstallAsync(IPythonDownloadConsent consent, CancellationToken ct = default);
}

public sealed class StandalonePythonStore(
    string root,
    HttpClient http,
    StandalonePythonCatalog catalog,
    IPythonDiscovery discovery,
    ILogger<StandalonePythonStore> logger,
    IHostSystem? host = null) : IStandalonePythonStore
{
    private readonly IHostSystem _host = host ?? new RuntimeHostSystem();

    public string Root { get; } = root;

    private string Installation => Path.Combine(Root, "python");

    public PythonRuntimeDescriptor? Installed()
    {
        var executable = _host.IsWindows
            ? Path.Combine(Installation, "python.exe")
            : Path.Combine(Installation, "bin", "python3");
        if (!File.Exists(executable)) return null;

        return discovery.Inspect(executable).Runtime is { } runtime ? runtime with { Origin = PythonProvisioningMode.Standalone } : null;
    }

    public async Task<PythonRuntimeDescriptor?> InstallAsync(IPythonDownloadConsent consent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(consent);

        var release = await TextAsync(catalog.LatestRelease, ct).ConfigureAwait(false);
        var checksums = catalog.ChecksumsAsset(release) is { } sums ? await TextAsync(new Uri(sums), ct).ConfigureAwait(false) : null;
        if (catalog.Pick(release, checksums) is not { } asset)
        {
            logger.LogWarning("No standalone Python build matches {Triple}", catalog.Triple);
            return null;
        }

        var offer = new PythonDownloadOffer(asset.Version.ToString(), asset.Download.Host + asset.Download.AbsolutePath, asset.Bytes, Installation);
        if (!await consent.AllowAsync(offer, ct).ConfigureAwait(false))
        {
            logger.LogInformation("The user declined the standalone Python download");
            return null;
        }

        logger.LogInformation("Downloading standalone Python {Version} from {Source}", asset.Version, asset.Download);
        try
        {
            var archive = await DownloadAsync(asset, consent.Progress, ct).ConfigureAwait(false);
            try
            {
                if (asset.Sha256 is not { } expected || !string.Equals(expected, await HashAsync(archive, ct).ConfigureAwait(false), StringComparison.Ordinal))
                {
                    logger.LogError("Checksum mismatch for {Asset}", asset.Name);
                    return null;
                }

                consent.Progress.Report(new ProvisioningProgress("extract", 0.95));
                await ExtractAsync(archive, ct).ConfigureAwait(false);
            }
            finally
            {
                File.Delete(archive);
            }
        }
        finally
        {
            consent.Progress.Report(new ProvisioningProgress("ready", 1));
        }

        return Installed();
    }

    private async Task<string> DownloadAsync(StandaloneAsset asset, IProgress<ProvisioningProgress> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        var archive = Path.Combine(Root, asset.Name + ".part");
        using var response = await http.SendAsync(Request(asset.Download), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? asset.Bytes;
        var ceiling = asset.Bytes > 0 ? asset.Bytes : MaximumDownloadBytes;

        try
        {
            await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var target = File.Create(archive);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                done += read;
                if (done > ceiling) throw new InvalidDataException($"The download of {asset.Name} is larger than announced.");
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                if (total > 0) progress.Report(new ProvisioningProgress("download", 0.9 * Math.Min(done, total) / total));
            }
        }
        catch
        {
            File.Delete(archive);
            throw;
        }

        return archive;
    }

    private const long MaximumDownloadBytes = 512L * 1024 * 1024;

    private HttpRequestMessage Request(Uri address)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.UserAgent.ParseAdd("PulseOffice");
        return request;
    }

    private async Task<string> TextAsync(Uri address, CancellationToken ct)
    {
        using var request = Request(address);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private async Task<string> HashAsync(string archive, CancellationToken ct)
    {
        await using var stream = File.OpenRead(archive);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
    }

    private async Task ExtractAsync(string archive, CancellationToken ct)
    {
        var staging = Path.Combine(Root, "staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);

        await using (var file = File.OpenRead(archive))
        await using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            await TarFile.ExtractToDirectoryAsync(gzip, staging, overwriteFiles: true, ct).ConfigureAwait(false);

        if (Directory.Exists(Installation)) Directory.Delete(Installation, recursive: true);
        Directory.Move(Path.Combine(staging, "python"), Installation);
        Directory.Delete(staging, recursive: true);
    }
}
