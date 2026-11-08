using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;

namespace PulseOffice.Remote;

public sealed class WebDavResponseReader
{
    private readonly XNamespace Dav = "DAV:";
    private readonly WebDavPaths _paths = new();

    public IReadOnlyList<RemoteEntry> ReadListing(string xml, string requestedPath, string basePath = "/")
    {
        ArgumentNullException.ThrowIfNull(xml);

        XDocument document;

        try
        {
            document = Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var entries = new List<RemoteEntry>();

        foreach (var response in document.Descendants(Dav + "response"))
        {
            var href = (string?)response.Element(Dav + "href");
            if (string.IsNullOrEmpty(href)) continue;

            var absolute = _paths.Normalize(Uri.UnescapeDataString(href));

            if (string.Equals(absolute, _paths.Normalize(requestedPath), StringComparison.Ordinal)) continue;

            var path = _paths.Relative(absolute, _paths.Normalize(basePath));
            var properties = new DavProperties(response.Descendants(Dav + "prop").FirstOrDefault(), Dav);

            entries.Add(new RemoteEntry(_paths.NameOf(path), path, properties.IsFolder, properties.Size, properties.Modified));
        }

        return entries;
    }

    public string? ReadLockToken(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        try
        {
            return Parse(xml)
                .Descendants(Dav + "locktoken")
                .Elements(Dav + "href")
                .Select(element => element.Value.Trim())
                .FirstOrDefault(IsUsableToken);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    public bool IsUsableToken(string token) =>
        token.Length is > 0 and <= 1024 && token.All(c => c is > ' ' and < '\u007f' and not '<' and not '>');

    private XDocument Parse(string xml)
    {
        var settings = new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 64L * 1024 * 1024,
        };

        using var reader = System.Xml.XmlReader.Create(new StringReader(xml), settings);
        return XDocument.Load(reader);
    }
}

public sealed class WebDavPaths
{
    public string Relative(string path, string basePath)
    {
        if (basePath.Length <= 1 || !path.StartsWith(basePath, StringComparison.Ordinal)) return path;

        var tail = path[basePath.Length..];
        return tail.Length == 0 ? "/" : tail.StartsWith('/') ? tail : "/" + tail;
    }

    public string NameOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        var separator = trimmed.LastIndexOf('/');
        return separator < 0 ? trimmed : trimmed[(separator + 1)..];
    }

    public string Normalize(string path)
    {
        var value = path.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            ? Uri.UnescapeDataString(absolute.AbsolutePath)
            : path;

        value = value.TrimEnd('/');
        return value.Length == 0 ? "/" : value;
    }

    public string Address(RemoteConnection connection, string path)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var root = connection.Url.TrimEnd('/');
        var tail = (path ?? string.Empty).TrimStart('/');
        var escaped = string.Join('/', tail.Split('/').Select(Uri.EscapeDataString));
        return escaped.Length == 0 ? root + "/" : $"{root}/{escaped}";
    }
}

internal sealed class DavProperties(XElement? properties, XNamespace dav)
{
    public bool IsFolder => properties?.Element(dav + "resourcetype")?.Element(dav + "collection") is not null;

    public long Size =>
        long.TryParse((string?)properties?.Element(dav + "getcontentlength"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
            ? size
            : 0;

    public DateTimeOffset? Modified =>
        DateTimeOffset.TryParse((string?)properties?.Element(dav + "getlastmodified"),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment
            : null;
}

internal sealed class WebDavFailures
{
    private readonly IReadOnlyDictionary<HttpStatusCode, Diagnostic> _known = new Dictionary<HttpStatusCode, Diagnostic>
    {
        [HttpStatusCode.Unauthorized] = Diagnostic.Error("REM010", "Credentials refused."),
        [HttpStatusCode.Forbidden] = Diagnostic.Error("REM011", "Access denied."),
        [HttpStatusCode.NotFound] = Diagnostic.Error("REM012", "Not found on the server."),
        [HttpStatusCode.Locked] = Diagnostic.Error("REM013", "The document is checked out by someone else."),
    };

    public Diagnostic For(HttpStatusCode status) =>
        _known.TryGetValue(status, out var diagnostic) ? diagnostic : Diagnostic.Error("REM001", $"The server answered {(int)status}.", ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture)).With((int)status);
}

public sealed class WebDavVersions
{
    private const string TagPrefix = "etag:";
    private const string ModifiedPrefix = "modified:";

    public string? Of(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Headers.ETag is { IsWeak: false } tag) return TagPrefix + tag.Tag;

        return response.Content.Headers.LastModified is { } modified
            ? ModifiedPrefix + modified.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture)
            : null;
    }

    public void Require(HttpRequestMessage request, string? version)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(version)) return;

        if (version.StartsWith(TagPrefix, StringComparison.Ordinal))
            request.Headers.TryAddWithoutValidation("If-Match", version[TagPrefix.Length..]);
        else if (version.StartsWith(ModifiedPrefix, StringComparison.Ordinal)
                 && DateTimeOffset.TryParseExact(version[ModifiedPrefix.Length..], "R", CultureInfo.InvariantCulture,
                     DateTimeStyles.AssumeUniversal, out var modified))
            request.Headers.IfUnmodifiedSince = modified;
    }
}

public sealed class WebDavStorage(
    HttpClient client,
    ISecretVaultProvider vaults,
    WebDavResponseReader reader) : IRemoteStorage
{
    private readonly WebDavPaths _paths = new();
    private readonly WebDavFailures _failures = new();
    private readonly BoundedResponse _downloads = new();
    private readonly BoundedResponse _replies = new(BoundedResponse.TextMaximum);
    private readonly WebDavVersions _versions = new();

    private const string PropertyRequest =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <D:propfind xmlns:D="DAV:"><D:prop>
          <D:resourcetype/><D:getcontentlength/><D:getlastmodified/>
        </D:prop></D:propfind>
        """;

    private const string LockRequest =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <D:lockinfo xmlns:D="DAV:">
          <D:lockscope><D:exclusive/></D:lockscope>
          <D:locktype><D:write/></D:locktype>
          <D:owner><D:href>PulseOffice</D:href></D:owner>
        </D:lockinfo>
        """;

    public RemoteProtocol Protocol => RemoteProtocol.WebDav;

    public async Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        if (Unreachable(connection) is { } invalid) return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(invalid);

        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), _paths.Address(connection, path))
        {
            Content = new StringContent(PropertyRequest, Encoding.UTF8, "application/xml"),
        };

        request.Headers.Add("Depth", "1");

        var response = await SendAsync(connection, request, ct).ConfigureAwait(false);

        if (!response.Succeeded)
            return OperationResult<IReadOnlyList<RemoteEntry>>.Fail(response.Diagnostics[0]);

        var root = new Uri(connection.Url.TrimEnd('/') + "/").AbsolutePath;

        return OperationResult<IReadOnlyList<RemoteEntry>>.Ok(
            reader.ReadListing(response.Value!.Body, new Uri(_paths.Address(connection, path)).AbsolutePath, root));
    }

    public async Task<OperationResult<RemoteContent>> ReadAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        if (Unreachable(connection) is { } invalid) return OperationResult<RemoteContent>.Fail(invalid);

        using var request = new HttpRequestMessage(HttpMethod.Get, _paths.Address(connection, path));

        await AuthorizeAsync(connection, request, ct).ConfigureAwait(false);

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return OperationResult<RemoteContent>.Fail(_failures.For(response.StatusCode));

            var version = _versions.Of(response);

            return OperationResult<RemoteContent>.Ok(new RemoteContent(
                await _downloads.BytesAsync(response.Content, ct).ConfigureAwait(false), version));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return OperationResult<RemoteContent>.Fail(Diagnostic.Error("REM002", ex.Message).With(ex.Message));
        }
    }

    public async Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions,
        CancellationToken ct = default)
    {
        if (Unreachable(connection) is { } invalid) return OperationResult<RemoteWriteOutcome>.Fail(invalid);

        ArgumentNullException.ThrowIfNull(content);

        using var request = new HttpRequestMessage(HttpMethod.Put, _paths.Address(connection, path))
        {
            Content = new ByteArrayContent(content),
        };

        if (conditions?.Held is { } held) request.Headers.Add("If", $"(<{held.Token}>)");
        _versions.Require(request, conditions?.ExpectedVersion);

        var response = await ExchangeAsync(connection, request, ct).ConfigureAwait(false);

        if (!response.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(response.Diagnostics[0]);

        var reply = response.Value!;

        if (reply.Status == HttpStatusCode.PreconditionFailed && conditions?.ExpectedVersion is not null)
            return OperationResult<RemoteWriteOutcome>.Ok(new RemoteWriteOutcome(RemoteWriteStatus.Conflict, null));

        if (!reply.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(_failures.For(reply.Status));

        var version = reply.Version ?? await CurrentVersionAsync(connection, path, ct).ConfigureAwait(false);

        return OperationResult<RemoteWriteOutcome>.Ok(new RemoteWriteOutcome(RemoteWriteStatus.Written, version));
    }

    private async Task<string?> CurrentVersionAsync(RemoteConnection connection, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, _paths.Address(connection, path));

        var response = await ExchangeAsync(connection, request, ct).ConfigureAwait(false);

        return response is { Succeeded: true, Value: { Succeeded: true } reply } ? reply.Version : null;
    }

    public async Task<OperationResult<RemoteLock>> LockAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        if (Unreachable(connection) is { } invalid) return OperationResult<RemoteLock>.Fail(invalid);

        using var request = new HttpRequestMessage(new HttpMethod("LOCK"), _paths.Address(connection, path))
        {
            Content = new StringContent(LockRequest, Encoding.UTF8, "application/xml"),
        };

        request.Headers.Add("Timeout", "Second-3600");

        var response = await SendAsync(connection, request, ct).ConfigureAwait(false);

        if (!response.Succeeded) return OperationResult<RemoteLock>.Fail(response.Diagnostics[0]);

        if (reader.ReadLockToken(response.Value!.Body) is not { } token)
            return OperationResult<RemoteLock>.Fail(Diagnostic.Error("REM003", "The server granted no lock token."));

        return OperationResult<RemoteLock>.Ok(new RemoteLock(token, path));
    }

    public async Task<OperationResult<bool>> UnlockAsync(
        RemoteConnection connection, RemoteLock held, CancellationToken ct = default)
    {
        if (Unreachable(connection) is { } invalid) return OperationResult<bool>.Fail(invalid);

        ArgumentNullException.ThrowIfNull(held);

        using var request = new HttpRequestMessage(new HttpMethod("UNLOCK"), _paths.Address(connection, held.Path));

        request.Headers.Add("Lock-Token", $"<{held.Token}>");

        var response = await SendAsync(connection, request, ct).ConfigureAwait(false);

        return response.Succeeded
            ? OperationResult<bool>.Ok(true)
            : OperationResult<bool>.Fail(response.Diagnostics[0]);
    }

    private async Task<OperationResult<WebDavReply>> SendAsync(
        RemoteConnection connection, HttpRequestMessage request, CancellationToken ct)
    {
        var response = await ExchangeAsync(connection, request, ct).ConfigureAwait(false);

        if (!response.Succeeded) return response;

        return response.Value!.Succeeded
            ? response
            : OperationResult<WebDavReply>.Fail(_failures.For(response.Value!.Status));
    }

    private async Task<OperationResult<WebDavReply>> ExchangeAsync(
        RemoteConnection connection, HttpRequestMessage request, CancellationToken ct)
    {
        await AuthorizeAsync(connection, request, ct).ConfigureAwait(false);

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            var body = await _replies.TextAsync(response.Content, ct).ConfigureAwait(false);

            return OperationResult<WebDavReply>.Ok(new WebDavReply(
                response.StatusCode, response.IsSuccessStatusCode, body, _versions.Of(response)));
        }
        catch (HttpRequestException ex)
        {
            return OperationResult<WebDavReply>.Fail(Diagnostic.Error("REM002", ex.Message).With(ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            return OperationResult<WebDavReply>.Fail(Diagnostic.Error("REM004", ex.Message).With(ex.Message));
        }
    }

    private static Diagnostic? Unreachable(RemoteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return Uri.TryCreate(connection.Url, UriKind.Absolute, out var address)
               && address.Scheme is "http" or "https"
            ? null
            : Diagnostic.Error("REM005", "The storage address is not a valid http or https URL.");
    }

    private async Task AuthorizeAsync(
        RemoteConnection connection, HttpRequestMessage request, CancellationToken ct)
    {
        if (connection.User.Length == 0) return;
        if (request.RequestUri is { Scheme: "http", IsLoopback: false }) return;

        var password = await vaults.Current
            .ReadAsync("PulseOffice", connection.SecretKey, ct).ConfigureAwait(false) ?? string.Empty;

        var pair = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{connection.User}:{password}"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", pair);
    }

    private sealed record WebDavReply(HttpStatusCode Status, bool Succeeded, string Body, string? Version);
}
