namespace PulseOffice.Sheets.Styling;

public sealed class StyleCatalog
{
    private readonly List<CellStyle> _styles = [CellStyle.Default];
    private readonly Dictionary<CellStyle, int> _index = new() { [CellStyle.Default] = 0 };

    public const int DefaultStyleId = 0;

    public IReadOnlyList<CellStyle> Styles => _styles;

    public CellStyle Get(int id) =>
        id >= 0 && id < _styles.Count ? _styles[id] : CellStyle.Default;

    public int Intern(CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        if (_index.TryGetValue(style, out var existing)) return existing;

        var id = _styles.Count;
        _styles.Add(style);
        _index[style] = id;

        return id;
    }

    public void CopyTo(StyleCatalog target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target._styles.Clear();
        target._styles.AddRange(_styles);
        target._index.Clear();
        foreach (var (style, id) in _index) target._index[style] = id;
    }

    public int Derive(int baseStyleId, Func<CellStyle, CellStyle> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return Intern(change(Get(baseStyleId)));
    }
}
