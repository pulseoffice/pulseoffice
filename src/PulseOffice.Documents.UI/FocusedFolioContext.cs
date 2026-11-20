using Avalonia.Controls;
using PulseOffice.Shell;

namespace PulseOffice.Documents.UI;

public sealed class FocusedFolioContext : IActiveFolioContext
{
    private readonly IWindowScopedSlots<ActiveFolioContext> _slots;

    public FocusedFolioContext(IWindowScopedSlots<ActiveFolioContext> slots)
    {
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        foreach (var slot in slots.All) slot.Changed += OnSlotChanged;
        slots.SlotCreated += (_, slot) => slot.Changed += OnSlotChanged;
        slots.SlotDropped += (_, slot) => slot.Changed -= OnSlotChanged;
        slots.FocusChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public FolioTarget? Target => _slots.Current.Target;

    public event EventHandler? Changed;

    private void OnSlotChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _slots.Current)) Changed?.Invoke(this, e);
    }

    public void Attach(FolioTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        (Holding(target) ?? _slots.Current).Attach(target);
    }

    public void Attach(Control owner, FolioTarget target)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(target);
        var slot = _slots.SlotFor(owner);
        foreach (var other in _slots.All)
            if (!ReferenceEquals(other, slot) && other.Target is { } held && ReferenceEquals(held, target)) other.Detach(held);
        slot.Attach(target);
    }

    public void Detach(FolioTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach (var slot in _slots.All) slot.Detach(target);
    }

    private ActiveFolioContext? Holding(FolioTarget target) =>
        _slots.All.FirstOrDefault(slot => ReferenceEquals(slot.Target, target));
}
