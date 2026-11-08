using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PulseOffice.Agent.Tools;

public abstract record AgentContent;

public sealed record AgentText(string Text) : AgentContent;

public sealed record AgentImage(byte[] Data, string MimeType) : AgentContent;

public sealed record AgentToolResult(IReadOnlyList<AgentContent> Content, bool IsError = false)
{
    private static readonly JsonSerializerOptions Readable = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public static AgentToolResult Text(string text) => new([new AgentText(text)]);

    public static AgentToolResult Json(JsonNode? value) => new([new AgentText(value?.ToJsonString(Readable) ?? "null")]);

    public static AgentToolResult Failure(string message) => new([new AgentText(message)], IsError: true);
}

public sealed class AgentArgumentException(string message) : Exception(message);

public sealed class AgentArguments(JsonObject values)
{
    public JsonObject Values { get; } = values ?? new JsonObject();

    public bool Has(string name) => Values[name] is not null;

    public JsonNode? Raw(string name) => Values[name];

    public string Text(string name) => OptionalText(name) is { Length: > 0 } text
        ? text
        : throw new AgentArgumentException($"The argument '{name}' is required.");

    public string? OptionalText(string name) => Values[name] switch
    {
        null => null,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        JsonValue value => value.ToJsonString(),
        _ => throw new AgentArgumentException($"The argument '{name}' must be a string."),
    };

    public int Integer(string name, int fallback)
    {
        if (Values[name] is not JsonValue value) return fallback;
        return value.GetValueKind() switch
        {
            JsonValueKind.Number when value.TryGetValue<int>(out var number) => number,
            JsonValueKind.Number when Math.Round(value.GetValue<double>()) is >= int.MinValue and <= int.MaxValue and var rounded => (int)rounded,
            JsonValueKind.String when int.TryParse(value.GetValue<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new AgentArgumentException($"The argument '{name}' must be a number."),
        };
    }

    public bool Flag(string name, bool fallback) => Values[name] switch
    {
        null => fallback,
        JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False => value.GetValue<bool>(),
        _ => throw new AgentArgumentException($"The argument '{name}' must be true or false."),
    };
}

public sealed record AgentToolAnnotations(bool ReadOnly, bool Destructive);

public abstract class AgentTool
{
    public abstract string Name { get; }

    public virtual string Title => Name;

    public abstract string Description { get; }

    public abstract AgentAccessLevel Access { get; }

    public abstract JsonObject InputSchema();

    public virtual AgentToolAnnotations Annotations => new(Access == AgentAccessLevel.Read, false);

    public virtual Task<Hosting.AgentConfirmationNeed?> ConfirmationAsync(AgentArguments arguments, CancellationToken ct) =>
        Task.FromResult<Hosting.AgentConfirmationNeed?>(null);

    public abstract Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct);
}

public interface IAgentToolSource
{
    IEnumerable<AgentTool> Tools { get; }
}

public interface IAgentToolCatalog
{
    IReadOnlyList<AgentTool> All { get; }

    AgentTool? Find(string name);
}

public sealed class AgentToolCatalog(IEnumerable<IAgentToolSource> sources) : IAgentToolCatalog
{
    private readonly IReadOnlyList<IAgentToolSource> _sources = [.. sources];

    public IReadOnlyList<AgentTool> All =>
        [.. _sources.SelectMany(source => source.Tools)
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Select(group => group.First())];

    public AgentTool? Find(string name) =>
        _sources.SelectMany(source => source.Tools).FirstOrDefault(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));
}

public sealed class SchemaBuilder
{
    private readonly JsonObject _properties = [];
    private readonly JsonArray _required = [];

    public SchemaBuilder String(string name, string description, bool required = false, IReadOnlyList<string>? choices = null)
    {
        var property = new JsonObject { ["type"] = "string", ["description"] = description };
        if (choices is { Count: > 0 }) property["enum"] = new JsonArray([.. choices.Select(choice => (JsonNode)JsonValue.Create(choice))]);
        return Add(name, property, required);
    }

    public SchemaBuilder Integer(string name, string description, bool required = false, int? minimum = null)
    {
        var property = new JsonObject { ["type"] = "integer", ["description"] = description };
        if (minimum is { } floor) property["minimum"] = floor;
        return Add(name, property, required);
    }

    public SchemaBuilder Boolean(string name, string description, bool required = false) =>
        Add(name, new JsonObject { ["type"] = "boolean", ["description"] = description }, required);

    public SchemaBuilder Any(string name, string description, bool required = false) =>
        Add(name, new JsonObject { ["description"] = description }, required);

    public JsonObject Build()
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = _properties.DeepClone() };
        if (_required.Count > 0) schema["required"] = _required.DeepClone();
        return schema;
    }

    private SchemaBuilder Add(string name, JsonObject property, bool required)
    {
        _properties[name] = property;
        if (required) _required.Add(name);
        return this;
    }
}
