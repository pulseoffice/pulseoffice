using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls.Navigation;

namespace PulseOffice.Shell.Navigation;

public interface IModuleNavigatorSources
{
    void Register(ModuleKind module, INavigatorSource source);
}

public sealed class ModuleNavigatorSource : INavigatorSource, IModuleNavigatorSources
{
    private readonly IDocumentWorkspace _workspace;
    private readonly Dictionary<ModuleKind, INavigatorSource> _sources = [];
    private readonly Avalonia.Threading.DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private PulseOffice.Document.Commands.UndoStack? _history;

    public ModuleNavigatorSource(IDocumentWorkspace workspace)
    {
        _workspace = workspace;
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        };
        workspace.ActiveChanged += (_, _) => Refollow();
        workspace.DocumentClosed += (_, _) => Refollow();
    }

    private void Refollow()
    {
        Follow();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Follow()
    {
        if (_history is not null) _history.Changed -= OnEdited;
        _history = _workspace.Active?.History;
        if (_history is not null) _history.Changed += OnEdited;
    }

    private void OnEdited(object? sender, EventArgs args)
    {
        _settle.Stop();
        _settle.Start();
    }

    public event EventHandler? Changed;

    public void Register(ModuleKind module, INavigatorSource source)
    {
        _sources[module] = source;
        source.Changed += (_, _) =>
        {
            if (ReferenceEquals(Active, source)) Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    private INavigatorSource? Active =>
        _workspace.Active is { } document && _sources.TryGetValue(document.Module, out var source) ? source : null;

    public IReadOnlyList<NavigatorCategory> Categories => Active?.Categories ?? [];

    public IReadOnlyList<NavigatorItem> Items(string category) => Active?.Items(category) ?? [];

    public string? Current(string category) => Active?.Current(category);

    public void Go(string category, string id) => Active?.Go(category, id);

    private bool Locked => _workspace.Active?.IsReadOnly == true;

    public bool CanMove(string category, string id, NavigatorMove move) => !Locked && (Active?.CanMove(category, id, move) ?? false);

    public void Move(string category, string id, NavigatorMove move)
    {
        if (!Locked) Active?.Move(category, id, move);
    }

    public bool CanDrop(string category, string id, string? beforeId) => !Locked && (Active?.CanDrop(category, id, beforeId) ?? false);

    public void Drop(string category, string id, string? beforeId)
    {
        if (!Locked) Active?.Drop(category, id, beforeId);
    }
}
