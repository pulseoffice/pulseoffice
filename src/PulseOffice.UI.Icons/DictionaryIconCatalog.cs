namespace PulseOffice.UI.Icons;

public abstract class DictionaryIconCatalog : IIconCatalog
{
    private readonly Dictionary<string, IconGeometry> _shapes;

    protected DictionaryIconCatalog(IEnumerable<(string Key, string Path)> shapes) =>
        _shapes = shapes.ToDictionary(entry => entry.Key, entry => new IconGeometry(entry.Path), StringComparer.Ordinal);

    public IconGeometry? Find(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return _shapes.TryGetValue(key, out var geometry) ? geometry : null;
    }

    public bool Contains(string key) => Find(key) is not null;

    public IReadOnlyCollection<string> Keys => _shapes.Keys;
}
