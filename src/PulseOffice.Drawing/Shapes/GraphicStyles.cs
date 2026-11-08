namespace PulseOffice.Drawing.Shapes;

public sealed record GraphicStyle(string Name, ShapeStyle Style);

public sealed class GraphicStyleCatalog
{
    private readonly List<GraphicStyle> _styles = [];

    public IReadOnlyList<GraphicStyle> All => _styles;

    public GraphicStyle? Find(string? name) =>
        name is null ? null : _styles.FirstOrDefault(style => string.Equals(style.Name, name, StringComparison.Ordinal));

    public void Set(GraphicStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        var index = _styles.FindIndex(existing => existing.Name == style.Name);
        if (index >= 0) _styles[index] = style;
        else _styles.Add(style);
    }

    public bool Rename(string name, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var index = _styles.FindIndex(existing => existing.Name == name);
        if (index < 0 || (newName != name && Find(newName) is not null)) return false;
        _styles[index] = _styles[index] with { Name = newName };
        return true;
    }

    public bool Remove(string name) => _styles.RemoveAll(style => style.Name == name) > 0;

    public void Restore(IReadOnlyList<GraphicStyle> styles)
    {
        _styles.Clear();
        _styles.AddRange(styles);
    }
}
