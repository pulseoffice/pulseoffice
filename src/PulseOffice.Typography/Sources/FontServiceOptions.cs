using System.Text.Json;

namespace PulseOffice.Typography.Sources;

public sealed record OnlineServiceOptions(bool Enabled, Uri Endpoint, Uri? Files = null);

public sealed record FontServiceOptions
{
    public OnlineServiceOptions GoogleFonts { get; init; } = new(
        true, new Uri("https://fonts.google.com/metadata/fonts"), new Uri("https://raw.githubusercontent.com/google/fonts/main/"));

    public OnlineServiceOptions Fontsource { get; init; } = new(true, new Uri("https://api.fontsource.org/v1/"));

    public TimeSpan ListingLifetime { get; init; } = TimeSpan.FromDays(7);
}

public interface IFontServiceOptionsStore
{
    FontServiceOptions Load();
}

public sealed class FileFontServiceOptionsStore(string path) : IFontServiceOptionsStore
{
    public FontServiceOptions Load()
    {
        var defaults = new FontServiceOptions();
        if (!File.Exists(path)) return defaults;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return defaults;

            return defaults with
            {
                GoogleFonts = Read(document.RootElement, "googleFonts", defaults.GoogleFonts),
                Fontsource = Read(document.RootElement, "fontsource", defaults.Fontsource),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return defaults;
        }
    }

    private OnlineServiceOptions Read(JsonElement root, string name, OnlineServiceOptions fallback)
    {
        if (!root.TryGetProperty(name, out var section) || section.ValueKind != JsonValueKind.Object) return fallback;

        var enabled = section.TryGetProperty("enabled", out var flag) && flag.ValueKind is JsonValueKind.False or JsonValueKind.True
            ? flag.GetBoolean()
            : fallback.Enabled;

        return new OnlineServiceOptions(enabled, Url(section, "endpoint") ?? fallback.Endpoint, Url(section, "files") ?? fallback.Files);
    }

    private Uri? Url(JsonElement section, string name) =>
        section.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri
            : null;
}
