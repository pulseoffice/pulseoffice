namespace PulseOffice.Document.Themes;

public sealed record ThemePalette(
    string Key,
    string FontFamily,
    string HeadingColor,
    string Accent1,
    string Accent2,
    string Accent3,
    string NoteBackground);

public interface IThemePalettes
{
    IReadOnlyList<ThemePalette> All { get; }

    ThemePalette? Find(string key);
}

public sealed class ThemePalettes : IThemePalettes
{
    public IReadOnlyList<ThemePalette> All { get; } =
    [
        new("neutral", "Calibri", "#1F2937", "#2563EB", "#16A34A", "#DC2626", "#FFF7CC"),
        new("warm", "Georgia", "#7C2D12", "#C2410C", "#B45309", "#9F1239", "#FDF0E1"),
        new("cool", "Verdana", "#0C4A6E", "#0369A1", "#0F766E", "#4338CA", "#E0F2FE"),
        new("forest", "Trebuchet MS", "#14532D", "#15803D", "#4D7C0F", "#A16207", "#ECFCCB"),
        new("graphite", "Arial", "#111827", "#374151", "#6B7280", "#0F172A", "#F3F4F6"),
        new("orchid", "Garamond", "#581C87", "#7E22CE", "#BE185D", "#6D28D9", "#FAE8FF"),
    ];

    public ThemePalette? Find(string key) =>
        All.FirstOrDefault(theme => string.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase));
}
