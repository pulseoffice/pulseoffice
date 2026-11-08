using System.Net.Http.Headers;
using System.Text.Json;
using PulseOffice.Foundation.Network;

namespace PulseOffice.Updates;

public interface IReleaseFeed
{
    Task<ReleaseInfo?> LatestAsync(CancellationToken ct);
}

public sealed class GitHubReleaseFeed(IOutboundHttpClients clients, IUpdateEndpoints endpoints, IReleaseDocument document) : IReleaseFeed
{
    private const int MaximumResponseBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public async Task<ReleaseInfo?> LatestAsync(CancellationToken ct)
    {
        using var client = clients.Create(OutboundAudience.DocumentContent, Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoints.LatestRelease);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PulseOffice-Updater", "1"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaximumResponseBytes) throw new InvalidDataException("The release description is too large.");
            buffer.Write(chunk, 0, read);
        }

        return document.Read(buffer.ToArray());
    }
}

public interface IReleaseDocument
{
    ReleaseInfo? Read(ReadOnlySpan<byte> json);
}

public sealed class GitHubReleaseDocument : IReleaseDocument
{
    private const int MaximumNotesLength = 20_000;

    public ReleaseInfo? Read(ReadOnlySpan<byte> json)
    {
        using var parsed = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (Flag(root, "draft") || Flag(root, "prerelease")) return null;

        var tag = Text(root, "tag_name");
        if (!ReleaseVersion.TryParse(tag, out var version)) return null;
        if (!Uri.TryCreate(Text(root, "html_url"), UriKind.Absolute, out var page) || page.Scheme != Uri.UriSchemeHttps) return null;

        var notes = Text(root, "body");
        if (notes.Length > MaximumNotesLength) notes = notes[..MaximumNotesLength];

        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray())
            {
                var name = Text(item, "name");
                if (name.Length == 0 || !Uri.TryCreate(Text(item, "browser_download_url"), UriKind.Absolute, out var address)) continue;
                if (address.Scheme != Uri.UriSchemeHttps) continue;
                var size = item.TryGetProperty("size", out var bytes) && bytes.TryGetInt64(out var value) ? value : 0;
                assets.Add(new ReleaseAsset(name, address, size));
            }

        return new ReleaseInfo(version, tag, notes, page, assets);
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
