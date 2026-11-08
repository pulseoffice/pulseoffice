using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using PulseOffice.Agent.Protocol;
using PulseOffice.Agent.Security;

namespace PulseOffice.Agent.Transport;

public interface IAgentServer : IAsyncDisposable
{
    bool IsRunning { get; }

    Uri? Address { get; }

    string? Failure { get; }

    void Start(int port);

    Task StopAsync();

    event EventHandler? StateChanged;
}

public sealed class AgentHttpServer(
    IMcpEndpoint endpoint,
    IAgentAccessCode code,
    IAgentSessions sessions,
    ILogger<AgentHttpServer> logger,
    TimeProvider time) : IAgentServer
{
    private const string SessionHeader = "Mcp-Session-Id";
    private const int MaximumBody = 4 * 1024 * 1024;
    private readonly Lock _gate = new();
    private readonly HashSet<Task> _serving = [];
    private HttpListener? _listener;
    private CancellationTokenSource? _stopping;
    private Task? _loop;
    private volatile bool _draining;

    public TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public bool IsRunning => _listener?.IsListening == true;

    public Uri? Address { get; private set; }

    public string? Failure { get; private set; }

    public event EventHandler? StateChanged;

    public void Start(int port)
    {
        lock (_gate)
        {
            if (_listener is not null) return;
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                listener.Close();
                Failure = ex.Message;
                logger.LogWarning(ex, "The agent server could not listen on port {Port}", port);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            _ = code.Current;
            _draining = false;
            Address = new Uri(prefix + "mcp");
            Failure = null;
            var stopping = new CancellationTokenSource();
            _stopping = stopping;
            _listener = listener;
            var token = stopping.Token;
            _loop = Task.Run(() => AcceptAsync(listener, token));
            logger.LogInformation("Agent server listening on {Address}", Address);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync()
    {
        HttpListener? listener;
        Task? loop;
        CancellationTokenSource? stopping;
        lock (_gate)
        {
            listener = _listener;
            loop = _loop;
            stopping = _stopping;
            _listener = null;
            _loop = null;
            _stopping = null;
            Address = null;
            if (listener is not null) _draining = true;
        }

        if (listener is null) return;
        await DrainAsync().ConfigureAwait(false);
        await (stopping?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        listener.Close();
        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException or OperationCanceledException)
            {
                logger.LogDebug(ex, "Agent server loop ended");
            }
        }

        stopping?.Dispose();
        sessions.Clear();
        logger.LogInformation("Agent server stopped");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task DrainAsync()
    {
        Task[] serving;
        lock (_gate) serving = [.. _serving];
        if (serving.Length == 0) return;

        try
        {
            await Task.WhenAll(serving).WaitAsync(DrainTimeout, time).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Agent server stopped with {Count} requests still running after {Timeout}", serving.Count(task => !task.IsCompleted), DrainTimeout);
        }
    }

    private void Track(Task serving)
    {
        lock (_gate) _serving.Add(serving);
        serving.ContinueWith(finished =>
        {
            lock (_gate) _serving.Remove(finished);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task AcceptAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            Track(Task.Run(() => ServeAsync(context, ct), CancellationToken.None));
        }
    }

    private async Task ServeAsync(HttpListenerContext context, CancellationToken ct)
    {
        var response = context.Response;
        try
        {
            if (_draining)
            {
                response.StatusCode = 503;
                return;
            }

            await RespondAsync(context.Request, response, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogWarning(ex, "Agent request failed");
            try
            {
                response.StatusCode = 500;
            }
            catch (InvalidOperationException)
            {
                logger.LogDebug("Agent response already started");
            }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                logger.LogDebug(ex, "Agent response could not be closed");
            }
        }
    }

    private async Task RespondAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
    {
        if (!string.Equals(request.Url?.AbsolutePath.TrimEnd('/'), "/mcp", StringComparison.Ordinal))
        {
            response.StatusCode = 404;
            return;
        }

        if (!IsLocal(request))
        {
            logger.LogWarning("Agent request refused: not local or foreign origin");
            response.StatusCode = 403;
            return;
        }

        if (!Authorized(request))
        {
            logger.LogWarning("Agent request refused: wrong or missing access code");
            response.StatusCode = 401;
            response.AddHeader("WWW-Authenticate", "Bearer realm=\"PulseOffice\"");
            return;
        }

        switch (request.HttpMethod)
        {
            case "POST":
                await PostAsync(request, response, ct).ConfigureAwait(false);
                return;
            case "DELETE":
                response.StatusCode = request.Headers[SessionHeader] is { } id && sessions.End(id) ? 200 : 404;
                return;
            default:
                response.StatusCode = 405;
                response.AddHeader("Allow", "POST, DELETE");
                return;
        }
    }

    private async Task PostAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken ct)
    {
        if (request.ContentLength64 > MaximumBody)
        {
            response.StatusCode = 413;
            return;
        }

        if (await ReadBodyAsync(request.InputStream, ct).ConfigureAwait(false) is not { } body)
        {
            response.StatusCode = 413;
            return;
        }

        var reply = await endpoint.HandleAsync(body, request.Headers[SessionHeader], ct).ConfigureAwait(false);
        response.StatusCode = reply.Status;
        if (reply.SessionId is { } session) response.AddHeader(SessionHeader, session);
        if (reply.Body is not { } text) return;

        var bytes = Encoding.UTF8.GetBytes(text);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    private async Task<string?> ReadBodyAsync(Stream input, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaximumBody) return null;
            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private bool Authorized(HttpListenerRequest request)
    {
        var header = request.Headers["Authorization"];
        const string scheme = "Bearer ";
        return header is not null && header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) && code.Matches(header[scheme.Length..].Trim());
    }

    private static bool IsLocal(HttpListenerRequest request)
    {
        if (!IPAddress.IsLoopback(request.RemoteEndPoint.Address)) return false;
        if (request.Url is not { } url || !(url.IsLoopback || url.Host == "localhost")) return false;
        var origin = request.Headers["Origin"];
        return string.IsNullOrEmpty(origin)
            || (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && (uri.IsLoopback || uri.Host == "localhost"));
    }
}
