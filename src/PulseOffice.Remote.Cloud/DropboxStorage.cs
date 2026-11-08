using System.Globalization;
using System.Text;
using System.Text.Json;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Remote.Cloud;

public sealed class DropboxReader
{
    public IReadOnlyList<RemoteEntry> ReadListing(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];

            if (!document.RootElement.TryGetProperty("entries", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return [];

            var entries = new List<RemoteEntry>();

            foreach (var item in items.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var declared) ? declared.GetString() : null;
                var path = item.TryGetProperty("path_display", out var full) ? full.GetString() : null;

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;

                var tag = item.TryGetProperty(".tag", out var kind) ? kind.GetString() : null;
                var isFolder = string.Equals(tag, "folder", StringComparison.Ordinal);

                var size = item.TryGetProperty("size", out var measured) && measured.TryGetInt64(out var bytes)
                    ? bytes
                    : 0;

                var modified = item.TryGetProperty("server_modified", out var moment)
                    && DateTimeOffset.TryParse(moment.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var when)
                    ? when
                    : (DateTimeOffset?)null;

                entries.Add(new RemoteEntry(name, path, isFolder, size, modified));
            }

            return entries;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed class DropboxStorage(
    HttpClient client, ICloudSessions sessions, DropboxReader reader)
    : CloudStorage(client, sessions)
{
    private const string ListAddress = "https://api.dropboxapi.com/2/files/list_folder";
    private const string DownloadAddress = "https://content.dropboxapi.com/2/files/download";
    private const string UploadAddress = "https://content.dropboxapi.com/2/files/upload";

    public override RemoteProtocol Protocol => RemoteProtocol.Dropbox;

    public override async Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var entries = new List<RemoteEntry>();
        var address = ListAddress;
        var payload = JsonSerializer.Serialize(new Dictionary<string, object> { ["path"] = Folder(path), ["recursive"] = false });

        for (var pages = 0; ; pages++)
        {
            if (pages >= MaximumPages) return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(Diagnostic.Error("CLOUD032", "The cloud storage kept sending more pages."));
            var request = await RequestAsync(connection, HttpMethod.Post, address, ct).ConfigureAwait(false);

            if (!request.Succeeded)
                return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(request.Diagnostics[0]);

            request.Value!.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var body = await TextAsync(request.Value!, ct).ConfigureAwait(false);
            if (!body.Succeeded) return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(body.Diagnostics[0]);

            entries.AddRange(reader.ReadListing(body.Value!));
            if (!JsonFlag(body.Value!, "has_more") || JsonText(body.Value!, "cursor") is not { Length: > 0 } cursor) break;

            address = ListAddress + "/continue";
            payload = JsonSerializer.Serialize(new Dictionary<string, object> { ["cursor"] = cursor });
        }

        return OperationResult<IReadOnlyList<RemoteEntry>>.Ok(entries);
    }

    public override async Task<OperationResult<RemoteContent>> ReadAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var request = await RequestAsync(connection, HttpMethod.Post, DownloadAddress, ct).ConfigureAwait(false);

        if (!request.Succeeded) return OperationResult<RemoteContent>.Fail(request.Diagnostics[0]);

        request.Value!.Headers.Add("Dropbox-API-Arg", Argument(path));

        var download = await BytesAsync(request.Value!, ct).ConfigureAwait(false);

        if (!download.Succeeded) return OperationResult<RemoteContent>.Fail(download.Diagnostics[0]);

        var version = download.Value!.Headers.TryGetValue(ResultHeader, out var result) ? JsonText(result, "rev") : null;

        return OperationResult<RemoteContent>.Ok(new RemoteContent(download.Value!.Bytes, version));
    }

    public override async Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var request = await RequestAsync(connection, HttpMethod.Post, UploadAddress, ct).ConfigureAwait(false);

        if (!request.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(request.Diagnostics[0]);

        var expected = conditions?.ExpectedVersion;

        request.Value!.Headers.Add("Dropbox-API-Arg", UploadArgument(path, expected));

        request.Value!.Content = new ByteArrayContent(content);
        request.Value!.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var reply = await ReplyAsync(request.Value!, ct).ConfigureAwait(false);

        if (!reply.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(reply.Diagnostics[0]);

        if (reply.Value!.Succeeded) return Written(JsonText(reply.Value!.Body, "rev"));

        if (expected is not null && IsConflict(reply.Value!)) return Conflicting();

        return OperationResult<RemoteWriteOutcome>.Fail(Failure(reply.Value!.Status, Protocol));
    }

    private bool IsConflict(CloudReply reply) =>
        reply.Status == System.Net.HttpStatusCode.Conflict
        && JsonText(reply.Body, "error_summary") is { } summary
        && summary.StartsWith("path/conflict", StringComparison.Ordinal);

    private const string ResultHeader = "Dropbox-API-Result";

    public static string Folder(string path) =>
        Trimmed(path) is { Length: > 0 } tail ? "/" + tail : string.Empty;

    public static string Argument(string path) =>
        $$"""{"path": "{{Target(path)}}"}""";

    public static string UploadArgument(string path, string? expectedRevision) =>
        expectedRevision is null
            ? $$"""{"path": "{{Target(path)}}", "mode": "overwrite", "autorename": false, "mute": true}"""
            : $$"""{"path": "{{Target(path)}}", "mode": {".tag": "update", "update": "{{JsonEncodedText.Encode(expectedRevision)}}"}, "autorename": false, "mute": true}""";

    private static string Target(string path) => JsonEncodedText.Encode("/" + Trimmed(path)).ToString();
}
