using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using PulseOffice.Foundation.Network;
using PulseOffice.Settings;

namespace PulseOffice.Updates;

public sealed record VerifiedPackage(ReleaseVersion Version, string Path);

public interface IUpdateDownloader
{
    Task<VerifiedPackage> DownloadAsync(ReleaseVersion version, UpdatePackage package, IProgress<double>? progress, CancellationToken ct);
}

public sealed class UpdateDownloader(
    IOutboundHttpClients clients,
    IUpdateEndpoints endpoints,
    IUpdateTrust trust,
    IChecksumList checksums,
    IUserDataPaths paths) : IUpdateDownloader
{
    private const long MaximumPackageBytes = 1024L * 1024 * 1024;
    private const int MaximumListBytes = 64 * 1024;
    private const int MaximumSignatureBytes = 4 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    public async Task<VerifiedPackage> DownloadAsync(ReleaseVersion version, UpdatePackage package, IProgress<double>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (!trust.IsConfigured) throw new UpdateVerificationException("This build has no update signing key.");
        if (package.Signature is not { } signatureAsset) throw new UpdateVerificationException("The release has no signature for its checksums.");
        if (package.Asset.Size is <= 0 or > MaximumPackageBytes) throw new UpdateVerificationException("The package size is not acceptable.");

        using var client = clients.Create(OutboundAudience.DocumentContent, Timeout);
        var list = await SmallAsync(client, package.Checksums, MaximumListBytes, ct).ConfigureAwait(false);
        var signature = await SmallAsync(client, signatureAsset, MaximumSignatureBytes, ct).ConfigureAwait(false);
        if (!trust.Verifies(list, signature)) throw new UpdateVerificationException("The checksum list is not signed by PulseOffice.");

        var expected = checksums.Read(Encoding.UTF8.GetString(list)).GetValueOrDefault(package.Asset.Name)
            ?? throw new UpdateVerificationException("The checksum list does not name the package.");

        var folder = Path.Combine(paths.CacheDirectory, "updates", version.ToString());
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, package.Asset.Name);
        var partial = target + ".part";

        try
        {
            var actual = await DownloadAsync(client, package.Asset, partial, progress, ct).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                throw new UpdateVerificationException("The downloaded package does not match its checksum.");
            File.Move(partial, target, overwrite: true);
            return new VerifiedPackage(version, target);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    private async Task<byte[]> DownloadAsync(HttpClient client, ReleaseAsset asset, string path, IProgress<double>? progress, CancellationToken ct)
    {
        using var response = await SendAsync(client, asset, ct).ConfigureAwait(false);
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > asset.Size) throw new UpdateVerificationException("The package is larger than announced.");
            hash.AppendData(buffer, 0, read);
            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            progress?.Report((double)total / asset.Size);
        }

        if (total != asset.Size) throw new UpdateVerificationException("The package is shorter than announced.");
        return hash.GetHashAndReset();
    }

    private async Task<byte[]> SmallAsync(HttpClient client, ReleaseAsset asset, int limit, CancellationToken ct)
    {
        using var response = await SendAsync(client, asset, ct).ConfigureAwait(false);
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await source.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit) throw new UpdateVerificationException($"{asset.Name} is too large.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, ReleaseAsset asset, CancellationToken ct)
    {
        Trusted(asset.Address);
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.Address);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PulseOffice-Updater", "1"));
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is { } final) Trusted(final);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private void Trusted(Uri address)
    {
        if (address.Scheme != Uri.UriSchemeHttps
            || !endpoints.TrustedDownloadHosts.Any(host => string.Equals(host, address.Host, StringComparison.OrdinalIgnoreCase)))
            throw new UpdateVerificationException($"Downloads from {address.Host} are not accepted.");
    }
}
