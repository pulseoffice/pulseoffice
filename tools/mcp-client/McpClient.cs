using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PulseOffice.Tools.McpClient;

public sealed class McpHttpClient(HttpClient http, Uri address, string token)
{
    private int _next;

    public string? Session { get; private set; }

    public async Task<JsonObject> InitializeAsync(CancellationToken ct)
    {
        var result = await RequestAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "pulse-mcp", ["version"] = "1.0" },
        }, ct).ConfigureAwait(false);
        await NotifyAsync("notifications/initialized", ct).ConfigureAwait(false);
        return result;
    }

    public async Task<JsonObject> RequestAsync(string method, JsonObject parameters, CancellationToken ct)
    {
        var message = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = ++_next, ["method"] = method, ["params"] = parameters };
        var reply = await SendAsync(message.ToJsonString(), ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{method}: empty reply");
        var json = JsonNode.Parse(reply)!.AsObject();
        if (json["error"] is { } error) throw new InvalidOperationException($"{method}: {error["message"]} ({error["code"]})");
        return json["result"]!.AsObject();
    }

    public Task NotifyAsync(string method, CancellationToken ct) =>
        SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method }.ToJsonString(), ct);

    public async Task<string?> SendAsync(string body, CancellationToken ct)
    {
        var reply = await PostAsync(body, ct).ConfigureAwait(false);
        if (!reply.IsSuccess && reply.Body is null)
            throw new InvalidOperationException($"HTTP {reply.Status} {reply.Reason}");
        return reply.Body;
    }

    public async Task<McpHttpReply> PostAsync(string body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
        if (Session is not null) request.Headers.Add("Mcp-Session-Id", Session);

        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessions)) Session = sessions.First();
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new McpHttpReply((int)response.StatusCode, response.ReasonPhrase, text.Length == 0 ? null : text);
    }

    public void ForgetSession() => Session = null;

    public async Task EndAsync(CancellationToken ct)
    {
        if (Session is null) return;
        using var request = new HttpRequestMessage(HttpMethod.Delete, address);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Mcp-Session-Id", Session);
        using var _ = await http.SendAsync(request, ct).ConfigureAwait(false);
        Session = null;
    }
}

public sealed record McpHttpReply(int Status, string? Reason, string? Body)
{
    public bool IsSuccess => Status is >= 200 and < 300;

    public bool IsJson => Body is { } text && text.TrimStart().StartsWith('{');
}

public sealed class StdioBridge(McpHttpClient client, TextReader input, TextWriter output, TextWriter diagnostics)
{
    private const int NotFound = 404;
    private string? _initialize;

    public async Task RunAsync(CancellationToken ct)
    {
        while (await input.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (line.Trim().Length == 0) continue;
            var reply = await ForwardAsync(line, ct).ConfigureAwait(false);
            if (reply is null) continue;
            await output.WriteLineAsync(reply.AsMemory(), ct).ConfigureAwait(false);
            await output.FlushAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await client.EndAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            await diagnostics.WriteLineAsync($"pulse-mcp: the session could not be closed: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task<string?> ForwardAsync(string line, CancellationToken ct)
    {
        JsonObject? message;
        try
        {
            message = JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            return Error(null, -32700, "Parse error");
        }

        if (message is null) return Error(null, -32600, "A JSON-RPC message must be a single object");
        var id = message["id"]?.DeepClone();
        var isInitialize = message["method"] is JsonValue method && method.TryGetValue<string>(out var name) && name == "initialize";
        if (isInitialize) _initialize = line;

        try
        {
            var reply = await client.PostAsync(line, ct).ConfigureAwait(false);
            if (reply.Status == NotFound && !isInitialize && client.Session is not null && _initialize is not null)
                reply = await ResumeAsync(line, ct).ConfigureAwait(false);
            return Answer(id, reply);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            return id is null ? null : Error(id, -32603, "PulseOffice is not reachable: " + ex.Message);
        }
    }

    private async Task<McpHttpReply> ResumeAsync(string line, CancellationToken ct)
    {
        client.ForgetSession();
        var initialized = await client.PostAsync(_initialize!, ct).ConfigureAwait(false);
        if (!initialized.IsSuccess || client.Session is null) return initialized;
        await diagnostics.WriteLineAsync("pulse-mcp: the PulseOffice session had expired; a new one was opened").ConfigureAwait(false);
        await client.NotifyAsync("notifications/initialized", ct).ConfigureAwait(false);
        return await client.PostAsync(line, ct).ConfigureAwait(false);
    }

    private static string? Answer(JsonNode? id, McpHttpReply reply)
    {
        if (reply.IsJson) return reply.Body;
        if (id is null) return null;
        return reply.IsSuccess
            ? Error(id, -32603, "PulseOffice sent an empty reply")
            : Error(id, -32603, $"PulseOffice refused the request: HTTP {reply.Status} {reply.Reason}".TrimEnd());
    }

    private static string Error(JsonNode? id, int code, string message) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString();
}
