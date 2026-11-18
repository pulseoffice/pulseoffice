using Avalonia;
using Avalonia.Media;

namespace PulseOffice.UI.Theming;

public static class Palette
{
    public static class Keys
    {
        public const string Canvas = "PulseCanvas";

        public const string Surface = "PulseSurface";

        public const string SurfaceRaised = "PulseSurfaceRaised";

        public const string SurfaceSunken = "PulseSurfaceSunken";

        public const string SurfaceOverlay = "PulseSurfaceOverlay";

        public const string Border = "PulseBorder";
        public const string BorderStrong = "PulseBorderStrong";
        public const string FieldBorder = "PulseFieldBorder";

        public const string TextPrimary = "PulseTextPrimary";
        public const string TextSecondary = "PulseTextSecondary";
        public const string TextDisabled = "PulseTextDisabled";

        public const string Hover = "PulseHover";

        public const string Pressed = "PulsePressed";

        public const string Scrim = "PulseScrim";

        public const string Info = "PulseInfo";

        public const string Danger = "PulseDanger";
        public const string Warning = "PulseWarning";
        public const string Success = "PulseSuccess";

        public const string Page = "PulsePage";
        public const string PageInk = "PulsePageInk";
        public const string PageGuide = "PulsePageGuide";
        public const string PageSample = "PulsePageSample";
    }

    public const string HighContrastFocus = "#FFFF00";

    public static void Apply(Application application, bool dark) =>
        Apply(application, dark ? PaletteVariant.Dark : PaletteVariant.Light);

    public static void Apply(Application application, PaletteVariant variant)
    {
        ArgumentNullException.ThrowIfNull(application);

        var colors = Table(variant);

        foreach (var (key, value) in colors)
            application.Resources[key] = new SolidColorBrush(Color.Parse(value));

        foreach (var (alias, key) in MenuSurfaceAliases)
            application.Resources[alias] = application.Resources[key];

        application.Resources["MenuFlyoutPresenterBorderThemeThickness"] = new Thickness(1);
        application.Resources["OverlayCornerRadius"] = new CornerRadius(12);
    }

    public static IReadOnlyDictionary<string, Color> Colors(bool dark) =>
        Colors(dark ? PaletteVariant.Dark : PaletteVariant.Light);

    public static IReadOnlyDictionary<string, Color> Colors(PaletteVariant variant) =>
        Table(variant).ToDictionary(entry => entry.Key, entry => Color.Parse(entry.Color));

    private static (string Key, string Color)[] Table(PaletteVariant variant) => variant switch
    {
        PaletteVariant.Dark => Dark,
        PaletteVariant.HighContrast => HighContrast,
        _ => Light,
    };

    public static IReadOnlyList<string> Backgrounds { get; } =
        [Keys.Canvas, Keys.Surface, Keys.SurfaceRaised, Keys.SurfaceSunken, Keys.SurfaceOverlay];

    private static readonly (string Alias, string Key)[] MenuSurfaceAliases =
    [
        ("MenuFlyoutPresenterBackground", Keys.SurfaceOverlay),
        ("MenuFlyoutPresenterBorderBrush", Keys.Border),
    ];

    private static readonly (string Key, string Color)[] Light =
[

    (Keys.Canvas, "#EDF0F5"),
        (Keys.Surface, "#FFFFFF"),
        (Keys.SurfaceRaised, "#F7F9FC"),
        (Keys.SurfaceSunken, "#EDF0F5"),
        (Keys.SurfaceOverlay, "#FFFFFF"),

        (Keys.Border, "#DFE4EC"),
        (Keys.BorderStrong, "#C6CEDA"),
        (Keys.FieldBorder, "#808A99"),

        (Keys.TextPrimary, "#111827"),
        (Keys.TextSecondary, "#5F6979"),
        (Keys.TextDisabled, "#9CA3AF"),

        (Keys.Hover, "#0D0F1724"),
        (Keys.Pressed, "#190F1724"),
        (Keys.Scrim, "#8C0B1220"),

        (Keys.Info, "#2563EB"),
        (Keys.Danger, "#DC2626"),
        (Keys.Warning, "#D97706"),
        (Keys.Success, "#16A34A"),

        (Keys.Page, "#FFFFFF"),
        (Keys.PageInk, "#111827"),
        (Keys.PageGuide, "#9CA3AF"),
        (Keys.PageSample, "#E5E7EB"),
    ];

    private static readonly (string Key, string Color)[] Dark =
[
    (Keys.Canvas, "#0B0F16"),
        (Keys.Surface, "#131821"),
        (Keys.SurfaceRaised, "#191F2A"),
        (Keys.SurfaceSunken, "#0E131B"),
        (Keys.SurfaceOverlay, "#1C2330"),

        (Keys.Border, "#252D3A"),
        (Keys.BorderStrong, "#37414F"),
        (Keys.FieldBorder, "#646E7E"),

        (Keys.TextPrimary, "#E6E9EF"),
        (Keys.TextSecondary, "#9AA4B2"),
        (Keys.TextDisabled, "#5C6673"),

        (Keys.Hover, "#0FFFFFFF"),
        (Keys.Pressed, "#1AFFFFFF"),
        (Keys.Scrim, "#A6000000"),

        (Keys.Info, "#60A5FA"),
        (Keys.Danger, "#F87171"),
        (Keys.Warning, "#FBBF24"),
        (Keys.Success, "#4ADE80"),

        (Keys.Page, "#D5DAE1"),
        (Keys.PageInk, "#111827"),
        (Keys.PageGuide, "#6B7280"),
        (Keys.PageSample, "#BCC3CD"),
    ];

    private static readonly (string Key, string Color)[] HighContrast =
    [
        (Keys.Canvas, "#000000"),
        (Keys.Surface, "#000000"),
        (Keys.SurfaceRaised, "#0A0A0A"),
        (Keys.SurfaceSunken, "#000000"),
        (Keys.SurfaceOverlay, "#0A0A0A"),

        (Keys.Border, "#BFBFBF"),
        (Keys.BorderStrong, "#FFFFFF"),
        (Keys.FieldBorder, "#FFFFFF"),

        (Keys.TextPrimary, "#FFFFFF"),
        (Keys.TextSecondary, "#E0E0E0"),
        (Keys.TextDisabled, "#A6A6A6"),

        (Keys.Hover, "#40FFFFFF"),
        (Keys.Pressed, "#66FFFFFF"),
        (Keys.Scrim, "#CC000000"),

        (Keys.Info, "#8AB4FF"),
        (Keys.Danger, "#FF8A8A"),
        (Keys.Warning, "#FFD60A"),
        (Keys.Success, "#5CF08A"),

        (Keys.Page, "#000000"),
        (Keys.PageInk, "#FFFFFF"),
        (Keys.PageGuide, "#C8C8C8"),
        (Keys.PageSample, "#333333"),
    ];
}
