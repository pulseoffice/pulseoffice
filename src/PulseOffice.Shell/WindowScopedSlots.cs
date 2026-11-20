using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PulseOffice.Shell;

public interface IWindowScopeLocator
{
    ShellWindowScope? ScopeShowing(Control owner);

    OpenDocument? DocumentShowing(ShellWindowScope scope, Control owner);
}

public sealed class WindowScopeLocator(IShellWindowFocus focus) : IWindowScopeLocator
{
    private readonly IShellWindowFocus _focus = focus ?? throw new ArgumentNullException(nameof(focus));

    public ShellWindowScope? ScopeShowing(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var lineage = owner.GetSelfAndVisualAncestors().ToList();
        bool Shows(OpenDocument document) => ReferenceEquals(document.View, owner) || lineage.Contains(document.View);
        return _focus.Scopes.FirstOrDefault(scope => scope.Workspace.Active is { } active && Shows(active))
            ?? _focus.Scopes.FirstOrDefault(scope => scope.Workspace.Documents.Any(Shows))
            ?? OwnerWindowScope(owner);
    }

    public OpenDocument? DocumentShowing(ShellWindowScope scope, Control owner)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(owner);
        var lineage = owner.GetSelfAndVisualAncestors().ToList();
        return scope.Workspace.Documents.FirstOrDefault(document => ReferenceEquals(document.View, owner))
            ?? scope.Workspace.Documents.FirstOrDefault(document => lineage.Contains(document.View));
    }

    private ShellWindowScope? OwnerWindowScope(Control owner) =>
        TopLevel.GetTopLevel(owner) is IShellWindowScopeOwner window
            ? _focus.Scopes.FirstOrDefault(scope => ReferenceEquals(scope.Workspace, window.Scope.Workspace))
            : null;
}

public interface IWindowScopedSlots<TSlot> where TSlot : class
{
    TSlot Current { get; }

    IReadOnlyList<TSlot> All { get; }

    TSlot SlotFor(Control owner);

    Control? OwnerOf(TSlot slot);

    void Own(TSlot slot, Control? owner);

    event EventHandler? FocusChanged;

    event EventHandler<TSlot>? SlotCreated;

    event EventHandler<TSlot>? SlotDropped;
}

public sealed class WindowScopedSlots<TSlot> : IWindowScopedSlots<TSlot> where TSlot : class
{
    private readonly IShellWindowFocus _focus;
    private readonly IWindowScopeLocator _locator;
    private readonly Func<TSlot> _create;
    private readonly List<SlotEntry> _slots = [];
    private readonly Dictionary<IDocumentWorkspace, WorkspaceWatch> _watched = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TSlot, WeakReference<Control>> _owners = new(ReferenceEqualityComparer.Instance);
    private long _clock;

    public WindowScopedSlots(IShellWindowFocus focus, IWindowScopeLocator locator, Func<TSlot> create)
    {
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _create = create ?? throw new ArgumentNullException(nameof(create));
        focus.FocusChanged += (_, _) => FocusChanged?.Invoke(this, EventArgs.Empty);
        focus.Entered += (_, scope) => Watch(scope.Workspace);
        focus.Left += (_, scope) => Drop(scope.Workspace);
        foreach (var scope in focus.Scopes) Watch(scope.Workspace);
    }

    public event EventHandler? FocusChanged;

    public event EventHandler<TSlot>? SlotCreated;

    public event EventHandler<TSlot>? SlotDropped;

    public TSlot Current => CurrentOf(_focus.Current.Workspace);

    public IReadOnlyList<TSlot> All => [.. _slots.Select(entry => entry.Slot)];

    public TSlot SlotFor(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_locator.ScopeShowing(owner) is not { } scope) return Current;
        var slot = SlotOf(scope.Workspace, _locator.DocumentShowing(scope, owner)?.View);
        Touch(slot);
        return slot;
    }

    public Control? OwnerOf(TSlot slot) =>
        _owners.TryGetValue(slot, out var owner) && owner.TryGetTarget(out var control) ? control : null;

    public void Own(TSlot slot, Control? owner)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (owner is null) _owners.Remove(slot);
        else _owners[slot] = new WeakReference<Control>(owner);
    }

    private TSlot CurrentOf(IDocumentWorkspace workspace)
    {
        if (workspace.Active?.View is { } view && Find(workspace, view) is { } shown) return shown.Slot;
        var latest = _slots.Where(entry => ReferenceEquals(entry.Workspace, workspace)).MaxBy(entry => entry.Used);
        return latest?.Slot ?? SlotOf(workspace, null);
    }

    private SlotEntry? Find(IDocumentWorkspace workspace, Control? view) =>
        _slots.FirstOrDefault(entry => ReferenceEquals(entry.Workspace, workspace) && ReferenceEquals(entry.View, view));

    private TSlot SlotOf(IDocumentWorkspace workspace, Control? view)
    {
        if (Find(workspace, view) is { } known) return known.Slot;
        var slot = _create();
        _slots.Add(new SlotEntry(workspace, view, slot) { Used = ++_clock });
        SlotCreated?.Invoke(this, slot);
        return slot;
    }

    private void Touch(TSlot slot)
    {
        if (_slots.FirstOrDefault(entry => ReferenceEquals(entry.Slot, slot)) is { } entry) entry.Used = ++_clock;
    }

    private void Watch(IDocumentWorkspace workspace)
    {
        if (_watched.ContainsKey(workspace)) return;
        var watch = new WorkspaceWatch(
            (_, _) => OnActiveChanged(workspace),
            (_, document) => DropView(workspace, document.View));
        _watched[workspace] = watch;
        workspace.ActiveChanged += watch.ActiveChanged;
        workspace.DocumentClosed += watch.ViewGone;
        workspace.DocumentDetached += watch.ViewGone;
    }

    private void Unwatch(IDocumentWorkspace workspace)
    {
        if (!_watched.Remove(workspace, out var watch)) return;
        workspace.ActiveChanged -= watch.ActiveChanged;
        workspace.DocumentClosed -= watch.ViewGone;
        workspace.DocumentDetached -= watch.ViewGone;
    }

    private void OnActiveChanged(IDocumentWorkspace workspace)
    {
        if (ReferenceEquals(workspace, _focus.Current.Workspace)) FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DropView(IDocumentWorkspace workspace, Control view)
    {
        if (Find(workspace, view) is not { } entry) return;
        Remove(entry);
        OnActiveChanged(workspace);
    }

    private void Drop(IDocumentWorkspace workspace)
    {
        Unwatch(workspace);
        foreach (var entry in _slots.Where(entry => ReferenceEquals(entry.Workspace, workspace)).ToList()) Remove(entry);
    }

    private void Remove(SlotEntry entry)
    {
        _slots.Remove(entry);
        _owners.Remove(entry.Slot);
        SlotDropped?.Invoke(this, entry.Slot);
    }

    private sealed record WorkspaceWatch(EventHandler<OpenDocument> ActiveChanged, EventHandler<OpenDocument> ViewGone);

    private sealed class SlotEntry(IDocumentWorkspace workspace, Control? view, TSlot slot)
    {
        public IDocumentWorkspace Workspace { get; } = workspace;

        public Control? View { get; } = view;

        public TSlot Slot { get; } = slot;

        public long Used { get; set; }
    }
}
