using System.Text.Json;
using System.Text.Json.Nodes;

namespace PulseOffice.Agent.Protocol;

public static class JsonRpcCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int TooManySessions = -32001;
    public const int TooManyRequests = -32002;
}

public sealed record JsonRpcRequest(JsonNode? Id, string Method, JsonObject Params)
{
    public bool IsNotification => Id is null;
}

public sealed record JsonRpcParse(JsonRpcRequest? Request, bool IsResponse, int ErrorCode, string? ErrorMessage, JsonNode? Id)
{
    public bool Succeeded => Request is not null || IsResponse;
}

public sealed class JsonRpcException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

public interface IJsonRpcCodec
{
    JsonRpcParse Parse(string body);

    string Result(JsonNode? id, JsonNode result);

    string Error(JsonNode? id, int code, string message);
}

public sealed class JsonRpcCodec : IJsonRpcCodec
{
    private const int MaximumDepth = 64;

    private static readonly JsonSerializerOptions Readable = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public JsonRpcParse Parse(string body)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false, MaxDepth = MaximumDepth });
        }
        catch (JsonException)
        {
            return Failure(JsonRpcCodes.ParseError, "Parse error", null);
        }

        if (root is not JsonObject message)
            return Failure(JsonRpcCodes.InvalidRequest, "A JSON-RPC message must be a single object", null);

        var id = message["id"]?.DeepClone();
        if (id is not null && id.GetValueKind() is not (JsonValueKind.String or JsonValueKind.Number))
            return Failure(JsonRpcCodes.InvalidRequest, "The id must be a string or a number", null);

        if (message["jsonrpc"]?.GetValueKind() != JsonValueKind.String || message["jsonrpc"]!.GetValue<string>() != "2.0")
            return Failure(JsonRpcCodes.InvalidRequest, "The jsonrpc member must be \"2.0\"", id);

        if (message["method"] is null)
            return message.ContainsKey("result") || message.ContainsKey("error")
                ? new JsonRpcParse(null, true, 0, null, id)
                : Failure(JsonRpcCodes.InvalidRequest, "The method is missing", id);

        if (message["method"]!.GetValueKind() != JsonValueKind.String)
            return Failure(JsonRpcCodes.InvalidRequest, "The method must be a string", id);

        if (message.ContainsKey("id") && message["id"] is null)
            return Failure(JsonRpcCodes.InvalidRequest, "The id must be a string or a number, not null", null);

        var parameters = message["params"] switch
        {
            null => new JsonObject(),
            JsonObject given => (JsonObject)given.DeepClone(),
            _ => null,
        };

        return parameters is null
            ? Failure(JsonRpcCodes.InvalidParams, "The params must be an object", id)
            : new JsonRpcParse(new JsonRpcRequest(id, message["method"]!.GetValue<string>(), parameters), false, 0, null, id);
    }

    public string Result(JsonNode? id, JsonNode result) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = result }.ToJsonString(Readable);

    public string Error(JsonNode? id, int code, string message) =>
        new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString(Readable);

    private static JsonRpcParse Failure(int code, string message, JsonNode? id) => new(null, false, code, message, id);
}
