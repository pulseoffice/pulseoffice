using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Assets;

public enum VectorSourceKind
{
    InlineSvg,
    PackReference,
    FontGlyph,
    Outlines,
}

public sealed record AssetLicense(
    string SpdxId,
    string Name,
    bool RequiresAttribution,
    bool IsShareAlike,
    string? AttributionText = null,
    string? SourceUrl = null)
{
    public static readonly AssetLicense Mit = new("MIT", "MIT", false, false);
    public static readonly AssetLicense Isc = new("ISC", "ISC", false, false);
    public static readonly AssetLicense Apache2 = new("Apache-2.0", "Apache 2.0", false, false);
    public static readonly AssetLicense Cc0 = new("CC0-1.0", "CC0 1.0", false, false);
    public static readonly AssetLicense CcBy4 = new("CC-BY-4.0", "Creative Commons BY 4.0", true, false);
    public static readonly AssetLicense CcBySa4 = new("CC-BY-SA-4.0", "Creative Commons BY-SA 4.0", true, true);
    public static readonly AssetLicense Ofl = new("OFL-1.1", "SIL Open Font License 1.1", true, false);

    public bool IsSafeToBundle => !IsShareAlike;
}

public sealed record AssetPack
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required AssetLicense License { get; init; }
    public required AssetPackKind Kind { get; init; }

    public string? Version { get; init; }
    public string? HomepageUrl { get; init; }

    public bool IsMonochrome { get; init; }

    public bool IsBundled { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = [];
    public int ItemCount { get; init; }
}

public enum AssetPackKind { Icons, Stickers, Emoji, Clipart, Shapes, Arrows, Flags, Diagrams }

public sealed record VectorAsset
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required VectorSourceKind SourceKind { get; init; }

    public string? SvgContent { get; init; }

    public string? PackId { get; init; }
    public string? ItemId { get; init; }

    public GlyphOrigin? Glyph { get; init; }

    public DocumentSize NaturalSize { get; init; } = new(Twip.FromCentimeters(2), Twip.FromCentimeters(2));

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public bool IsRecolorable { get; init; }
}

public sealed record GlyphOrigin(string FontFamily, int CodePoint, string? StyleVariant = null);

public interface IAssetLibrary
{
    IReadOnlyCollection<AssetPack> Packs { get; }

    Task<IReadOnlyList<VectorAsset>> SearchAsync(
        string query, AssetPackKind? kind = null, string? packId = null, CancellationToken ct = default);

    Task<VectorAsset?> GetAsync(string packId, string itemId, CancellationToken ct = default);

    Task<VectorAsset> ResolveGlyphAsync(GlyphOrigin origin, CancellationToken ct = default);
}
