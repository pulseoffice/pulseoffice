namespace PulseOffice.UI.Controls.Sorting;

public sealed record SortLevel(int Column, bool Descending = false, int CustomOrder = -1);

public sealed record SortLevelsRequest(string TitleKey, IReadOnlyList<string> Headers, IReadOnlyList<string> Letters)
{
    public bool HasHeader { get; init; } = true;

    public bool OffersHeaderChoice { get; init; } = true;

    public bool CaseSensitive { get; init; }

    public bool OffersCase { get; init; } = true;

    public IReadOnlyList<string> CustomOrders { get; init; } = [];

    public IReadOnlyList<SortLevel> Initial { get; init; } = [];

    public string? ExplanationKey { get; init; }

    public IReadOnlyList<string> RowHeaders { get; init; } = [];

    public IReadOnlyList<string> RowLetters { get; init; } = [];

    public bool LeftToRight { get; init; }

    public bool OffersDirection => RowLetters.Count > 0;
}

public sealed record SortLevelsChoice(IReadOnlyList<SortLevel> Levels, bool HasHeader, bool CaseSensitive)
{
    public bool LeftToRight { get; init; }
}

public sealed class SortLevelsModel
{
    private readonly List<SortLevel> _levels;

    public SortLevelsModel(int columns, IEnumerable<SortLevel>? initial = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        Columns = columns;
        _levels = [.. (initial ?? []).Where(level => level.Column >= 0 && level.Column < columns)];
        if (_levels.Count == 0) _levels.Add(new SortLevel(0));
        Selected = 0;
    }

    public event EventHandler? Changed;

    public event EventHandler? SelectionChanged;

    public event EventHandler? Edited;

    public int Columns { get; private set; }

    public IReadOnlyList<SortLevel> Levels => _levels;

    public int Selected { get; private set; }

    public bool CanAdd => _levels.Count < Columns;

    public bool CanRemove => _levels.Count > 1;

    public bool CanMoveUp => Selected > 0;

    public bool CanMoveDown => Selected < _levels.Count - 1;

    public void Reset(int columns, IEnumerable<SortLevel>? initial = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        Columns = columns;
        _levels.Clear();
        _levels.AddRange((initial ?? []).Where(level => level.Column >= 0 && level.Column < columns));
        if (_levels.Count == 0) _levels.Add(new SortLevel(0));
        Selected = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Select(int index)
    {
        var clamped = Math.Clamp(index, 0, _levels.Count - 1);
        if (clamped == Selected) return;
        Selected = clamped;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Set(int index, SortLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (index < 0 || index >= _levels.Count || _levels[index] == level) return;
        _levels[index] = level;
        Edited?.Invoke(this, EventArgs.Empty);
    }

    public void Add()
    {
        if (!CanAdd) return;
        var unused = Enumerable.Range(0, Columns).First(column => _levels.All(level => level.Column != column));
        Insert(new SortLevel(unused));
    }

    public void CopySelected()
    {
        if (!CanAdd) return;
        Insert(_levels[Selected]);
    }

    public void RemoveSelected()
    {
        if (!CanRemove) return;
        _levels.RemoveAt(Selected);
        Selected = Math.Min(Selected, _levels.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MoveUp() => Move(-1);

    public void MoveDown() => Move(1);

    public bool HasDuplicates => _levels.GroupBy(level => level.Column).Any(group => group.Count() > 1);

    private void Insert(SortLevel level)
    {
        _levels.Insert(Selected + 1, level);
        Selected++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Move(int delta)
    {
        var target = Selected + delta;
        if (target < 0 || target >= _levels.Count) return;
        (_levels[Selected], _levels[target]) = (_levels[target], _levels[Selected]);
        Selected = target;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
