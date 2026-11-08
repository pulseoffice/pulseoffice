using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Shapes;

public sealed class ShapeIconCatalog : IIconCatalog
{
    private const double Inset = 3;

    private readonly Dictionary<string, IconGeometry> _icons;

    public ShapeIconCatalog(IShapeGeometry geometry, IShapePathWriter writer)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(writer);

        var side = IconGeometry.Grid - 2 * Inset;

        _icons = new Dictionary<string, IconGeometry>(StringComparer.Ordinal);

        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var path = writer.Write(Shifted(geometry.Outline(kind).Scaled(side, side)));
            var icon = new IconGeometry(path) { Filled = kind is not ShapeKind.Line };

            _icons[$"ledger.shape.{ShapeGalleryView.Key(kind)}"] = icon;
            _icons[$"shape.{ShapeGalleryView.Key(kind)}"] = icon;
        }

        _icons["ledger.shape.gallery"] = _icons["shape.star5"];
    }

    public ShapeIconCatalog() : this(new ShapeGeometry(), new ShapePathWriter()) { }

    public IconGeometry? Find(string key) =>
        _icons.TryGetValue(key, out var geometry) ? geometry : null;

    public bool Contains(string key) => _icons.ContainsKey(key);

    public IReadOnlyCollection<string> Keys => _icons.Keys;

    private ShapeOutline Shifted(ShapeOutline outline) =>
        new SegmentMapper(point => new(point.X + Inset, point.Y + Inset)).Map(outline);
}
