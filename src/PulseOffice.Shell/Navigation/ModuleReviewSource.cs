using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls.Review;

namespace PulseOffice.Shell.Navigation;

public interface IModuleReviewSources
{
    void Register(ModuleKind module, IReviewSource source);
}

public sealed class ModuleReviewSource : IReviewSource, IModuleReviewSources
{
    private readonly IDocumentWorkspace _workspace;
    private readonly Dictionary<ModuleKind, IReviewSource> _sources = [];
    private readonly Avalonia.Threading.DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private PulseOffice.Document.Commands.UndoStack? _history;

    public ModuleReviewSource(IDocumentWorkspace workspace)
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

    public event EventHandler? Changed;

    public void Register(ModuleKind module, IReviewSource source)
    {
        _sources[module] = source;
        source.Changed += (_, _) =>
        {
            if (ReferenceEquals(Active, source)) Changed?.Invoke(this, EventArgs.Empty);
        };
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

    private IReviewSource? Active =>
        _workspace.Active is { } document && _sources.TryGetValue(document.Module, out var source) ? source : null;

    private bool Locked => _workspace.Active?.IsReadOnly == true;

    public ReviewAbilities Abilities => Active?.Abilities is { } abilities
        ? Locked ? abilities with { Resolve = false, Comment = false, ResolveComments = false, Edit = false } : abilities
        : ReviewAbilities.None;

    public IReadOnlyList<ReviewItem> Changes => Active?.Changes ?? [];

    public IReadOnlyList<ReviewItem> Comments => Active?.Comments ?? [];

    public string? Current => Active?.Current;

    public void Go(string id) => Active?.Go(id);

    public void Accept(IReadOnlyList<string> ids)
    {
        if (!Locked) Active?.Accept(ids);
    }

    public void Reject(IReadOnlyList<string> ids)
    {
        if (!Locked) Active?.Reject(ids);
    }

    public void Reply(string id, string text)
    {
        if (!Locked) Active?.Reply(id, text);
    }

    public void Edit(string id, string text)
    {
        if (!Locked) Active?.Edit(id, text);
    }

    public void ToggleResolved(string id)
    {
        if (!Locked) Active?.ToggleResolved(id);
    }

    public void Delete(IReadOnlyList<string> ids)
    {
        if (!Locked) Active?.Delete(ids);
    }
}
