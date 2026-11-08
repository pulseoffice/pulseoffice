using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public sealed class SnapshotWriter(Utf8JsonWriter writer, JsonSerializerOptions options)
{
    public SnapshotWriter Put<T>(string name, T value)
    {
        if (value is null) return this;
        writer.WritePropertyName(name);
        JsonSerializer.Serialize(writer, value, options);
        return this;
    }
}

public sealed class SnapshotReader(JsonObject node, JsonSerializerOptions options)
{
    public T? Get<T>(string name) =>
        node.TryGetPropertyValue(name, out var value) && value is not null ? value.Deserialize<T>(options) : default;

    public T Get<T>(string name, T fallback) =>
        node.TryGetPropertyValue(name, out var value) && value is not null ? value.Deserialize<T>(options) ?? fallback : fallback;

    public T Require<T>(string name) => Get<T>(name) ?? throw new JsonException($"Snapshot member '{name}' is missing.");
}

public abstract class SnapshotConverter<T> : JsonConverter<T> where T : class
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonNode.Parse(ref reader) is not JsonObject node) throw new JsonException($"A snapshot object was expected for {typeof(T).Name}.");
        return Read(new SnapshotReader(node, options));
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        Write(value, new SnapshotWriter(writer, options));
        writer.WriteEndObject();
    }

    protected abstract T Read(SnapshotReader node);

    protected abstract void Write(T value, SnapshotWriter node);
}
