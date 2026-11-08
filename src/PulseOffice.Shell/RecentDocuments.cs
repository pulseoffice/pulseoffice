using System.Text.Json;
using Microsoft.Extensions.Logging;
using PulseOffice.Platform;
using PulseOffice.Settings;

namespace PulseOffice.Shell;

public sealed record RecentDocument(string Path, string Name, DateTimeOffset LastOpened);

public interface IRecentDocuments
{
    IReadOnlyList<RecentDocument> Items { get; }

    void Remember(string path, string name);

    void Forget(string path);

    void Clear();

    event EventHandler? Changed;
}

public sealed class RecentDocuments : IRecentDocuments
{
    private readonly List<RecentDocument> _items = [];
    private readonly string _path;
    private readonly ILogger<RecentDocuments> _logger;
    private const int MaximumKept = 100;

    private readonly Func<int> _capacity;
    private readonly Lock _gate = new();
    private readonly StringComparison _pathComparison;

    public RecentDocuments(ILogger<RecentDocuments> logger, string path, int capacity = 12, StringComparison? pathComparison = null)
        : this(logger, path, () => capacity, pathComparison)
    {
    }

    public RecentDocuments(ILogger<RecentDocuments> logger, string path, Func<int> capacity, StringComparison? pathComparison = null)
    {
        _pathComparison = pathComparison ?? ((IHostSystem)new RuntimeHostSystem()).PathComparison;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
        _path = path;

        Load();
    }

    private int Capacity => Math.Clamp(_capacity(), 0, MaximumKept);

    public IReadOnlyList<RecentDocument> Items
    {
        get { lock (_gate) return [.. _items.Take(Capacity)]; }
    }

    public event EventHandler? Changed;

    public void Remember(string path, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        path = Canonical(path);

        lock (_gate)
        {
            _items.RemoveAll(item => string.Equals(item.Path, path, _pathComparison));
            _items.Insert(0, new RecentDocument(path, name, DateTimeOffset.Now));

            var kept = Capacity;
            if (_items.Count > kept) _items.RemoveRange(kept, _items.Count - kept);
        }

        Save();
    }

    public void Forget(string path)
    {
        path = Canonical(path);
        lock (_gate)
            if (_items.RemoveAll(item =>
                    string.Equals(item.Path, path, _pathComparison)) == 0)
                return;

        Save();
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_items.Count == 0) return;
            _items.Clear();
        }

        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var stored = JsonSerializer.Deserialize<List<RecentDocument>>(File.ReadAllText(_path));
            if (stored is null) return;

            var seen = new HashSet<string>(StringComparer.FromComparison(_pathComparison));
            lock (_gate)
                _items.AddRange(stored
                    .Where(item => item is not null && !string.IsNullOrEmpty(item.Path))
                    .Select(item => item with { Path = Canonical(item.Path) })
                    .Where(item => seen.Add(item.Path) && File.Exists(item.Path))
                    .Select(item => item with { Name = item.Name ?? Path.GetFileName(item.Path) })
                    .Take(MaximumKept));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unreadable recent documents list: starting empty");
        }
    }

    private void Save()
    {
        var temporary = _path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            List<RecentDocument> snapshot;
            lock (_gate) snapshot = [.. _items];

            File.WriteAllText(temporary, JsonSerializer.Serialize(
                snapshot, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not save recent documents");
            try { File.Delete(temporary); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string Canonical(string path) => Canonical(path, depth: 0);

    private string Canonical(string path, int depth)
    {
        if (depth > 16) return path;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full) ?? string.Empty;
            var resolved = root;
            foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                resolved = Path.Combine(resolved, part);
                var info = Directory.Exists(resolved) ? (FileSystemInfo)new DirectoryInfo(resolved) : new FileInfo(resolved);
                if (info.Exists && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                    resolved = Canonical(target.FullName, depth + 1);
            }
            return resolved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return path;
        }
    }
}
