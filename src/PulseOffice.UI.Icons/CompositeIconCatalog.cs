namespace PulseOffice.UI.Icons;

public sealed class CompositeIconCatalog : IIconCatalog
{
    private readonly IReadOnlyList<IIconCatalog> _catalogs;

    public CompositeIconCatalog(IEnumerable<IIconCatalog> catalogs) => _catalogs = [.. catalogs];

    public IconGeometry? Find(string key)
    {
        foreach (var catalog in _catalogs)
            if (catalog.Find(key) is { } geometry) return geometry;

        return null;
    }

    public bool Contains(string key) => Find(key) is not null;

    public IReadOnlyCollection<string> Keys => [.. _catalogs.SelectMany(catalog => catalog.Keys).Distinct()];
}
