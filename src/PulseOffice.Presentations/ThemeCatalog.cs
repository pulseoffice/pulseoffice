namespace PulseOffice.Presentations;

public interface IThemeCatalog
{
    IReadOnlyList<Theme> Themes { get; }

    Theme Default { get; }

    Theme? Find(string name);
}

public sealed class ThemeCatalog : IThemeCatalog
{
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chiaro"] = "Light",
        ["Ardesia"] = "Slate",
        ["Sabbia"] = "Sand",
    };

    private readonly List<Theme> _themes =
    [
        new Theme
        {
            Name = "Light",
            Decoration = ThemeDecorationKind.None,
            Fonts = new FontScheme { Major = "Inter", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Slate",
            Decoration = ThemeDecorationKind.SideBand,
            Colors = new ColorScheme
            {
                Background = 0xFF1B2230,
                Text = 0xFFEDF1F7,
                Background2 = 0xFF273041,
                Text2 = 0xFFAAB4C6,
                Accent1 = 0xFF60A5FA,
                Accent2 = 0xFF34D399,
                Accent3 = 0xFFF87171,
                Accent4 = 0xFFFBBF24,
                Accent5 = 0xFFA78BFA,
                Accent6 = 0xFF22D3EE,
                Hyperlink = 0xFF7DD3FC,
                FollowedHyperlink = 0xFFC4B5FD,
            },
            Fonts = new FontScheme { Major = "Inter", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Sand",
            Decoration = ThemeDecorationKind.BottomStripes,
            Colors = new ColorScheme
            {
                Background = 0xFFFBF7F0,
                Text = 0xFF2B2620,
                Background2 = 0xFFF0E8DA,
                Text2 = 0xFF6B5D48,
                Accent1 = 0xFFB45309,
                Accent2 = 0xFF15803D,
                Accent3 = 0xFF9F1239,
                Accent4 = 0xFF1D4ED8,
                Accent5 = 0xFF7C2D12,
                Accent6 = 0xFF0F766E,
                Hyperlink = 0xFF9A3412,
                FollowedHyperlink = 0xFF7C2D12,
            },
            Fonts = new FontScheme { Major = "Georgia", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Meridian",
            Decoration = ThemeDecorationKind.SideBand,
            Colors = new ColorScheme
            {
                Background = 0xFFFFFFFF,
                Text = 0xFF1F2937,
                Background2 = 0xFFEEF2F7,
                Text2 = 0xFF334E68,
                Accent1 = 0xFF1E3A8A,
                Accent2 = 0xFF0EA5E9,
                Accent3 = 0xFFF59E0B,
                Accent4 = 0xFF10B981,
                Accent5 = 0xFF6366F1,
                Accent6 = 0xFFEF4444,
                Hyperlink = 0xFF1D4ED8,
                FollowedHyperlink = 0xFF6D28D9,
            },
            Fonts = new FontScheme { Major = "Trebuchet MS", Minor = "Verdana" },
        },

        new Theme
        {
            Name = "Aurora",
            Decoration = ThemeDecorationKind.Wash,
            Colors = new ColorScheme
            {
                Background = 0xFF0B1020,
                Text = 0xFFE6EDF7,
                Background2 = 0xFF151B33,
                Text2 = 0xFFA5B4CF,
                Accent1 = 0xFF22D3EE,
                Accent2 = 0xFFA78BFA,
                Accent3 = 0xFF34D399,
                Accent4 = 0xFFF472B6,
                Accent5 = 0xFFFBBF24,
                Accent6 = 0xFF60A5FA,
                Hyperlink = 0xFF67E8F9,
                FollowedHyperlink = 0xFFC4B5FD,
            },
            Fonts = new FontScheme { Major = "Inter", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Horizon",
            Decoration = ThemeDecorationKind.Slice,
            Colors = new ColorScheme
            {
                Background = 0xFFFFFDF8,
                Text = 0xFF2D2A26,
                Background2 = 0xFFF6EFE3,
                Text2 = 0xFF6B5B45,
                Accent1 = 0xFFE76F51,
                Accent2 = 0xFFF4A261,
                Accent3 = 0xFF2A9D8F,
                Accent4 = 0xFF264653,
                Accent5 = 0xFFE9C46A,
                Accent6 = 0xFF8AB17D,
                Hyperlink = 0xFF1F7A70,
                FollowedHyperlink = 0xFF7A4E8C,
            },
            Fonts = new FontScheme { Major = "Trebuchet MS", Minor = "Trebuchet MS" },
        },

        new Theme
        {
            Name = "Prism",
            Decoration = ThemeDecorationKind.Corners,
            Colors = new ColorScheme
            {
                Background = 0xFFFFFFFF,
                Text = 0xFF111827,
                Background2 = 0xFFF3F4F6,
                Text2 = 0xFF4B5563,
                Accent1 = 0xFF7C3AED,
                Accent2 = 0xFFEC4899,
                Accent3 = 0xFFF59E0B,
                Accent4 = 0xFF10B981,
                Accent5 = 0xFF3B82F6,
                Accent6 = 0xFFEF4444,
                Hyperlink = 0xFF6D28D9,
                FollowedHyperlink = 0xFF9D174D,
            },
            Fonts = new FontScheme { Major = "Inter", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Atelier",
            Decoration = ThemeDecorationKind.Frame,
            Colors = new ColorScheme
            {
                Background = 0xFFFAF6EE,
                Text = 0xFF2B2420,
                Background2 = 0xFFEFE6D6,
                Text2 = 0xFF6E5E4E,
                Accent1 = 0xFF9C6B30,
                Accent2 = 0xFF5B7553,
                Accent3 = 0xFF8E3B46,
                Accent4 = 0xFF3E5C76,
                Accent5 = 0xFFC08552,
                Accent6 = 0xFF6D597A,
                Hyperlink = 0xFF3E5C76,
                FollowedHyperlink = 0xFF6D597A,
            },
            Fonts = new FontScheme { Major = "Georgia", Minor = "Georgia" },
        },

        new Theme
        {
            Name = "Nordic",
            Decoration = ThemeDecorationKind.TopBar,
            Colors = new ColorScheme
            {
                Background = 0xFFF7F9FB,
                Text = 0xFF1C2833,
                Background2 = 0xFFE6ECF1,
                Text2 = 0xFF4A5A6A,
                Accent1 = 0xFF2E5C8A,
                Accent2 = 0xFF7FA7C9,
                Accent3 = 0xFFC0392B,
                Accent4 = 0xFF6C8EAD,
                Accent5 = 0xFF3D7A5E,
                Accent6 = 0xFFD4A373,
                Hyperlink = 0xFF2E5C8A,
                FollowedHyperlink = 0xFF6B4C8A,
            },
            Fonts = new FontScheme { Major = "Arial", Minor = "Arial" },
        },

        new Theme
        {
            Name = "Ember",
            Decoration = ThemeDecorationKind.Arcs,
            Colors = new ColorScheme
            {
                Background = 0xFF1C1917,
                Text = 0xFFF5F0EB,
                Background2 = 0xFF292524,
                Text2 = 0xFFC8BBAE,
                Accent1 = 0xFFF97316,
                Accent2 = 0xFFEF4444,
                Accent3 = 0xFFFACC15,
                Accent4 = 0xFFFB923C,
                Accent5 = 0xFFA3E635,
                Accent6 = 0xFF38BDF8,
                Hyperlink = 0xFFFDBA74,
                FollowedHyperlink = 0xFFFCA5A5,
            },
            Fonts = new FontScheme { Major = "Trebuchet MS", Minor = "Trebuchet MS" },
        },

        new Theme
        {
            Name = "Lagoon",
            Decoration = ThemeDecorationKind.Wash,
            Colors = new ColorScheme
            {
                Background = 0xFFF4FBFB,
                Text = 0xFF133B3B,
                Background2 = 0xFFDDF1F0,
                Text2 = 0xFF2F6461,
                Accent1 = 0xFF0D9488,
                Accent2 = 0xFF0284C7,
                Accent3 = 0xFFF59E0B,
                Accent4 = 0xFF14B8A6,
                Accent5 = 0xFF6366F1,
                Accent6 = 0xFFEC4899,
                Hyperlink = 0xFF0369A1,
                FollowedHyperlink = 0xFF6D28D9,
            },
            Fonts = new FontScheme { Major = "Inter", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Citrus",
            Decoration = ThemeDecorationKind.Orbs,
            Colors = new ColorScheme
            {
                Background = 0xFFFFFFFF,
                Text = 0xFF1F2A1F,
                Background2 = 0xFFF2F7EC,
                Text2 = 0xFF4D5E44,
                Accent1 = 0xFF65A30D,
                Accent2 = 0xFFEAB308,
                Accent3 = 0xFFF97316,
                Accent4 = 0xFF16A34A,
                Accent5 = 0xFF0EA5E9,
                Accent6 = 0xFFDB2777,
                Hyperlink = 0xFF0369A1,
                FollowedHyperlink = 0xFF7E22CE,
            },
            Fonts = new FontScheme { Major = "Verdana", Minor = "Verdana" },
        },

        new Theme
        {
            Name = "Midnight",
            Decoration = ThemeDecorationKind.Frame,
            Colors = new ColorScheme
            {
                Background = 0xFF0F172A,
                Text = 0xFFE2E8F0,
                Background2 = 0xFF1E293B,
                Text2 = 0xFF94A3B8,
                Accent1 = 0xFF38BDF8,
                Accent2 = 0xFF818CF8,
                Accent3 = 0xFFF472B6,
                Accent4 = 0xFF34D399,
                Accent5 = 0xFFFBBF24,
                Accent6 = 0xFFF87171,
                Hyperlink = 0xFF7DD3FC,
                FollowedHyperlink = 0xFFC4B5FD,
            },
            Fonts = new FontScheme { Major = "Georgia", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Terracotta",
            Decoration = ThemeDecorationKind.BottomStripes,
            Colors = new ColorScheme
            {
                Background = 0xFFFBF5F0,
                Text = 0xFF3B2A22,
                Background2 = 0xFFF1E2D6,
                Text2 = 0xFF7A5646,
                Accent1 = 0xFFC2410C,
                Accent2 = 0xFF9A3412,
                Accent3 = 0xFFCA8A04,
                Accent4 = 0xFF4D7C0F,
                Accent5 = 0xFF0F766E,
                Accent6 = 0xFF7C2D12,
                Hyperlink = 0xFF9A3412,
                FollowedHyperlink = 0xFF5B21B6,
            },
            Fonts = new FontScheme { Major = "Georgia", Minor = "Verdana" },
        },

        new Theme
        {
            Name = "Orchid",
            Decoration = ThemeDecorationKind.Arcs,
            Colors = new ColorScheme
            {
                Background = 0xFFFFFFFF,
                Text = 0xFF2E1A3A,
                Background2 = 0xFFF6EFFA,
                Text2 = 0xFF6B4C7A,
                Accent1 = 0xFF9333EA,
                Accent2 = 0xFFDB2777,
                Accent3 = 0xFF6366F1,
                Accent4 = 0xFF0891B2,
                Accent5 = 0xFFF59E0B,
                Accent6 = 0xFF16A34A,
                Hyperlink = 0xFF7E22CE,
                FollowedHyperlink = 0xFF9D174D,
            },
            Fonts = new FontScheme { Major = "Trebuchet MS", Minor = "Trebuchet MS" },
        },

        new Theme
        {
            Name = "Graphite",
            Decoration = ThemeDecorationKind.TopBar,
            Colors = new ColorScheme
            {
                Background = 0xFF18181B,
                Text = 0xFFF4F4F5,
                Background2 = 0xFF27272A,
                Text2 = 0xFFA1A1AA,
                Accent1 = 0xFFEAB308,
                Accent2 = 0xFF22C55E,
                Accent3 = 0xFF3B82F6,
                Accent4 = 0xFFEF4444,
                Accent5 = 0xFFA855F7,
                Accent6 = 0xFF14B8A6,
                Hyperlink = 0xFFFDE047,
                FollowedHyperlink = 0xFFD8B4FE,
            },
            Fonts = new FontScheme { Major = "Arial", Minor = "Arial" },
        },

        new Theme
        {
            Name = "Botanical",
            Decoration = ThemeDecorationKind.Orbs,
            Colors = new ColorScheme
            {
                Background = 0xFFF7FAF5,
                Text = 0xFF1E2B1E,
                Background2 = 0xFFE4EEDF,
                Text2 = 0xFF4A6046,
                Accent1 = 0xFF2F6B3A,
                Accent2 = 0xFF8DB255,
                Accent3 = 0xFFD29C3F,
                Accent4 = 0xFF5E8C6A,
                Accent5 = 0xFFA0522D,
                Accent6 = 0xFF3E7CB1,
                Hyperlink = 0xFF2F6B3A,
                FollowedHyperlink = 0xFF6B4C8A,
            },
            Fonts = new FontScheme { Major = "Georgia", Minor = "Verdana" },
        },

        new Theme
        {
            Name = "Coral",
            Decoration = ThemeDecorationKind.Slice,
            Colors = new ColorScheme
            {
                Background = 0xFFFFFFFF,
                Text = 0xFF26323F,
                Background2 = 0xFFFFF1EE,
                Text2 = 0xFF5C6B7A,
                Accent1 = 0xFFF2545B,
                Accent2 = 0xFFF7A072,
                Accent3 = 0xFF2EC4B6,
                Accent4 = 0xFF3D5A80,
                Accent5 = 0xFFFFBF69,
                Accent6 = 0xFF7D5BA6,
                Hyperlink = 0xFF3D5A80,
                FollowedHyperlink = 0xFF7D5BA6,
            },
            Fonts = new FontScheme { Major = "Trebuchet MS", Minor = "Arial" },
        },

        new Theme
        {
            Name = "Blueprint",
            Decoration = ThemeDecorationKind.Frame,
            Colors = new ColorScheme
            {
                Background = 0xFF0B3C6B,
                Text = 0xFFF1F6FB,
                Background2 = 0xFF0E4A82,
                Text2 = 0xFFBCD3EA,
                Accent1 = 0xFF7DD3FC,
                Accent2 = 0xFFFDE68A,
                Accent3 = 0xFFFCA5A5,
                Accent4 = 0xFFA7F3D0,
                Accent5 = 0xFFC4B5FD,
                Accent6 = 0xFFFDBA74,
                Hyperlink = 0xFFBAE6FD,
                FollowedHyperlink = 0xFFDDD6FE,
            },
            Fonts = new FontScheme { Major = "Courier New", Minor = "Verdana" },
        },

        new Theme
        {
            Name = "Mosaic",
            Decoration = ThemeDecorationKind.Mosaic,
            Colors = new ColorScheme
            {
                Background = 0xFFFFFFFF,
                Text = 0xFF1F2937,
                Background2 = 0xFFF1F5F9,
                Text2 = 0xFF475569,
                Accent1 = 0xFF0EA5E9,
                Accent2 = 0xFF6366F1,
                Accent3 = 0xFFF43F5E,
                Accent4 = 0xFFF59E0B,
                Accent5 = 0xFF10B981,
                Accent6 = 0xFF8B5CF6,
                Hyperlink = 0xFF0369A1,
                FollowedHyperlink = 0xFF6D28D9,
            },
            Fonts = new FontScheme { Major = "Inter", Minor = "Inter" },
        },

        new Theme
        {
            Name = "Quartz",
            Decoration = ThemeDecorationKind.Corners,
            Colors = new ColorScheme
            {
                Background = 0xFFFDFBFF,
                Text = 0xFF2A2438,
                Background2 = 0xFFEEE8F7,
                Text2 = 0xFF625873,
                Accent1 = 0xFF8B5CF6,
                Accent2 = 0xFFEC4899,
                Accent3 = 0xFF06B6D4,
                Accent4 = 0xFFF59E0B,
                Accent5 = 0xFF10B981,
                Accent6 = 0xFFF43F5E,
                Hyperlink = 0xFF6D28D9,
                FollowedHyperlink = 0xFF9D174D,
            },
            Fonts = new FontScheme { Major = "Georgia", Minor = "Inter" },
        },
    ];

    public IReadOnlyList<Theme> Themes => _themes;

    public Theme Default => _themes[0];

    public Theme? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var canonical = _aliases.TryGetValue(name, out var alias) ? alias : name;
        return _themes.FirstOrDefault(t => string.Equals(t.Name, canonical, StringComparison.OrdinalIgnoreCase));
    }
}
