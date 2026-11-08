namespace PulseOffice.UI.Commands.Shortcuts;

public enum ChordProblem
{
    None,
    NeedsModifier,
    Reserved,
}

public sealed record ShortcutCheck(ChordProblem Problem, IReadOnlyList<ShortcutEntry> Conflicts)
{
    public bool IsAccepted => Problem == ChordProblem.None && Conflicts.Count == 0;
}

public interface IShortcutDraftFactory
{
    ShortcutDraft Create(IReadOnlyList<ShortcutEntry> entries);
}

public sealed class ShortcutDraftFactory(IReservedShortcuts reserved) : IShortcutDraftFactory
{
    public ShortcutDraft Create(IReadOnlyList<ShortcutEntry> entries) => new(entries, reserved, new ShortcutScope());
}

public sealed class ShortcutDraft
{
    private readonly IReadOnlyList<ShortcutEntry> _entries;
    private readonly IReservedShortcuts _reserved;
    private readonly ShortcutScope _scope;
    private readonly GestureChordParser _parser = new();
    private readonly Dictionary<string, string?> _gestures;

    public ShortcutDraft(IReadOnlyList<ShortcutEntry> entries, IReservedShortcuts reserved, ShortcutScope scope)
    {
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _reserved = reserved ?? throw new ArgumentNullException(nameof(reserved));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _gestures = entries.ToDictionary(entry => entry.Id, entry => entry.Gesture, StringComparer.Ordinal);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ShortcutEntry> Entries => _entries;

    public string? GestureOf(string id) => _gestures.TryGetValue(id, out var gesture) ? gesture : null;

    public bool IsCustomized(string id) => !Same(GestureOf(id), Find(id).FactoryGesture);

    public bool IsEdited(string id) => !Same(GestureOf(id), Find(id).Gesture);

    public bool HasEdits => _entries.Any(entry => IsEdited(entry.Id));

    public bool HasCustomizations => _entries.Any(entry => IsCustomized(entry.Id));

    public ShortcutCheck Check(string id, GestureChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);

        if (!chord.HasCommandModifier && !chord.IsFunctionKey) return new ShortcutCheck(ChordProblem.NeedsModifier, []);
        if (_reserved.IsReserved(chord)) return new ShortcutCheck(ChordProblem.Reserved, []);

        return new ShortcutCheck(ChordProblem.None, ConflictsWith(Find(id), chord.Canonical()));
    }

    public IReadOnlyList<ShortcutEntry> ConflictsOf(string id) =>
        GestureOf(id) is { } gesture ? ConflictsWith(Find(id), gesture) : [];

    public bool Assign(string id, GestureChord chord, bool reassign)
    {
        var check = Check(id, chord);
        if (check.Problem != ChordProblem.None) return false;
        if (check.Conflicts.Count > 0 && !reassign) return false;

        foreach (var other in check.Conflicts) _gestures[other.Id] = null;

        _gestures[id] = chord.Canonical();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Remove(string id) => Set(id, null);

    public void RestoreDefault(string id) => Set(id, Find(id).FactoryGesture);

    public void RestoreAll()
    {
        foreach (var entry in _entries) _gestures[entry.Id] = entry.FactoryGesture;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyDictionary<string, string?> Edits() =>
        _entries.Where(entry => IsEdited(entry.Id)).ToDictionary(entry => entry.Id, entry => GestureOf(entry.Id), StringComparer.Ordinal);

    private void Set(string id, string? gesture)
    {
        _ = Find(id);
        _gestures[id] = gesture;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private ShortcutEntry Find(string id) =>
        _entries.FirstOrDefault(entry => entry.Id == id) ?? throw new KeyNotFoundException(id);

    private IReadOnlyList<ShortcutEntry> ConflictsWith(ShortcutEntry owner, string gesture) =>
        _parser.Parse(gesture) is null ? [] :
    [
        .. _entries.Where(entry => entry.Id != owner.Id
            && _scope.ActiveTogether(owner.Module, entry.Module)
            && Same(GestureOf(entry.Id), gesture)),
    ];

    private bool Same(string? first, string? second)
    {
        var a = _parser.Parse(first)?.Canonical();
        var b = _parser.Parse(second)?.Canonical();
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
