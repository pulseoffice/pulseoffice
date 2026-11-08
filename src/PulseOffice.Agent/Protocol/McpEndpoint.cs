using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Tools;

namespace PulseOffice.Agent.Protocol;

public sealed record McpReply(int Status, string? Body, string? SessionId)
{
    public static McpReply Accepted { get; } = new(202, null, null);
}

public sealed record McpCall(JsonObject Params, AgentSession? Session);

public interface IMcpMethod
{
    string Name { get; }

    Task<JsonNode> InvokeAsync(McpCall call, CancellationToken ct);
}

public interface IMcpEndpoint
{
    Task<McpReply> HandleAsync(string body, string? sessionId, CancellationToken ct);
}

public interface IMcpProtocolVersions
{
    IReadOnlyList<string> Supported { get; }

    string Negotiate(string? requested);
}

public sealed class McpProtocolVersions : IMcpProtocolVersions
{
    public IReadOnlyList<string> Supported { get; } = ["2025-06-18", "2025-03-26", "2024-11-05"];

    public string Negotiate(string? requested) =>
        requested is not null && Supported.Contains(requested, StringComparer.Ordinal) ? requested : Supported[0];
}

public sealed class McpEndpoint(
    IJsonRpcCodec codec,
    IEnumerable<IMcpMethod> methods,
    IAgentSessions sessions,
    IMcpProtocolVersions versions,
    IAgentRequestGate gate,
    ILogger<McpEndpoint> logger) : IMcpEndpoint
{
    private readonly IReadOnlyDictionary<string, IMcpMethod> _methods =
        methods.ToDictionary(method => method.Name, StringComparer.Ordinal);

    public async Task<McpReply> HandleAsync(string body, string? sessionId, CancellationToken ct)
    {
        var parsed = codec.Parse(body);
        if (!parsed.Succeeded) return new McpReply(400, codec.Error(parsed.Id, parsed.ErrorCode, parsed.ErrorMessage ?? "Invalid request"), null);
        if (parsed.Request is not { } request) return McpReply.Accepted;

        if (request.Method == "initialize") return Initialize(request);

        var session = sessions.Find(sessionId);
        if (session is null)
            return sessionId is null
                ? new McpReply(400, codec.Error(request.Id, JsonRpcCodes.InvalidRequest, "Missing Mcp-Session-Id: send initialize first"), null)
                : new McpReply(404, codec.Error(request.Id, JsonRpcCodes.InvalidRequest, "Unknown or expired session"), null);
        sessions.Touch(session);

        if (request.IsNotification) return McpReply.Accepted;

        if (!_methods.TryGetValue(request.Method, out var method))
            return new McpReply(200, codec.Error(request.Id, JsonRpcCodes.MethodNotFound, $"Method not found: {request.Method}"), null);

        using var ticket = gate.TryEnter(session);
        if (ticket is null)
        {
            logger.LogWarning("Agent client {Client} exceeded the limit of concurrent requests; {Method} refused", session.Client, request.Method);
            return new McpReply(429, codec.Error(request.Id, JsonRpcCodes.TooManyRequests, "Too many requests in flight: wait for the running ones to finish"), null);
        }

        try
        {
            var result = await method.InvokeAsync(new McpCall(request.Params, session), ct).ConfigureAwait(true);
            return new McpReply(200, codec.Result(request.Id, result), null);
        }
        catch (JsonRpcException ex)
        {
            return new McpReply(200, codec.Error(request.Id, ex.Code, ex.Message), null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            logger.LogError(ex, "MCP method {Method} failed", request.Method);
            return new McpReply(200, codec.Error(request.Id, JsonRpcCodes.InternalError, ex.Message), null);
        }
    }

    private McpReply Initialize(JsonRpcRequest request)
    {
        if (request.IsNotification) return McpReply.Accepted;

        var version = versions.Negotiate(Text(request.Params["protocolVersion"]));
        var client = Text((request.Params["clientInfo"] as JsonObject)?["name"]) is { Length: > 0 } name ? Shortened(name) : "client";
        if (sessions.TryCreate(client, version) is not { } session)
        {
            logger.LogWarning("Agent client {Client} refused: the limit of open sessions is reached", client);
            return new McpReply(503, codec.Error(request.Id, JsonRpcCodes.TooManySessions, "Too many open sessions: close one or wait for idle ones to expire"), null);
        }
        logger.LogInformation("Agent client {Client} connected with protocol {Version}", client, version);

        var result = new JsonObject
        {
            ["protocolVersion"] = version,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "PulseOffice",
                ["version"] = typeof(McpEndpoint).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0",
            },
            ["instructions"] =
                "PulseOffice is an office suite with three modules: Folio (text), Ledger (spreadsheets) and Deck (presentations). " +
                "Call list_documents first. Content tools are named <module>_<object>_<member> and act on the active document of that module, " +
                "or on the document passed in 'document'. Every change is one undoable step marked as made by the agent. " +
                "Use render_page to look at the result.",
        };

        return new McpReply(200, codec.Result(request.Id, result), session.Id);
    }

    private static string Shortened(string name)
    {
        var printable = new string([.. name.Where(character => !char.IsControl(character)).Take(MaximumClientName)]);
        return printable.Length > 0 ? printable : "client";
    }

    private const int MaximumClientName = 64;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

public sealed class PingMethod : IMcpMethod
{
    public string Name => "ping";

    public Task<JsonNode> InvokeAsync(McpCall call, CancellationToken ct) => Task.FromResult<JsonNode>(new JsonObject());
}

public sealed class ToolsListMethod(IAgentToolCatalog catalog, IAgentToolInvoker invoker) : IMcpMethod
{
    public string Name => "tools/list";

    public Task<JsonNode> InvokeAsync(McpCall call, CancellationToken ct)
    {
        var tools = new JsonArray();
        foreach (var tool in catalog.All.Where(invoker.IsAllowed))
            tools.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema(),
                ["annotations"] = new JsonObject
                {
                    ["readOnlyHint"] = tool.Annotations.ReadOnly,
                    ["destructiveHint"] = tool.Annotations.Destructive,
                },
            });

        return Task.FromResult<JsonNode>(new JsonObject { ["tools"] = tools });
    }
}

public sealed class ToolsCallMethod(IAgentToolCatalog catalog, IAgentToolInvoker invoker) : IMcpMethod
{
    public string Name => "tools/call";

    public async Task<JsonNode> InvokeAsync(McpCall call, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (call.Params["name"] is not JsonValue nameValue || !nameValue.TryGetValue<string>(out var name))
            throw new JsonRpcException(JsonRpcCodes.InvalidParams, "tools/call needs a tool name");
        if (catalog.Find(name) is not { } tool)
            throw new JsonRpcException(JsonRpcCodes.InvalidParams, $"Unknown tool: {name}");

        var arguments = call.Params["arguments"] switch
        {
            null => new JsonObject(),
            JsonObject given => (JsonObject)given.DeepClone(),
            _ => throw new JsonRpcException(JsonRpcCodes.InvalidParams, "The arguments must be an object"),
        };

        var result = await invoker.InvokeAsync(tool, arguments, call.Session?.Client ?? "client", ct).ConfigureAwait(true);
        var content = new JsonArray();
        foreach (var item in result.Content)
            content.Add(item switch
            {
                AgentImage image => new JsonObject { ["type"] = "image", ["data"] = Convert.ToBase64String(image.Data), ["mimeType"] = image.MimeType },
                AgentText text => new JsonObject { ["type"] = "text", ["text"] = text.Text },
                _ => new JsonObject { ["type"] = "text", ["text"] = string.Empty },
            });

        return new JsonObject { ["content"] = content, ["isError"] = result.IsError };
    }
}
