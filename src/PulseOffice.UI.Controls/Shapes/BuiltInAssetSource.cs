using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Shapes;

public sealed class BuiltInAssetSource : IAssetSource
{
    public const string IconsPackId = "pulseoffice-icons";
    public const string ShapesPackId = "pulseoffice-shapes";

    private readonly IIconCatalog _icons;
    private readonly IShapeGeometry _geometry;
    private readonly IShapePathWriter _writer;
    private readonly IShapePresets _presets;

    public BuiltInAssetSource(
        IIconCatalog icons, IShapeGeometry geometry, IShapePathWriter writer, IShapePresets presets)
    {
        _icons = icons;
        _geometry = geometry;
        _writer = writer;
        _presets = presets;
    }

    public BuiltInAssetSource()
        : this(new IconCatalog(), new ShapeGeometry(), new ShapePathWriter(), new ShapePresets()) { }

    public IReadOnlyCollection<AssetPack> Packs =>
    [
        new()
        {
            Id = IconsPackId,
            DisplayName = "PulseOffice",
            Kind = AssetPackKind.Icons,
            License = AssetLicense.Apache2,
            IsMonochrome = true,
            IsBundled = true,
            ItemCount = _icons.Keys.Count,
        },
        new()
        {
            Id = ShapesPackId,
            DisplayName = "PulseOffice",
            Kind = AssetPackKind.Shapes,
            License = AssetLicense.Apache2,
            IsMonochrome = true,
            IsBundled = true,
            ItemCount = Enum.GetValues<ShapeKind>().Length,
        },
    ];

    public IReadOnlyList<VectorAsset> Items(string packId) => packId switch
    {
        IconsPackId => IconItems(),
        ShapesPackId => ShapeItems(),
        _ => [],
    };

    private IReadOnlyList<VectorAsset> IconItems()
    {
        var items = new List<VectorAsset>();

        foreach (var key in _icons.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            if (_icons.Find(key) is not { } geometry) continue;

            items.Add(Asset(IconsPackId, key, Document(geometry.Path, geometry.Filled)));
        }

        return items;
    }

    private IReadOnlyList<VectorAsset> ShapeItems()
    {
        var items = new List<VectorAsset>();

        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var path = _writer.Write(
                _geometry.Outline(kind).Scaled(IconGeometry.Grid, IconGeometry.Grid));

            items.Add(Asset(ShapesPackId, _presets.OdfName(kind), Document(path, filled: true)));
        }

        return items;
    }

    private static VectorAsset Asset(string packId, string itemId, string svg) => new()
    {
        Id = $"{packId}/{itemId}",
        Name = itemId.Replace('.', ' ').Replace('-', ' '),
        SourceKind = VectorSourceKind.InlineSvg,
        SvgContent = svg,
        PackId = packId,
        ItemId = itemId,
        IsRecolorable = true,
        Keywords = [.. itemId.Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries)],
    };

    private static string Document(string path, bool filled)
    {
        var size = IconGeometry.Grid.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var paint = filled
            ? """fill="currentColor" stroke="none" """
            : """fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" """;

        return $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}"><path d="{path}" {paint}/></svg>""";
    }
}
