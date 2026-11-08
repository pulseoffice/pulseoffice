using System.Text.Json;
using Microsoft.Extensions.Logging;
using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public interface IRecentCommands
{
    IReadOnlyList<CommandId> Items { get; }

    void Remember(CommandId id);
}

public sealed class RecentCommands : IRecentCommands
{
    private readonly List<string> _items = [];
    private readonly string _path;
    private readonly int _capacity;
    private readonly ILogger<RecentCommands> _logger;
    private readonly Lock _gate = new();

    public RecentCommands(ILogger<RecentCommands> logger, string path, int capacity = 8)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrEmpty(path);
        _path = path;
        _capacity = capacity;
        Load();
    }

    public IReadOnlyList<CommandId> Items
    {
        get { lock (_gate) return [.. _items.Select(id => new CommandId(id))]; }
    }

    public void Remember(CommandId id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id.Value);

        lock (_gate)
        {
            _items.Remove(id.Value);
            _items.Insert(0, id.Value);
            if (_items.Count > _capacity) _items.RemoveRange(_capacity, _items.Count - _capacity);
        }

        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            if (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path)) is not { } stored) return;

            lock (_gate)
                _items.AddRange(stored.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Take(_capacity));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unreadable recent commands list: starting empty");
        }
    }

    private void Save()
    {
        var temporary = _path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            List<string> snapshot;
            lock (_gate) snapshot = [.. _items];

            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not save recent commands");
            try { File.Delete(temporary); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
        }
    }
}
