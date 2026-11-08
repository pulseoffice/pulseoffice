using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public enum ChangeState { Pending, Accepted, Rejected }

public sealed record TrackedChange
{
    public required string Id { get; init; }

    public required int SheetIndex { get; init; }

    public required CellAddress Address { get; init; }

    public required CellValue Before { get; init; }

    public required CellValue After { get; init; }

    public string? FormulaBefore { get; init; }

    public string? FormulaAfter { get; init; }

    public string? Author { get; init; }

    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;

    public ChangeState State { get; init; } = ChangeState.Pending;

    public string? Comment { get; init; }
}

public sealed class ChangeLog
{
    private readonly List<TrackedChange> _changes = [];
    private Dictionary<(int Sheet, CellAddress Address), TrackedChange>? _pendingByCell;

    public IReadOnlyList<TrackedChange> Changes => _changes;

    public bool IsRecording { get; set; }

    public bool IsProtected { get; set; }

    public string? ProtectionHash { get; set; }

    public bool AcceptsProtection(string? password) =>
        ProtectionHash is null || (password is not null && WorkbookProtection.Hash(password) == ProtectionHash);

    public ChangeLogMemento Save() => new([.. _changes], ShowChanges);

    public void Restore(ChangeLogMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        _changes.Clear();
        _changes.AddRange(memento.Changes);
        ShowChanges = memento.ShowChanges;
        Invalidate();
    }

    public bool ShowChanges { get; set; } = true;

    public int PendingCount => _changes.Count(change => change.State == ChangeState.Pending);

    public void Record(TrackedChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!IsRecording) return;

        _changes.Add(change);
        Invalidate();
    }

    public void Import(IEnumerable<TrackedChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        _changes.AddRange(changes);
        Invalidate();
    }

    public void Annotate(TrackedChange change, string? comment)
    {
        ArgumentNullException.ThrowIfNull(change);

        var index = _changes.IndexOf(change);
        if (index < 0) return;

        _changes[index] = change with
        {
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
        };
        Invalidate();
    }

    public TrackedChange? At(int sheetIndex, CellAddress address)
    {
        if (_changes.Count == 0) return null;

        _pendingByCell ??= PendingByCell();
        return _pendingByCell.GetValueOrDefault((sheetIndex, address));
    }

    public bool Resolve(string id, ChangeState state)
    {
        var index = _changes.FindIndex(change => change.Id == id);
        if (index < 0) return false;

        _changes[index] = _changes[index] with { State = state };
        Invalidate();
        return true;
    }

    public void ResolveAll(ChangeState state)
    {
        for (var index = 0; index < _changes.Count; index++)
            if (_changes[index].State == ChangeState.Pending)
                _changes[index] = _changes[index] with { State = state };
        Invalidate();
    }

    public void Clear()
    {
        _changes.Clear();
        Invalidate();
    }

    public IReadOnlyList<TrackedChange> ReindexSheets(Func<int, int> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var detached = new List<TrackedChange>();
        var kept = new List<TrackedChange>(_changes.Count);
        foreach (var change in _changes)
        {
            var moved = map(change.SheetIndex);
            if (moved < 0) detached.Add(change);
            else kept.Add(moved == change.SheetIndex ? change : change with { SheetIndex = moved });
        }

        _changes.Clear();
        _changes.AddRange(kept);
        Invalidate();
        return detached;
    }

    public void Reattach(IEnumerable<TrackedChange> changes, int sheetIndex)
    {
        ArgumentNullException.ThrowIfNull(changes);

        _changes.AddRange(changes.Select(change => change with { SheetIndex = sheetIndex }));
        Invalidate();
    }

    public void MoveCells(int sheetIndex, Func<CellAddress, CellAddress?> move)
    {
        ArgumentNullException.ThrowIfNull(move);

        for (var index = _changes.Count - 1; index >= 0; index--)
        {
            var change = _changes[index];
            if (change.SheetIndex != sheetIndex) continue;

            if (move(change.Address) is not { } moved) _changes.RemoveAt(index);
            else if (moved != change.Address) _changes[index] = change with { Address = moved };
        }

        Invalidate();
    }

    public void Relocate(int fromSheet, int toSheet, Func<CellAddress, CellAddress?> move)
    {
        ArgumentNullException.ThrowIfNull(move);

        for (var index = 0; index < _changes.Count; index++)
        {
            var change = _changes[index];
            if (change.SheetIndex != fromSheet || move(change.Address) is not { } moved) continue;
            _changes[index] = change with { SheetIndex = toSheet, Address = moved };
        }

        Invalidate();
    }

    private void Invalidate() => _pendingByCell = null;

    private Dictionary<(int Sheet, CellAddress Address), TrackedChange> PendingByCell()
    {
        var lookup = new Dictionary<(int Sheet, CellAddress Address), TrackedChange>();
        foreach (var change in _changes)
            if (change.State == ChangeState.Pending)
                lookup[(change.SheetIndex, change.Address)] = change;
        return lookup;
    }
}

public sealed record ChangeLogMemento(
    IReadOnlyList<TrackedChange> Changes, bool ShowChanges);
