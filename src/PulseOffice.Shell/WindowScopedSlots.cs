using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PulseOffice.Shell;

public interface IWindowScopeLocator
{
    ShellWindowScope? ScopeShowing(Control owner);
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
    private readonly List<(IDocumentWorkspace Workspace, TSlot Slot)> _slots = [];
    private readonly Dictionary<TSlot, WeakReference<Control>> _owners = new(ReferenceEqualityComparer.Instance);

    public WindowScopedSlots(IShellWindowFocus focus, IWindowScopeLocator locator, Func<TSlot> create)
    {
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _create = create ?? throw new ArgumentNullException(nameof(create));
        focus.FocusChanged += (_, _) => FocusChanged?.Invoke(this, EventArgs.Empty);
        focus.Left += (_, scope) => Drop(scope.Workspace);
    }

    public event EventHandler? FocusChanged;

    public event EventHandler<TSlot>? SlotCreated;

    public event EventHandler<TSlot>? SlotDropped;

    public TSlot Current => SlotOf(_focus.Current.Workspace);

    public IReadOnlyList<TSlot> All => [.. _slots.Select(entry => entry.Slot)];

    public TSlot SlotFor(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _locator.ScopeShowing(owner) is { } scope ? SlotOf(scope.Workspace) : Current;
    }

    public Control? OwnerOf(TSlot slot) =>
        _owners.TryGetValue(slot, out var owner) && owner.TryGetTarget(out var control) ? control : null;

    public void Own(TSlot slot, Control? owner)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (owner is null) _owners.Remove(slot);
        else _owners[slot] = new WeakReference<Control>(owner);
    }

    private TSlot SlotOf(IDocumentWorkspace workspace)
    {
        foreach (var entry in _slots)
            if (ReferenceEquals(entry.Workspace, workspace)) return entry.Slot;
        var slot = _create();
        _slots.Add((workspace, slot));
        SlotCreated?.Invoke(this, slot);
        return slot;
    }

    private void Drop(IDocumentWorkspace workspace)
    {
        var index = _slots.FindIndex(entry => ReferenceEquals(entry.Workspace, workspace));
        if (index < 0) return;
        var slot = _slots[index].Slot;
        _slots.RemoveAt(index);
        _owners.Remove(slot);
        SlotDropped?.Invoke(this, slot);
    }
}
