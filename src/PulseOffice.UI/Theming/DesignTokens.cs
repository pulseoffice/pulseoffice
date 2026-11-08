namespace PulseOffice.UI.Theming;

public enum InterfaceDensity { Compact, Comfortable, Touch }

public static class DesignTokens
{
    public const string ShellAccent = "#6B7280";

    public const double SubtleAccentOpacity = 0.14;

    public static Metrics For(InterfaceDensity density) => density switch
    {
        InterfaceDensity.Comfortable => new Metrics(
            TitleBarHeight: 40, StatusBarHeight: 26,
            RowHeight: 30, ControlHeight: 30, IconSize: 18,
            SpacingSmall: 6, SpacingMedium: 12, SpacingLarge: 20,
            FontSize: 13, RadiusTight: 5, RadiusSoft: 14),

        InterfaceDensity.Touch => new Metrics(
            TitleBarHeight: 48, StatusBarHeight: 32,
            RowHeight: 38, ControlHeight: 38, IconSize: 22,
            SpacingSmall: 8, SpacingMedium: 16, SpacingLarge: 24,
            FontSize: 14, RadiusTight: 6, RadiusSoft: 16),

        _ => new Metrics(
            TitleBarHeight: 36, StatusBarHeight: 24,
            RowHeight: 22, ControlHeight: 26, IconSize: 16,
            SpacingSmall: 4, SpacingMedium: 8, SpacingLarge: 16,
            FontSize: 12, RadiusTight: 4, RadiusSoft: 12),
    };

    public readonly record struct Metrics(
    double TitleBarHeight,
    double StatusBarHeight,
    double RowHeight,
    double ControlHeight,
    double IconSize,
    double SpacingSmall,
    double SpacingMedium,
    double SpacingLarge,
    double FontSize,
    double RadiusTight,
    double RadiusSoft)
    {
        public double Radius => RadiusTight;

        public double FontSizeSmall => FontSize - 2;

        public double FontSizeHeading => FontSize + 4;

        public double FontSizeDisplay => FontSize + 14;

        public double LineHeight => Math.Round(FontSize * 1.45);
    }
}
