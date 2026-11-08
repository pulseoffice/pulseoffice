using System.Text.Json;
using System.Text.Json.Nodes;
using PulseOffice.Settings;
using PulseOffice.UI.Commands.Shortcuts;

namespace PulseOffice.UI.Controls.Services;

public sealed class ShortcutBindingStore : IShortcutBindingStore
{
    private const int Version = 1;

    private readonly string _path;
    private readonly Lock _gate = new();

    public ShortcutBindingStore(string path) =>
        _path = path;

    public IReadOnlyDictionary<string, string?> Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_path)) return new Dictionary<string, string?>();

                return JsonNode.Parse(File.ReadAllText(_path))?["bindings"] is JsonObject bindings
                    ? bindings
                        .Where(pair => pair.Key.Length > 0)
                        .Where(pair => pair.Value is null || pair.Value.GetValueKind() == JsonValueKind.String)
                        .ToDictionary(pair => pair.Key, pair => pair.Value?.GetValue<string>(), StringComparer.Ordinal)
                    : new Dictionary<string, string?>();
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return new Dictionary<string, string?>();
            }
        }
    }

    public void Save(IReadOnlyDictionary<string, string?> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var bindings = new JsonObject();
        foreach (var (id, gesture) in overrides.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            bindings[id] = gesture is null ? null : JsonValue.Create(gesture);

        var document = new JsonObject { ["version"] = Version, ["bindings"] = bindings };

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temporary = _path + ".tmp";
                File.WriteAllText(temporary, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporary, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
