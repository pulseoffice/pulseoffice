using System.Globalization;
using System.Text;
using System.Text.Json;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Remote.Cloud;

public sealed record DriveItem(string Id, string Name, bool IsFolder, long Size, DateTimeOffset? Modified)
{
    public string? Revision { get; init; }

    public string? Version => Revision ?? Modified?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}

public sealed class GoogleDriveReader
{
    public IReadOnlyList<DriveItem> ReadListing(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];

            if (!document.RootElement.TryGetProperty("files", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return [];

            var entries = new List<DriveItem>();

            foreach (var item in items.EnumerateArray())
                if (ReadItem(item) is { } entry) entries.Add(entry);

            return entries;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public string? ReadId(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string? ReadVersion(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadItem(document.RootElement)?.Version : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private DriveItem? ReadItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;

        var id = item.TryGetProperty("id", out var declared) && declared.ValueKind == JsonValueKind.String ? declared.GetString() : null;
        var name = item.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) return null;

        var mime = item.TryGetProperty("mimeType", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;

        var size = item.TryGetProperty("size", out var measured) && measured.ValueKind == JsonValueKind.String
            && long.TryParse(measured.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes)
            ? bytes
            : 0;

        var modified = item.TryGetProperty("modifiedTime", out var moment) && moment.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(moment.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
            ? when
            : (DateTimeOffset?)null;

        var revision = item.TryGetProperty("headRevisionId", out var head) && head.ValueKind == JsonValueKind.String
            ? head.GetString()
            : null;

        return new DriveItem(id, name, string.Equals(mime, FolderType, StringComparison.Ordinal), size, modified)
        {
            Revision = revision,
        };
    }

    public const string FolderType = "application/vnd.google-apps.folder";
}

public sealed class GoogleDriveStorage(
    HttpClient client, ICloudSessions sessions, GoogleDriveReader reader)
    : CloudStorage(client, sessions)
{
    private const string Files = "https://www.googleapis.com/drive/v3/files";
    private const string Upload = "https://www.googleapis.com/upload/drive/v3/files";
    private const string Fields = "nextPageToken,files(id,name,mimeType,size,modifiedTime,headRevisionId)";

    public override RemoteProtocol Protocol => RemoteProtocol.GoogleDrive;

    public override async Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var folder = await FolderIdAsync(connection, path, ct).ConfigureAwait(false);

        if (!folder.Succeeded)
            return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(folder.Diagnostics[0]);

        var children = await ChildrenAsync(connection, folder.Value!, ct).ConfigureAwait(false);

        if (!children.Succeeded)
            return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(children.Diagnostics[0]);

        return OperationResult<IReadOnlyList<RemoteEntry>>.Ok(
        [
            .. children.Value!.Select(item => new RemoteEntry(
                item.Name, CloudPaths.Join(path, item.Name), item.IsFolder, item.Size, item.Modified)),
        ]);
    }

    public override async Task<OperationResult<RemoteContent>> ReadAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var file = await FileAsync(connection, path, ct).ConfigureAwait(false);

        if (!file.Succeeded) return OperationResult<RemoteContent>.Fail(file.Diagnostics[0]);

        var request = await RequestAsync(
            connection, HttpMethod.Get, $"{Files}/{file.Value!.Id}?alt=media", ct).ConfigureAwait(false);

        if (!request.Succeeded) return OperationResult<RemoteContent>.Fail(request.Diagnostics[0]);

        var download = await BytesAsync(request.Value!, ct).ConfigureAwait(false);

        return download.Succeeded
            ? OperationResult<RemoteContent>.Ok(new RemoteContent(download.Value!.Bytes, file.Value!.Version))
            : OperationResult<RemoteContent>.Fail(download.Diagnostics[0]);
    }

    public override async Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var file = await FileAsync(connection, path, ct).ConfigureAwait(false);

        if (!file.Succeeded && file.Diagnostics[0].Code != NotFound)
            return OperationResult<RemoteWriteOutcome>.Fail(file.Diagnostics[0]);

        if (file.Succeeded && conditions?.ExpectedVersion is { } expected
            && !string.Equals(file.Value!.Version, expected, StringComparison.Ordinal))
            return Conflicting();

        var id = file.Succeeded
            ? file.Value!.Id
            : (await CreateAsync(connection, path, ct).ConfigureAwait(false)) is { Succeeded: true } made
                ? made.Value
                : null;

        if (id is null)
            return OperationResult<RemoteWriteOutcome>.Fail(Diagnostic.Error("CLOUD017", "The file could not be created in Google Drive."));

        var request = await RequestAsync(
            connection, HttpMethod.Patch,
            $"{Upload}/{id}?uploadType=media&fields={Uri.EscapeDataString(UploadFields)}", ct).ConfigureAwait(false);

        if (!request.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(request.Diagnostics[0]);

        request.Value!.Content = new ByteArrayContent(content);

        var body = await TextAsync(request.Value!, ct).ConfigureAwait(false);

        return body.Succeeded
            ? Written(reader.ReadVersion(body.Value!))
            : OperationResult<RemoteWriteOutcome>.Fail(body.Diagnostics[0]);
    }

    private const string UploadFields = "id,name,mimeType,size,modifiedTime,headRevisionId";

    private async Task<OperationResult<string>> CreateAsync(
        RemoteConnection connection, string path, CancellationToken ct)
    {
        var parent = await FolderIdAsync(connection, CloudPaths.ParentOf(path), ct).ConfigureAwait(false);

        if (!parent.Succeeded) return OperationResult<string>.Fail(parent.Diagnostics[0]);

        var request = await RequestAsync(connection, HttpMethod.Post, Files, ct).ConfigureAwait(false);

        if (!request.Succeeded) return OperationResult<string>.Fail(request.Diagnostics[0]);

        var name = JsonEncodedText.Encode(CloudPaths.NameOf(path)).ToString();

        request.Value!.Content = new StringContent(
            $$"""{"name": "{{name}}", "parents": ["{{parent.Value}}"]}""",
            Encoding.UTF8, "application/json");

        var body = await TextAsync(request.Value!, ct).ConfigureAwait(false);

        if (!body.Succeeded) return OperationResult<string>.Fail(body.Diagnostics[0]);

        return reader.ReadId(body.Value!) is { Length: > 0 } id
            ? OperationResult<string>.Ok(id)
            : OperationResult<string>.Fail(Diagnostic.Error("CLOUD019", "Google Drive returned no file id."));
    }

    private async Task<OperationResult<string>> FolderIdAsync(
        RemoteConnection connection, string path, CancellationToken ct)
    {
        var current = "root";

        foreach (var segment in Segments(path))
        {
            var children = await ChildrenAsync(connection, current, ct).ConfigureAwait(false);

            if (!children.Succeeded) return OperationResult<string>.Fail(children.Diagnostics[0]);

            var match = Named(children.Value!.Where(item => item.IsFolder), segment);

            if (match is null)
                return OperationResult<string>.Fail(Diagnostic.Error("CLOUD018", $"The folder '{segment}' does not exist in Google Drive.", segment).With(segment));

            current = match.Id;
        }

        return OperationResult<string>.Ok(current);
    }

    private async Task<OperationResult<DriveItem>> FileAsync(
        RemoteConnection connection, string path, CancellationToken ct)
    {
        var parent = await FolderIdAsync(connection, CloudPaths.ParentOf(path), ct).ConfigureAwait(false);

        if (!parent.Succeeded) return OperationResult<DriveItem>.Fail(parent.Diagnostics[0]);

        var children = await ChildrenAsync(connection, parent.Value!, ct).ConfigureAwait(false);

        if (!children.Succeeded) return OperationResult<DriveItem>.Fail(children.Diagnostics[0]);

        var name = CloudPaths.NameOf(path);

        var match = Named(children.Value!.Where(item => !item.IsFolder), name);

        return match is null
            ? OperationResult<DriveItem>.Fail(Diagnostic.Error(NotFound, "Not found in the cloud storage."))
            : OperationResult<DriveItem>.Ok(match);
    }

    private const string NotFound = "CLOUD015";

    private DriveItem? Named(IEnumerable<DriveItem> items, string name)
    {
        var candidates = items.ToList();

        return candidates.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))
            ?? candidates.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<OperationResult<IReadOnlyList<DriveItem>>> ChildrenAsync(
        RemoteConnection connection, string parentId, CancellationToken ct)
    {
        var items = new List<DriveItem>();
        string? page = null;
        var pages = 0;

        do
        {
            if (pages++ >= MaximumPages) return OperationResult<IReadOnlyList<DriveItem>>.Fail(Diagnostic.Error("CLOUD032", "The cloud storage kept sending more pages."));
            var address = page is null ? Query(parentId) : $"{Query(parentId)}&pageToken={Uri.EscapeDataString(page)}";
            var request = await RequestAsync(connection, HttpMethod.Get, address, ct).ConfigureAwait(false);

            if (!request.Succeeded)
                return OperationResult<IReadOnlyList<DriveItem>>.Fail(request.Diagnostics[0]);

            var body = await TextAsync(request.Value!, ct).ConfigureAwait(false);
            if (!body.Succeeded) return OperationResult<IReadOnlyList<DriveItem>>.Fail(body.Diagnostics[0]);

            items.AddRange(reader.ReadListing(body.Value!));
            page = NextPage(body.Value!);
        }
        while (page is not null);

        return OperationResult<IReadOnlyList<DriveItem>>.Ok(items);
    }

    private string? NextPage(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("nextPageToken", out var token)
                && token.ValueKind == System.Text.Json.JsonValueKind.String
                && token.GetString() is { Length: > 0 } value ? value : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static string Query(string parentId) =>
        $"{Files}?q={Uri.EscapeDataString($"'{parentId}' in parents and trashed = false")}"
        + $"&fields={Uri.EscapeDataString(Fields)}&pageSize=200";

    public static IReadOnlyList<string> Segments(string path) =>
        [.. (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries)];
}
