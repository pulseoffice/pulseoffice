using System.Globalization;
using System.Text.Json;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Remote.Cloud;

public sealed class OneDriveReader
{
    public IReadOnlyList<RemoteEntry> ReadListing(string json, string parentPath)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];

            if (!document.RootElement.TryGetProperty("value", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return [];

            var entries = new List<RemoteEntry>();

            foreach (var item in items.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var declared) ? declared.GetString() : null;
                if (string.IsNullOrEmpty(name)) continue;

                var isFolder = item.TryGetProperty("folder", out _);

                var size = item.TryGetProperty("size", out var measured) && measured.TryGetInt64(out var bytes)
                    ? bytes
                    : 0;

                var modified = item.TryGetProperty("lastModifiedDateTime", out var moment)
                    && DateTimeOffset.TryParse(moment.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var when)
                    ? when
                    : (DateTimeOffset?)null;

                entries.Add(new RemoteEntry(name, CloudPaths.Join(parentPath, name), isFolder, size, modified));
            }

            return entries;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public static class CloudPaths
{
    public static string Join(string parent, string name)
    {
        var head = (parent ?? string.Empty).Trim('/');
        var tail = (name ?? string.Empty).Trim('/');

        return head.Length == 0 ? "/" + tail : $"/{head}/{tail}";
    }

    public static string Escaped(string path) =>
        string.Join('/', (path ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

    public static string NameOf(string path) =>
        (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
            ? parts[^1]
            : string.Empty;

    public static string ParentOf(string path)
    {
        var parts = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length <= 1 ? "/" : "/" + string.Join('/', parts[..^1]);
    }
}

public sealed class OneDriveStorage(
    HttpClient client, ICloudSessions sessions, OneDriveReader reader)
    : CloudStorage(client, sessions)
{
    private const string Root = "https://graph.microsoft.com/v1.0/me/drive/root";

    public override RemoteProtocol Protocol => RemoteProtocol.OneDrive;

    public override async Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var entries = new List<RemoteEntry>();
        string? address = Children(path);

        for (var pages = 0; address is not null; pages++)
        {
            if (pages >= MaximumPages) return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(Diagnostic.Error("CLOUD032", "The cloud storage kept sending more pages."));

            var request = await RequestAsync(connection, HttpMethod.Get, address, ct).ConfigureAwait(false);

            if (!request.Succeeded)
                return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(request.Diagnostics[0]);

            var body = await TextAsync(request.Value!, ct).ConfigureAwait(false);
            if (!body.Succeeded) return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(body.Diagnostics[0]);

            entries.AddRange(reader.ReadListing(body.Value!, path));
            address = JsonText(body.Value!, "@odata.nextLink") is { } next
                && next.StartsWith("https://graph.microsoft.com/", StringComparison.Ordinal) ? next : null;
        }

        return OperationResult<IReadOnlyList<RemoteEntry>>.Ok(entries);
    }

    public override async Task<OperationResult<RemoteContent>> ReadAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var item = await RequestAsync(connection, HttpMethod.Get, Item(path), ct).ConfigureAwait(false);

        if (!item.Succeeded) return OperationResult<RemoteContent>.Fail(item.Diagnostics[0]);

        var metadata = await TextAsync(item.Value!, ct).ConfigureAwait(false);

        if (!metadata.Succeeded) return OperationResult<RemoteContent>.Fail(metadata.Diagnostics[0]);

        var request = await RequestAsync(connection, HttpMethod.Get, Content(path), ct).ConfigureAwait(false);

        if (!request.Succeeded) return OperationResult<RemoteContent>.Fail(request.Diagnostics[0]);

        var download = await BytesAsync(request.Value!, ct).ConfigureAwait(false);

        return download.Succeeded
            ? OperationResult<RemoteContent>.Ok(new RemoteContent(download.Value!.Bytes, JsonText(metadata.Value!, "eTag")))
            : OperationResult<RemoteContent>.Fail(download.Diagnostics[0]);
    }

    public override async Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var request = await RequestAsync(connection, HttpMethod.Put, Content(path), ct).ConfigureAwait(false);

        if (!request.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(request.Diagnostics[0]);

        var expected = conditions?.ExpectedVersion;

        if (expected is not null) request.Value!.Headers.TryAddWithoutValidation("If-Match", Quoted(expected));

        request.Value!.Content = new ByteArrayContent(content);

        var reply = await ReplyAsync(request.Value!, ct).ConfigureAwait(false);

        if (!reply.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(reply.Diagnostics[0]);

        if (reply.Value!.Succeeded) return Written(JsonText(reply.Value!.Body, "eTag"));

        if (expected is not null && reply.Value!.Status == System.Net.HttpStatusCode.PreconditionFailed) return Conflicting();

        return OperationResult<RemoteWriteOutcome>.Fail(Failure(reply.Value!.Status, Protocol));
    }

    private string Quoted(string tag) =>
        tag.StartsWith('"') || tag.StartsWith("W/", StringComparison.Ordinal) ? tag : $"\"{tag}\"";

    public static string Item(string path) => $"{Root}:/{CloudPaths.Escaped(path)}";

    public static string Children(string path) =>
        CloudPaths.Escaped(path) is { Length: > 0 } tail
            ? $"{Root}:/{tail}:/children"
            : $"{Root}/children";

    public static string Content(string path) => $"{Root}:/{CloudPaths.Escaped(path)}:/content";
}
