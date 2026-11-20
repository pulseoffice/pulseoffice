using Avalonia.Controls;
using PulseOffice.Shell;

namespace PulseOffice.Presentations.UI;

public sealed class FocusedDeckContext : IActiveDeckContext
{
    private readonly IWindowScopedSlots<ActiveDeckContext> _slots;

    public FocusedDeckContext(IWindowScopedSlots<ActiveDeckContext> slots)
    {
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        foreach (var slot in slots.All) slot.Changed += OnSlotChanged;
        slots.SlotCreated += (_, slot) => slot.Changed += OnSlotChanged;
        slots.SlotDropped += (_, slot) => slot.Changed -= OnSlotChanged;
        slots.FocusChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public DeckTarget? Target => _slots.Current.Target;

    public bool HasDeck => _slots.Current.HasDeck;

    public event EventHandler? Changed;

    private void OnSlotChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _slots.Current)) Changed?.Invoke(this, e);
    }

    public void Attach(DeckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var slot = Holding(target) ?? _slots.Current;
        if (!ReferenceEquals(slot.Target, target)) _slots.Own(slot, null);
        slot.Attach(target);
    }

    public void Attach(Control owner, DeckTarget target)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(target);
        var slot = _slots.SlotFor(owner);
        foreach (var other in _slots.All)
            if (!ReferenceEquals(other, slot) && other.Target is { } held && ReferenceEquals(_slots.OwnerOf(other), owner)) other.Detach(held);
        _slots.Own(slot, owner);
        slot.Attach(target);
    }

    public void Detach(DeckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach (var slot in _slots.All)
        {
            if (!ReferenceEquals(slot.Target, target)) continue;
            _slots.Own(slot, null);
            slot.Detach(target);
        }
    }

    public void Reannounce(DeckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach (var slot in _slots.All) slot.Reannounce(target);
    }

    private ActiveDeckContext? Holding(DeckTarget target) =>
        _slots.All.FirstOrDefault(slot => ReferenceEquals(slot.Target, target));
}
