namespace PulseOffice.Drawing.Assets;

public static class AssetCatalog
{
    public static readonly IReadOnlyList<AssetPack> BundledIcons =
[
    new() { Id = "lucide", DisplayName = "Lucide", License = AssetLicense.Isc,
                Kind = AssetPackKind.Icons, IsMonochrome = true, IsBundled = true,
                HomepageUrl = "https://lucide.dev", ItemCount = 1500 },

        new() { Id = "tabler", DisplayName = "Tabler Icons", License = AssetLicense.Mit,
                Kind = AssetPackKind.Icons, IsMonochrome = true, IsBundled = true,
                HomepageUrl = "https://tabler.io/icons", ItemCount = 5800 },

        new() { Id = "material-symbols", DisplayName = "Material Symbols", License = AssetLicense.Apache2,
                Kind = AssetPackKind.Icons, IsMonochrome = true, IsBundled = true,
                HomepageUrl = "https://fonts.google.com/icons", ItemCount = 3000 },

        new() { Id = "phosphor", DisplayName = "Phosphor", License = AssetLicense.Mit,
                Kind = AssetPackKind.Icons, IsMonochrome = true, IsBundled = true,
                HomepageUrl = "https://phosphoricons.com", ItemCount = 9000 },
    ];

    public static readonly IReadOnlyList<AssetPack> BundledStickers =
[
    new() { Id = "twemoji", DisplayName = "Twemoji", Kind = AssetPackKind.Emoji, IsBundled = true,
                License = AssetLicense.CcBy4 with
                {
                    AttributionText = "Twemoji graphics © Twitter, Inc. and contributors, CC BY 4.0",
                    SourceUrl = "https://github.com/jdecked/twemoji",
                },
                ItemCount = 3600 },

        new() { Id = "noto-emoji", DisplayName = "Noto Emoji", Kind = AssetPackKind.Emoji, IsBundled = true,
                License = AssetLicense.Ofl with { SourceUrl = "https://github.com/googlefonts/noto-emoji" },
                ItemCount = 3600 },
    ];

    public static readonly IReadOnlyList<AssetPack> OptionalPacks =
[
    new() { Id = "openmoji", DisplayName = "OpenMoji", Kind = AssetPackKind.Stickers, IsBundled = false,
                License = AssetLicense.CcBySa4 with
                {
                    AttributionText = "OpenMoji — the open-source emoji project of HfG Schwäbisch Gmünd, CC BY-SA 4.0",
                    SourceUrl = "https://openmoji.org",
                },
                ItemCount = 4000 },
    ];

    public static IEnumerable<AssetPack> All => BundledIcons.Concat(BundledStickers).Concat(OptionalPacks);

    public static IEnumerable<AssetPack> RequiringAttribution =>
    All.Where(p => p.IsBundled && p.License.RequiresAttribution);
}
