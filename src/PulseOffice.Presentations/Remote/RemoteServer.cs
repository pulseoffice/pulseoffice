using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PulseOffice.Presentations.Remote;

public interface IRemoteTarget
{
    int Position { get; }

    int Count { get; }

    string Title { get; }

    string Notes { get; }

    void Next();

    void Previous();

    void First();

    void Last();
}

public interface IRemoteSession
{
    IRemoteTarget? Target { get; set; }
}

public sealed class RemoteSession : IRemoteSession
{
    public IRemoteTarget? Target { get; set; }
}

public sealed record RemoteEndpoint(string Address, int Port, string Pin)
{
    public string Url => $"http://{Address}:{Port.ToString(CultureInfo.InvariantCulture)}/?pin={Pin}";
}

public interface IRemoteServer
{
    RemoteEndpoint? Endpoint { get; }

    RemoteEndpoint Start(IPAddress? bind = null);

    void Stop();
}

public enum RemoteLoginNotice
{
    None,
    WrongPin,
    TooManyAttempts,
}

public interface IRemotePage
{
    string Login(RemoteLoginNotice notice, TimeSpan retryAfter);

    string Controller(string pin);
}

public sealed class RemoteServer(IRemoteSession session, Func<Action, Task> onUi, IRemotePage page, IRemoteAttemptThrottle throttle) : IRemoteServer, IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _stop;

    public RemoteEndpoint? Endpoint { get; private set; }

    public RemoteEndpoint Start(IPAddress? bind = null)
    {
        if (Endpoint is { } running) return running;

        var address = bind ?? IPAddress.Parse(LocalAddress());
        _listener = new TcpListener(address, 0);
        _listener.Start();
        _stop?.Dispose();
        _stop = new CancellationTokenSource();
        throttle.Reset();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var shown = address.ToString();
        Endpoint = new RemoteEndpoint(shown, port, RandomNumberGenerator.GetInt32(0, 10000).ToString("D4", CultureInfo.InvariantCulture));
        _ = AcceptAsync(_listener, _stop.Token);
        return Endpoint;
    }

    public void Stop()
    {
        _stop?.Cancel();
        _listener?.Stop();
        _listener = null;
        Endpoint = null;
    }

    public void Dispose()
    {
        Stop();
        _stop?.Dispose();
    }

    private static string LocalAddress() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))?.ToString()
        ?? IPAddress.Loopback.ToString();

    private async Task AcceptAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            if (!_slots.Wait(0))
            {
                client.Dispose();
                continue;
            }

            _ = ServeLimitedAsync(client, ct);
        }
    }

    private const int MaxLine = 8192;
    private readonly SemaphoreSlim _slots = new(16, 16);

    private async Task ServeLimitedAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await ServeAsync(client, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    private async Task<string?> LineAsync(Stream stream, CancellationToken ct)
    {
        var line = new StringBuilder();
        var buffer = new byte[1];

        while (await stream.ReadAsync(buffer, ct).ConfigureAwait(false) == 1)
        {
            if (buffer[0] == (byte)'\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= MaxLine) throw new IOException("Request line too long.");
            line.Append((char)buffer[0]);
        }

        return line.Length == 0 ? null : line.ToString();
    }

    private async Task ServeAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var line = await LineAsync(stream, ct).ConfigureAwait(false);
                if (line is null) return;
                for (var headers = 0; headers < 100 && !string.IsNullOrEmpty(await LineAsync(stream, ct).ConfigureAwait(false)); headers++) { }

                var parts = line.Split(' ');
                if (parts.Length < 2) return;
                var address = (client.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;
                var (status, type, body, retry) = await RespondAsync(parts[0], parts[1], address).ConfigureAwait(false);
                var payload = Encoding.UTF8.GetBytes(body);
                var retryHeader = retry > TimeSpan.Zero ? $"Retry-After: {Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture)}\r\n" : string.Empty;
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {status}\r\nContent-Type: {type}; charset=utf-8\r\nContent-Length: {payload.Length}\r\n{retryHeader}Cache-Control: no-store\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, ct).ConfigureAwait(false);
                await stream.WriteAsync(payload, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException)
            {
            }
        }
    }

    private async Task<(string Status, string Type, string Body, TimeSpan Retry)> RespondAsync(string method, string target, IPAddress client)
    {
        var path = target.Split('?')[0];
        var query = target.Contains('?', StringComparison.Ordinal) ? target[(target.IndexOf('?', StringComparison.Ordinal) + 1)..] : string.Empty;
        var pin = query.Split('&').Select(pair => pair.Split('=')).Where(pair => pair.Length == 2 && pair[0] == "pin").Select(pair => pair[1]).FirstOrDefault();

        if (pin is not null)
        {
            var wait = throttle.Wait(client);
            if (wait > TimeSpan.Zero)
                return path == "/"
                    ? ("429 Too Many Requests", "text/html", page.Login(RemoteLoginNotice.TooManyAttempts, wait), wait)
                    : ("429 Too Many Requests", "text/plain", "Wait", wait);
        }

        var authorised = Endpoint is { } endpoint && pin is not null
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(pin), Encoding.ASCII.GetBytes(endpoint.Pin));

        if (authorised) throttle.Succeeded(client);
        else if (pin is not null) throttle.Failed(client);

        if (path == "/")
            return ("200 OK", "text/html", authorised ? page.Controller(pin!) : page.Login(pin is null ? RemoteLoginNotice.None : RemoteLoginNotice.WrongPin, TimeSpan.Zero), TimeSpan.Zero);
        if (!authorised) return ("403 Forbidden", "text/plain", "PIN", TimeSpan.Zero);

        if (path == "/state") return ("200 OK", "application/json", await StateAsync().ConfigureAwait(false), TimeSpan.Zero);

        if (method == "POST" && path.StartsWith("/go/", StringComparison.Ordinal))
        {
            Action? action = path[4..] switch
            {
                "next" => () => session.Target?.Next(),
                "previous" => () => session.Target?.Previous(),
                "first" => () => session.Target?.First(),
                "last" => () => session.Target?.Last(),
                _ => null,
            };
            if (action is null) return ("404 Not Found", "text/plain", "?", TimeSpan.Zero);
            await onUi(action).ConfigureAwait(false);
            return ("200 OK", "application/json", await StateAsync().ConfigureAwait(false), TimeSpan.Zero);
        }

        return ("404 Not Found", "text/plain", "?", TimeSpan.Zero);
    }

    private async Task<string> StateAsync()
    {
        var state = new Dictionary<string, object?> { ["running"] = false };
        await onUi(() =>
        {
            if (session.Target is not { } target) return;
            state["running"] = true;
            state["position"] = target.Position + 1;
            state["count"] = target.Count;
            state["title"] = target.Title;
            state["notes"] = target.Notes;
        }).ConfigureAwait(false);
        return JsonSerializer.Serialize(state);
    }
}
