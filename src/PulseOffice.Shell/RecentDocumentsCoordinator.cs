using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public sealed class RecentDocumentsCoordinator : IDisposable
{
    private readonly IRecentDocuments _recent;
    private readonly CommandRegistry _registry;
    private readonly Func<string, Task> _open;
    private readonly List<CommandId> _registered = [];

    public event EventHandler? CommandsChanged;

    public RecentDocumentsCoordinator(
        IRecentDocuments recent, CommandRegistry registry, Func<string, Task> open)
    {
        _recent = recent ?? throw new ArgumentNullException(nameof(recent));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _open = open ?? throw new ArgumentNullException(nameof(open));

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
                Execute = async (_, _) =>
                {
                    if (!File.Exists(item.Path))
                    {
                        _recent.Forget(item.Path);
                        return;
                    }

                    await _open(item.Path).ConfigureAwait(true);
                },
            });

            _registered.Add(id);
        }
    }

    public void Dispose() => _recent.Changed -= OnChanged;
}
