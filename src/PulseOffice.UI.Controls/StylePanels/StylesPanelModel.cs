namespace PulseOffice.UI.Controls.StylePanels;

public enum StyleListFilter
{
    All,
    Recent,
    Custom,
    Applied,
}

public sealed record StyleRow(StyleListEntry Entry, int Depth, bool IsCurrent);

public interface IStyleRecents
{
    IReadOnlyList<string> Of(string family);

    void Note(string family, string name);
}

public sealed class StyleRecents : IStyleRecents
{
    private const int Capacity = 12;

    private readonly Dictionary<string, List<string>> _recent = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Of(string family) => _recent.TryGetValue(family, out var names) ? [.. names] : [];

    public void Note(string family, string name)
    {
        if (!_recent.TryGetValue(family, out var names)) _recent[family] = names = [];
        names.Remove(name);
        names.Insert(0, name);
        if (names.Count > Capacity) names.RemoveRange(Capacity, names.Count - Capacity);
    }
}

public sealed class StylesPanelModel
{
    private readonly IStylesPanelSource _source;
    private readonly IStyleRecents _recents;
    private string? _followed;

    public StylesPanelModel(IStylesPanelSource source, IStyleRecents recents)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _recents = recents ?? throw new ArgumentNullException(nameof(recents));
        Family = source.Families.Count > 0 ? source.Families[0].Key : string.Empty;
        Refresh();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<StyleFamilyChoice> Families => _source.Families;

    public string Family { get; private set; }

    public StyleListFilter Filter { get; private set; }

    public string Search { get; private set; } = string.Empty;

    public IReadOnlyList<StyleRow> Rows { get; private set; } = [];

    public string? Selected { get; private set; }

    public string? Current { get; private set; }

    public string? PendingDelete { get; private set; }

    public IReadOnlyList<StyleListFilter> Filters =>
        Families.FirstOrDefault(choice => choice.Key == Family) is { TracksUsage: true }
            ? Enum.GetValues<StyleListFilter>()
            : [StyleListFilter.All, StyleListFilter.Recent, StyleListFilter.Custom];

    public StyleListEntry? SelectedEntry => Rows.FirstOrDefault(row => row.Entry.Name == Selected)?.Entry;

    public bool CanApply => Selected is not null && _source.CanApply(Family);

    public bool Can(StylePanelAction action) => action switch
    {
        StylePanelAction.New or StylePanelAction.Update => _source.Can(action, Family, Selected),
        _ => Selected is not null && _source.Can(action, Family, Selected),
    };

    public void Choose(string family)
    {
        if (Families.All(choice => choice.Key != family) || family == Family) return;
        Family = family;
        _followed = null;
        Selected = null;
        PendingDelete = null;
        if (!Filters.Contains(Filter)) Filter = StyleListFilter.All;
        Refresh();
    }

    public void SetFilter(StyleListFilter filter)
    {
        if (!Filters.Contains(filter) || filter == Filter) return;
        Filter = filter;
        Refresh();
    }

    public void SetSearch(string? search)
    {
        var text = search?.Trim() ?? string.Empty;
        if (text == Search) return;
        Search = text;
        Refresh();
    }

    public void Select(string? name)
    {
        if (name == Selected) return;
        if (name is not null && Rows.All(row => row.Entry.Name != name)) return;
        Selected = name;
        PendingDelete = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Apply()
    {
        if (!CanApply || Selected is not { } name) return false;
        _source.Apply(Family, name);
        _recents.Note(Family, name);
        Refresh();
        return true;
    }

    public bool RequestDelete()
    {
        if (!Can(StylePanelAction.Delete)) return false;
        PendingDelete = Selected;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void CancelDelete()
    {
        if (PendingDelete is null) return;
        PendingDelete = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> ConfirmDeleteAsync(CancellationToken ct)
    {
        if (PendingDelete is not { } name) return false;
        PendingDelete = null;
        await _source.RunAsync(StylePanelAction.Delete, Family, name, ct).ConfigureAwait(true);
        if (Selected == name) Selected = null;
        Refresh();
        return true;
    }

    public async Task<bool> RunAsync(StylePanelAction action, CancellationToken ct)
    {
        if (action == StylePanelAction.Delete) return RequestDelete();
        if (!Can(action)) return false;
        await _source.RunAsync(action, Family, Selected, ct).ConfigureAwait(true);
        Refresh();
        return true;
    }

    public void Refresh()
    {
        if (Families.Count > 0 && Families.All(choice => choice.Key != Family))
        {
            Family = Families[0].Key;
            Selected = null;
        }
        if (!Filters.Contains(Filter)) Filter = StyleListFilter.All;
        Current = _source.Current(Family);
        var follow = Current != _followed;
        _followed = Current;
        Rows = Build(_source.Styles(Family));
        if (follow && Current is not null && Rows.Any(row => row.Entry.Name == Current)) Selected = Current;
        if (Selected is not null && Rows.All(row => row.Entry.Name != Selected)) Selected = null;
        if (PendingDelete is not null && Rows.All(row => row.Entry.Name != PendingDelete)) PendingDelete = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<StyleRow> Build(IReadOnlyList<StyleListEntry> entries)
    {
        var flat = Filter != StyleListFilter.All || Search.Length > 0;
        IEnumerable<StyleListEntry> chosen = Filter switch
        {
            StyleListFilter.Custom => entries.Where(entry => entry.Custom),
            StyleListFilter.Applied => entries.Where(entry => entry.InUse || entry.Name == Current),
            StyleListFilter.Recent => Recent(entries),
            _ => entries,
        };
        if (Search.Length > 0)
            chosen = chosen.Where(entry => entry.Title.Contains(Search, StringComparison.CurrentCultureIgnoreCase)
                || entry.Name.Contains(Search, StringComparison.CurrentCultureIgnoreCase));
        return [.. chosen.Select(entry => new StyleRow(entry, flat ? 0 : entry.Depth, entry.Name == Current))];
    }

    private IEnumerable<StyleListEntry> Recent(IReadOnlyList<StyleListEntry> entries)
    {
        var byName = entries.GroupBy(entry => entry.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var names = _recents.Of(Family).ToList();
        if (Current is not null && !names.Contains(Current)) names.Add(Current);
        return names.Where(byName.ContainsKey).Select(name => byName[name]);
    }
}
