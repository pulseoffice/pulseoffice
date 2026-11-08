using DocumentFormat.OpenXml;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml;

internal enum ThemeSlot { Dark1, Light1, Dark2, Light2 }

internal sealed record OoxmlColorMap(ThemeSlot Background1, ThemeSlot Text1, ThemeSlot Background2, ThemeSlot Text2)
{
    public static OoxmlColorMap Standard { get; } = new(ThemeSlot.Light1, ThemeSlot.Dark1, ThemeSlot.Light2, ThemeSlot.Dark2);

    public static OoxmlColorMap Inverted { get; } = new(ThemeSlot.Dark1, ThemeSlot.Light1, ThemeSlot.Dark2, ThemeSlot.Light2);

    public ColorRole RoleOf(ThemeSlot slot) =>
        slot == Background1 ? ColorRole.Background
        : slot == Text1 ? ColorRole.Text
        : slot == Background2 ? ColorRole.Background2
        : slot == Text2 ? ColorRole.Text2
        : slot is ThemeSlot.Light1 or ThemeSlot.Light2 ? ColorRole.Background : ColorRole.Text;

    public ThemeSlot SlotOf(ColorRole role) => role switch
    {
        ColorRole.Background => Background1,
        ColorRole.Text => Text1,
        ColorRole.Background2 => Background2,
        _ => Text2,
    };
}

internal sealed class ColorMapCodec
{
    private readonly IReadOnlyList<(ThemeSlot Slot, D.ColorSchemeIndexValues Value)> _slots =
    [
        (ThemeSlot.Dark1, D.ColorSchemeIndexValues.Dark1),
        (ThemeSlot.Light1, D.ColorSchemeIndexValues.Light1),
        (ThemeSlot.Dark2, D.ColorSchemeIndexValues.Dark2),
        (ThemeSlot.Light2, D.ColorSchemeIndexValues.Light2),
    ];

    public OoxmlColorMap Read(OpenXmlElement? map, OoxmlColorMap fallback) => map is null
        ? fallback
        : new OoxmlColorMap(
            Slot(map, "bg1", fallback.Background1),
            Slot(map, "tx1", fallback.Text1),
            Slot(map, "bg2", fallback.Background2),
            Slot(map, "tx2", fallback.Text2));

    public OoxmlColorMap Override(OoxmlColorMap master, P.ColorMapOverride? map) =>
        map?.OverrideColorMapping is { } own ? Read(own, master) : master;

    public OoxmlColorMap For(ColorScheme colors) =>
        Luminance(colors.Background) < Luminance(colors.Text) ? OoxmlColorMap.Inverted : OoxmlColorMap.Standard;

    public P.ColorMap Build(OoxmlColorMap map) => new()
    {
        Background1 = Value(map.Background1),
        Text1 = Value(map.Text1),
        Background2 = Value(map.Background2),
        Text2 = Value(map.Text2),
        Accent1 = D.ColorSchemeIndexValues.Accent1,
        Accent2 = D.ColorSchemeIndexValues.Accent2,
        Accent3 = D.ColorSchemeIndexValues.Accent3,
        Accent4 = D.ColorSchemeIndexValues.Accent4,
        Accent5 = D.ColorSchemeIndexValues.Accent5,
        Accent6 = D.ColorSchemeIndexValues.Accent6,
        Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
        FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
    };

    public uint Color(ThemeSlot slot, ColorScheme physical) => slot switch
    {
        ThemeSlot.Dark1 => physical.Text,
        ThemeSlot.Light1 => physical.Background,
        ThemeSlot.Dark2 => physical.Text2,
        _ => physical.Background2,
    };

    public ColorScheme Logical(ColorScheme physical, OoxmlColorMap map) => physical with
    {
        Background = Color(map.Background1, physical),
        Text = Color(map.Text1, physical),
        Background2 = Color(map.Background2, physical),
        Text2 = Color(map.Text2, physical),
    };

    public ColorScheme Physical(ColorScheme logical, OoxmlColorMap map)
    {
        uint Of(ThemeSlot slot) => slot == map.Background1 ? logical.Background
            : slot == map.Text1 ? logical.Text
            : slot == map.Background2 ? logical.Background2
            : logical.Text2;
        return logical with
        {
            Text = Of(ThemeSlot.Dark1),
            Background = Of(ThemeSlot.Light1),
            Text2 = Of(ThemeSlot.Dark2),
            Background2 = Of(ThemeSlot.Light2),
        };
    }

    private ThemeSlot Slot(OpenXmlElement map, string name, ThemeSlot fallback)
    {
        var value = map.GetAttributes().FirstOrDefault(a => a.LocalName == name && string.IsNullOrEmpty(a.NamespaceUri)).Value;
        return value switch
        {
            "dk1" => ThemeSlot.Dark1,
            "lt1" => ThemeSlot.Light1,
            "dk2" => ThemeSlot.Dark2,
            "lt2" => ThemeSlot.Light2,
            _ => fallback,
        };
    }

    private D.ColorSchemeIndexValues Value(ThemeSlot slot) => _slots.First(entry => entry.Slot == slot).Value;

    private double Luminance(uint argb) =>
        0.2126 * ((argb >> 16) & 0xFF) + 0.7152 * ((argb >> 8) & 0xFF) + 0.0722 * (argb & 0xFF);
}
