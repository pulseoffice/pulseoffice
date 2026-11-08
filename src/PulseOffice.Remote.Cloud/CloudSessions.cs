using System.Collections.Concurrent;
using System.Net.Http.Headers;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;

namespace PulseOffice.Remote.Cloud;

public interface ICloudSessions : IRemoteSignIn
{
    Task<OperationResult<string>> AccessTokenAsync(RemoteConnection connection, CancellationToken ct = default);
}

public sealed class CloudSessions(
    ICloudApplications applications,
    IOAuthFlow flow,
    ISecretVaultProvider vaults,
    TimeProvider clock) : ICloudSessions
{
    private readonly ConcurrentDictionary<string, OAuthTokens> _live = new(StringComparer.Ordinal);

    public bool Handles(RemoteProtocol protocol) => protocol.NeedsSignIn();

    public bool IsSignedIn(RemoteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return _live.ContainsKey(connection.Name);
    }

    public async Task<OperationResult<bool>> SignInAsync(
        RemoteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var application = applications.For(connection.Protocol);
        if (!application.Succeeded) return OperationResult<bool>.Fail(application.Diagnostics[0]);

        var tokens = await flow.SignInAsync(application.Value!, ct).ConfigureAwait(false);
        if (!tokens.Succeeded) return OperationResult<bool>.Fail(tokens.Diagnostics[0]);

        await KeepAsync(connection, tokens.Value!, ct).ConfigureAwait(false);

        return OperationResult<bool>.Ok(true);
    }

    public async Task SignOutAsync(RemoteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _live.TryRemove(connection.Name, out _);

        await vaults.Current.RemoveAsync("PulseOffice", connection.TokenKey, ct).ConfigureAwait(false);
    }

    public async Task<OperationResult<string>> AccessTokenAsync(
        RemoteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (_live.TryGetValue(connection.Name, out var cached) && cached.IsFreshAt(clock.GetUtcNow()))
            return OperationResult<string>.Ok(cached.Access);

        await _renewal.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await RenewAsync(connection, ct).ConfigureAwait(false);
        }
        finally
        {
            _renewal.Release();
        }
    }

    private readonly SemaphoreSlim _renewal = new(1, 1);

    private async Task<OperationResult<string>> RenewAsync(RemoteConnection connection, CancellationToken ct)
    {
        if (_live.TryGetValue(connection.Name, out var live) && live.IsFreshAt(clock.GetUtcNow()))
            return OperationResult<string>.Ok(live.Access);

        var application = applications.For(connection.Protocol);
        if (!application.Succeeded) return OperationResult<string>.Fail(application.Diagnostics[0]);

        var refresh = live?.Refresh
            ?? await vaults.Current.ReadAsync("PulseOffice", connection.TokenKey, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(refresh))
            return OperationResult<string>.Fail(Diagnostic.Error("CLOUD010", "This connection is not signed in."));

        var renewed = await flow.RefreshAsync(application.Value!, refresh, ct).ConfigureAwait(false);

        if (!renewed.Succeeded) return OperationResult<string>.Fail(renewed.Diagnostics[0]);

        await KeepAsync(connection, renewed.Value! with
        {
            Refresh = renewed.Value!.Refresh ?? refresh,
        }, ct).ConfigureAwait(false);

        return OperationResult<string>.Ok(renewed.Value!.Access);
    }

    private async Task KeepAsync(RemoteConnection connection, OAuthTokens tokens, CancellationToken ct)
    {
        _live[connection.Name] = tokens;

        if (tokens.Refresh is { Length: > 0 } refresh)
            await vaults.Current.WriteAsync("PulseOffice", connection.TokenKey, refresh, ct)
                .ConfigureAwait(false);
    }
}

public sealed record CloudReply(System.Net.HttpStatusCode Status, bool Succeeded, string Body);

public sealed record CloudDownload(byte[] Bytes, IReadOnlyDictionary<string, string> Headers);

public abstract class CloudStorage(HttpClient client, ICloudSessions sessions) : IRemoteStorage
{
    private readonly BoundedResponse _downloads = new();
    private readonly BoundedResponse _replies = new(BoundedResponse.TextMaximum);
    public abstract RemoteProtocol Protocol { get; }

    protected const int MaximumPages = 1000;

    public bool SupportsCheckOut => false;

    protected HttpClient Client => client;

    protected string? JsonText(string json, string name)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    protected bool JsonFlag(string json, string name)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public abstract Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(
        RemoteConnection connection, string path, CancellationToken ct = default);

    public abstract Task<OperationResult<RemoteContent>> ReadAsync(
        RemoteConnection connection, string path, CancellationToken ct = default);

    public abstract Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions,
        CancellationToken ct = default);

    public Task<OperationResult<RemoteLock>> LockAsync(
        RemoteConnection connection, string path, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<RemoteLock>.Fail(Diagnostic.Error("CLOUD030", $"{Protocol} has no check-out: the service does not lock files for third parties.").With(Protocol)));

    public Task<OperationResult<bool>> UnlockAsync(
        RemoteConnection connection, RemoteLock held, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<bool>.Fail(Diagnostic.Error("CLOUD030", $"{Protocol} has no check-out: the service does not lock files for third parties.").With(Protocol)));

    protected async Task<OperationResult<HttpRequestMessage>> RequestAsync(
        RemoteConnection connection, HttpMethod method, string address, CancellationToken ct)
    {
        var token = await sessions.AccessTokenAsync(connection, ct).ConfigureAwait(false);

        if (!token.Succeeded)
            return OperationResult<HttpRequestMessage>.Fail(token.Diagnostics[0]);

        var request = new HttpRequestMessage(method, address);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value!);

        return OperationResult<HttpRequestMessage>.Ok(request);
    }

    protected async Task<OperationResult<string>> TextAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var reply = await ReplyAsync(request, ct).ConfigureAwait(false);

        if (!reply.Succeeded) return OperationResult<string>.Fail(reply.Diagnostics[0]);

        return reply.Value!.Succeeded
            ? OperationResult<string>.Ok(reply.Value!.Body)
            : OperationResult<string>.Fail(Failure(reply.Value!.Status, Protocol));
    }

    protected async Task<OperationResult<CloudReply>> ReplyAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using (request)
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                var body = await _replies.TextAsync(response.Content, ct).ConfigureAwait(false);

                return OperationResult<CloudReply>.Ok(new CloudReply(response.StatusCode, response.IsSuccessStatusCode, body));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return OperationResult<CloudReply>.Fail(Diagnostic.Error("CLOUD012", ex.Message).With(ex.Message));
        }
    }

    protected async Task<OperationResult<CloudDownload>> BytesAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using (request)
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                    return OperationResult<CloudDownload>.Fail(Failure(response.StatusCode, Protocol));

                var headers = response.Headers.ToDictionary(
                    header => header.Key, header => string.Join(',', header.Value), StringComparer.OrdinalIgnoreCase);

                return OperationResult<CloudDownload>.Ok(new CloudDownload(
                    await _downloads.BytesAsync(response.Content, ct).ConfigureAwait(false), headers));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return OperationResult<CloudDownload>.Fail(Diagnostic.Error("CLOUD012", ex.Message).With(ex.Message));
        }
    }

    protected OperationResult<RemoteWriteOutcome> Written(string? version) =>
        OperationResult<RemoteWriteOutcome>.Ok(new RemoteWriteOutcome(RemoteWriteStatus.Written, version));

    protected OperationResult<RemoteWriteOutcome> Conflicting() =>
        OperationResult<RemoteWriteOutcome>.Ok(new RemoteWriteOutcome(RemoteWriteStatus.Conflict, null));

    public static Diagnostic Failure(System.Net.HttpStatusCode status, RemoteProtocol protocol) => status switch
    {
        System.Net.HttpStatusCode.Unauthorized => Diagnostic.Error("CLOUD013", $"{protocol} refused the sign-in: connect again.").With(protocol),
        System.Net.HttpStatusCode.Forbidden => Diagnostic.Error("CLOUD014", "Access denied."),
        System.Net.HttpStatusCode.NotFound => Diagnostic.Error("CLOUD015", "Not found in the cloud storage."),
        System.Net.HttpStatusCode.Conflict => Diagnostic.Error("CLOUD016", "The file changed in the cloud."),
        _ => Diagnostic.Error("CLOUD011", $"{protocol} answered {(int)status}.", ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture)).With(protocol, (int)status),
    };

    protected static string Trimmed(string? path) => (path ?? string.Empty).Trim('/');
}
