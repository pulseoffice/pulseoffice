using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PulseOffice.Settings;

public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private readonly ConcurrentDictionary<string, JsonElement> _values = new(StringComparer.Ordinal);
    private readonly string _path;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly Lock _gate = new();
    private readonly ITimer _timer;
    private volatile bool _disposed;

    private static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(600);

    public JsonSettingsStore(SettingScope scope, string path, TimeProvider time, ILogger<JsonSettingsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(time);
        Scope = scope;
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _timer = time.CreateTimer(_ => FlushScheduled(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Load();
    }

    public SettingScope Scope { get; }

    public IReadOnlyCollection<string> Keys => _values.Keys.ToList();

    public bool TryGet(string key, out object? value)
    {
        if (_values.TryGetValue(key, out var element))
        {
            value = FromJson(element);
            return true;
        }

        value = null;
        return false;
    }

    public void Set(string key, object? value)
    {
        _values[key] = ToJson(value);
        Schedule();
    }

    public void Remove(string key)
    {
        _values.TryRemove(key, out _);
        Schedule();
    }

    public void Flush()
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

                var temporary = _path + ".tmp";
                var json = JsonSerializer.Serialize(
                    _values.ToDictionary(pair => pair.Key, pair => pair.Value),
                    new JsonSerializerOptions { WriteIndented = true });

                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(System.Text.Encoding.UTF8.GetBytes(json));
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, _path, overwrite: true);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not save settings to {Path}", _path);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Permission denied saving settings to {Path}", _path);
            }
        }
    }

    private void FlushScheduled()
    {
        lock (_gate)
        {
            if (_disposed) return;
            Flush();
        }
    }

    private void Schedule()
    {
        if (_disposed)
        {
            Flush();
            return;
        }

        try
        {
            _timer.Change(WriteDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            Flush();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(_path));
            if (parsed is null) return;

            foreach (var (key, value) in parsed) _values[key] = value;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Unreadable settings at {Path}: falling back to defaults", _path);
            PreserveCorrupt();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read settings from {Path}", _path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied reading settings from {Path}", _path);
        }
    }

    private void PreserveCorrupt()
    {
        try
        {
            File.Copy(_path, _path + ".corrupt", overwrite: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not keep a copy of the unreadable settings at {Path}", _path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission denied keeping a copy of the unreadable settings at {Path}", _path);
        }
    }

    private static JsonElement ToJson(object? value) =>
    JsonSerializer.SerializeToElement(value);

    private static object? FromJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),

        JsonValueKind.Number => element.TryGetInt64(out var integer) ? (object)integer : element.GetDouble(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.ToString(),
    };

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _timer.Dispose();
        Flush();
    }
}
