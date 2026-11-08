using System.Net.Http.Headers;
using System.Text;
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
        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
        if (Session is not null) request.Headers.Add("Mcp-Session-Id", Session);

        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessions)) Session = sessions.First();
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && text.Length == 0)
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        return text.Length == 0 ? null : text;
    }

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

public sealed class StdioBridge(McpHttpClient client, TextReader input, TextWriter output)
{
    public async Task RunAsync(CancellationToken ct)
    {
        while (await input.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (line.Trim().Length == 0) continue;
            string? reply;
            try
            {
                reply = await client.SendAsync(line, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                reply = Failure(line, ex.Message);
            }

            if (reply is null) continue;
            await output.WriteLineAsync(reply.AsMemory(), ct).ConfigureAwait(false);
            await output.FlushAsync(ct).ConfigureAwait(false);
        }

        await client.EndAsync(ct).ConfigureAwait(false);
    }

    private static string? Failure(string line, string message)
    {
        var id = JsonNode.Parse(line)?["id"]?.DeepClone();
        return id is null
            ? null
            : new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JsonObject { ["code"] = -32603, ["message"] = "PulseOffice is not reachable: " + message },
            }.ToJsonString();
    }
}
