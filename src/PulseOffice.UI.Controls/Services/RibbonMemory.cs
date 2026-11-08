using System.Text.Json;
using PulseOffice.Settings;

namespace PulseOffice.UI.Controls.Services;

public interface IRibbonMemory
{
    string? Recall(string key, string? fallback);

    void Remember(string key, string? value);
}

public sealed class RibbonMemory : IRibbonMemory
{
    private const string NoValue = "none";

    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, string>? _values;

    public RibbonMemory(string path) =>
        _path = path;

    public string? Recall(string key, string? fallback)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        lock (_gate)
            return Values().TryGetValue(key, out var value) ? (value == NoValue ? null : value) : fallback;
    }

    public void Remember(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        lock (_gate)
        {
            var values = Values();
            var stored = value ?? NoValue;
            if (values.TryGetValue(key, out var current) && current == stored) return;

            values[key] = stored;
            Save(values);
        }
    }

    private Dictionary<string, string> Values()
    {
        if (_values is not null) return _values;

        try
        {
            _values = File.Exists(_path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _values = [];
        }

        return _values;
    }

    private void Save(Dictionary<string, string> values)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
