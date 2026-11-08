using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;

namespace PulseOffice.Remote.Cloud;

public sealed record OAuthEndpoints(string Authorize, string Token);

public sealed record OAuthTokens(string Access, string? Refresh, DateTimeOffset Expires)
{
    public bool IsFreshAt(DateTimeOffset now) => now < Expires.AddMinutes(-2);
}

public sealed record ProofKey(string Verifier, string Challenge);

public interface IProofKeys
{
    ProofKey Create();
}

public sealed class ProofKeys : IProofKeys
{
    public ProofKey Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var verifier = Base64Url(bytes);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        return new ProofKey(verifier, challenge);
    }

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public interface ILoopbackListener
{
    Task<OperationResult<string>> AwaitCodeAsync(string prefix, string state, CancellationToken ct = default);

    int Reserve();
}

public sealed class LoopbackListener : ILoopbackListener
{
    private const string ClosingPage =
        "<html><head><meta charset=\"utf-8\"></head><body style=\"font-family:-apple-system,sans-serif;"
        + "padding:3rem;text-align:center\"><h2>PulseOffice</h2><p>%MESSAGE%</p></body></html>";

    public int Reserve()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    public async Task<OperationResult<string>> AwaitCodeAsync(
        string prefix, string state, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            return OperationResult<string>.Fail(Diagnostic.Error("CLOUD002", ex.Message).With(ex.Message));
        }

        using var registration = ct.Register(listener.Stop);

        try
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            while (!BelongsToSignIn(context.Request.QueryString, state))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }

            var query = context.Request.QueryString;

            var code = query["code"];
            var failure = query["error"];

            await RespondAsync(context, code is not null && failure is null).ConfigureAwait(false);

            if (failure is not null)
                return OperationResult<string>.Fail(Diagnostic.Error("CLOUD003", failure).With(failure));

            return string.IsNullOrEmpty(code)
                ? OperationResult<string>.Fail(Diagnostic.Error("CLOUD005", "No authorization code."))
                : OperationResult<string>.Ok(code);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            return OperationResult<string>.Fail(Diagnostic.Error("CLOUD006", "Sign-in was interrupted."));
        }
    }

    public bool BelongsToSignIn(System.Collections.Specialized.NameValueCollection query, string state)
    {
        ArgumentNullException.ThrowIfNull(query);

        var answered = query["state"];
        if (answered is null || (query["code"] is null && query["error"] is null)) return false;

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(answered), Encoding.UTF8.GetBytes(state));
    }

    private static async Task RespondAsync(HttpListenerContext context, bool succeeded)
    {
        var message = succeeded
            ? "Sign-in complete. You can go back to PulseOffice."
            : "Sign-in failed. You can go back to PulseOffice.";

        var page = Encoding.UTF8.GetBytes(ClosingPage.Replace("%MESSAGE%", message, StringComparison.Ordinal));

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = page.Length;

        await context.Response.OutputStream.WriteAsync(page).ConfigureAwait(false);
        context.Response.Close();
    }
}

public interface IOAuthFlow
{
    OAuthEndpoints EndpointsFor(RemoteProtocol protocol);

    Task<OperationResult<OAuthTokens>> SignInAsync(
        CloudApplication application, CancellationToken ct = default);

    Task<OperationResult<OAuthTokens>> RefreshAsync(
        CloudApplication application, string refreshToken, CancellationToken ct = default);
}

public sealed class OAuthFlow(
    HttpClient client,
    IShellOpener browser,
    ILoopbackListener listener,
    IProofKeys keys,
    TimeProvider clock) : IOAuthFlow
{
    private readonly BoundedResponse _replies = new(BoundedResponse.TextMaximum);
    public OAuthEndpoints EndpointsFor(RemoteProtocol protocol) => protocol switch
    {
        RemoteProtocol.OneDrive => new OAuthEndpoints(
            "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            "https://login.microsoftonline.com/common/oauth2/v2.0/token"),
        RemoteProtocol.GoogleDrive => new OAuthEndpoints(
            "https://accounts.google.com/o/oauth2/v2/auth",
            "https://oauth2.googleapis.com/token"),
        RemoteProtocol.Dropbox => new OAuthEndpoints(
            "https://www.dropbox.com/oauth2/authorize",
            "https://api.dropboxapi.com/oauth2/token"),
        _ => throw new NotSupportedException($"{protocol} does not sign in with OAuth."),
    };

    public async Task<OperationResult<OAuthTokens>> SignInAsync(
        CloudApplication application, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        var endpoints = EndpointsFor(application.Protocol);
        var proof = keys.Create();
        var state = ProofKeys.Base64Url(RandomNumberGenerator.GetBytes(16));

        var port = listener.Reserve();
        var redirect = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/";

        var address = Authorize(endpoints, application, redirect, proof, state);

        using var signIn = CancellationTokenSource.CreateLinkedTokenSource(ct);
        signIn.CancelAfter(TimeSpan.FromMinutes(5));
        var waiting = listener.AwaitCodeAsync(redirect, state, signIn.Token);

        var opened = browser.OpenUrl(address);
        if (!opened.Succeeded)
        {
            await signIn.CancelAsync().ConfigureAwait(false);
            await waiting.ConfigureAwait(false);
            return OperationResult<OAuthTokens>.Fail(opened.Diagnostics[0]);
        }

        var code = await waiting.ConfigureAwait(false);
        if (!code.Succeeded) return OperationResult<OAuthTokens>.Fail(code.Diagnostics[0]);

        return await ExchangeAsync(endpoints, new Dictionary<string, string>
        {
            ["client_id"] = application.ClientId,
            ["code"] = code.Value!,
            ["code_verifier"] = proof.Verifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirect,
        }, ct).ConfigureAwait(false);
    }

    public Task<OperationResult<OAuthTokens>> RefreshAsync(
        CloudApplication application, string refreshToken, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        return ExchangeAsync(EndpointsFor(application.Protocol), new Dictionary<string, string>
        {
            ["client_id"] = application.ClientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }, ct);
    }

    public static string Authorize(
        OAuthEndpoints endpoints, CloudApplication application,
        string redirect, ProofKey proof, string state)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(proof);

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = application.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirect,
            ["code_challenge"] = proof.Challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        };

        if (application.Scopes.Length > 0) parameters["scope"] = application.Scopes;

        if (application.Protocol == RemoteProtocol.GoogleDrive)
        {
            parameters["access_type"] = "offline";
            parameters["prompt"] = "consent";
        }

        if (application.Protocol == RemoteProtocol.Dropbox)
        {
            parameters["token_access_type"] = "offline";
        }

        var query = string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return $"{endpoints.Authorize}?{query}";
    }

    private async Task<OperationResult<OAuthTokens>> ExchangeAsync(
        OAuthEndpoints endpoints, Dictionary<string, string> form, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoints.Token)
        {
            Content = new FormUrlEncodedContent(form),
        };

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var body = await _replies.TextAsync(response.Content, ct).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? ReadTokens(body, clock.GetUtcNow())
                : OperationResult<OAuthTokens>.Fail(Diagnostic.Error("CLOUD007", $"The sign-in service answered {(int)response.StatusCode}.", ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)).With((int)response.StatusCode));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return OperationResult<OAuthTokens>.Fail(Diagnostic.Error("CLOUD008", ex.Message).With(ex.Message));
        }
    }

    public static OperationResult<OAuthTokens> ReadTokens(string body, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return OperationResult<OAuthTokens>.Fail(Diagnostic.Error("CLOUD009", "No access token."));

            var access = root.TryGetProperty("access_token", out var token) ? token.GetString() : null;

            if (string.IsNullOrEmpty(access))
                return OperationResult<OAuthTokens>.Fail(Diagnostic.Error("CLOUD009", "No access token."));

            var refresh = root.TryGetProperty("refresh_token", out var renewal)
                ? renewal.GetString()
                : null;

            var seconds = root.TryGetProperty("expires_in", out var life) && life.TryGetInt32(out var value)
                ? value
                : 3600;

            return OperationResult<OAuthTokens>.Ok(
                new OAuthTokens(access, refresh, now.AddSeconds(seconds)));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return OperationResult<OAuthTokens>.Fail(Diagnostic.Error("CLOUD027", ex.Message).With(ex.Message));
        }
    }
}
