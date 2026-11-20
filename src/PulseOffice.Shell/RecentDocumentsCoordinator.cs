using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public interface IRecentFileProbe
{
    bool Exists(string path);
}

public sealed class RecentFileProbe : IRecentFileProbe
{
    public bool Exists(string path) => File.Exists(path);
}

public sealed class RecentDocumentsCoordinator : IDisposable
{
    public static readonly CommandId PinCurrentId = new("shell.file.recent.pinCurrent");

    private readonly IRecentDocuments _recent;
    private readonly CommandRegistry _registry;
    private readonly Func<string, Task> _open;
    private readonly IMissingRecentPrompt _missing;
    private readonly IRecentFileProbe _files;
    private readonly Func<string?> _currentPath;
    private readonly List<CommandId> _registered = [];

    public event EventHandler? CommandsChanged;

    public RecentDocumentsCoordinator(
        IRecentDocuments recent, CommandRegistry registry, Func<string, Task> open,
        IMissingRecentPrompt missing, IRecentFileProbe files, Func<string?> currentPath)
    {
        _recent = recent ?? throw new ArgumentNullException(nameof(recent));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _missing = missing ?? throw new ArgumentNullException(nameof(missing));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _currentPath = currentPath ?? throw new ArgumentNullException(nameof(currentPath));

        _recent.Changed += OnChanged;
        Rebuild();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        Rebuild();
        CommandsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild()
    {
        foreach (var id in _registered) _registry.Unregister(id);
        _registered.Clear();

        var items = _recent.Items;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var id = new CommandId($"shell.file.recent.{i}");

            _registry.Register(new CommandDefinition
            {
                Id = id,
                TitleOverride = item.Name,
                MenuPath = "file/recent",
                Surface = CommandSurface.Advanced,
                Keywords = [item.Name, item.Path],
                CheckedWhen = _ => item.Pinned,
                Execute = (_, ct) => OpenAsync(item, ct),
            });

            _registered.Add(id);
        }

        _registry.Register(new CommandDefinition
        {
            Id = PinCurrentId,
            IconKey = PinCurrentId.Value,
            MenuPath = "file/recent",
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => CurrentEntry() is not null,
            CheckedWhen = _ => CurrentEntry() is { Pinned: true },
            Execute = (_, _) =>
            {
                if (CurrentEntry() is { } entry) _recent.SetPinned(entry.Path, !entry.Pinned);
                return Task.CompletedTask;
            },
        });
        _registered.Add(PinCurrentId);
    }

    private RecentDocument? CurrentEntry()
    {
        if (_currentPath() is not { Length: > 0 } path) return null;
        return _recent.Find(path);
    }

    private async Task OpenAsync(RecentDocument item, CancellationToken ct)
    {
        if (_files.Exists(item.Path))
        {
            await _open(item.Path).ConfigureAwait(true);
            return;
        }

        if (await _missing.AskAsync(item, ct).ConfigureAwait(true) == MissingRecentAnswer.Remove)
            _recent.Forget(item.Path);
    }

    public void Dispose() => _recent.Changed -= OnChanged;
}
