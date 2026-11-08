using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed record ColorScheme
{
    public uint Background { get; init; } = 0xFFFFFFFF;
    public uint Text { get; init; } = 0xFF1A1A1A;
    public uint Background2 { get; init; } = 0xFFF2F4F7;
    public uint Text2 { get; init; } = 0xFF44506A;
    public uint Accent1 { get; init; } = 0xFF2563EB;
    public uint Accent2 { get; init; } = 0xFF16A34A;
    public uint Accent3 { get; init; } = 0xFFDC2626;
    public uint Accent4 { get; init; } = 0xFFEA580C;
    public uint Accent5 { get; init; } = 0xFF7C3AED;
    public uint Accent6 { get; init; } = 0xFF0891B2;
    public uint Hyperlink { get; init; } = 0xFF2563EB;
    public uint FollowedHyperlink { get; init; } = 0xFF7C3AED;

    public uint Resolve(ColorRole role) => role switch
    {
        ColorRole.Background => Background,
        ColorRole.Text => Text,
        ColorRole.Background2 => Background2,
        ColorRole.Text2 => Text2,
        ColorRole.Accent1 => Accent1,
        ColorRole.Accent2 => Accent2,
        ColorRole.Accent3 => Accent3,
        ColorRole.Accent4 => Accent4,
        ColorRole.Accent5 => Accent5,
        ColorRole.Accent6 => Accent6,
        ColorRole.Hyperlink => Hyperlink,
        ColorRole.FollowedHyperlink => FollowedHyperlink,
        _ => Text,
    };
}

public sealed record FontScheme
{
    public string Major { get; init; } = "Inter";
    public string Minor { get; init; } = "Inter";
}

public enum ThemeDecorationKind
{
    None,
    SideBand,
    TopBar,
    BottomStripes,
    Corners,
    Arcs,
    Wash,
    Frame,
    Mosaic,
    Orbs,
    Slice,
}

public sealed record Theme : ITextTheme
{
    public required string Name { get; init; }

    public ThemeDecorationKind Decoration { get; init; } = ThemeDecorationKind.None;

    public ColorScheme Colors { get; init; } = new();

    public FontScheme Fonts { get; init; } = new();

    public uint Resolve(ColorRole role) => Colors.Resolve(role);

    public string MajorFont => Fonts.Major;

    public string MinorFont => Fonts.Minor;
}
