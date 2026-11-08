using System.Text.Json;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public sealed record DictionaryEndpoint(bool Enabled, Uri Listing, Uri Files);

public sealed record DictionaryServiceOptions
{
    public DictionaryEndpoint Spelling { get; init; } = new(
        true,
        new Uri("https://api.github.com/repos/wooorm/dictionaries/contents/dictionaries"),
        new Uri("https://raw.githubusercontent.com/wooorm/dictionaries/main/"));

    public DictionaryEndpoint Hyphenation { get; init; } = new(
        true,
        new Uri("https://api.github.com/repos/hyphenation/tex-hyphen/contents/hyph-utf8/tex/generic/hyph-utf8/patterns/txt"),
        new Uri("https://raw.githubusercontent.com/hyphenation/tex-hyphen/master/hyph-utf8/tex/generic/hyph-utf8/patterns/"));

    public DictionaryEndpoint Mozilla { get; init; } = new(
        true,
        new Uri("https://addons.mozilla.org/api/v5/addons/language-tools/?app=firefox&type=dictionary"),
        new Uri("https://addons.mozilla.org/api/v5/addons/search/?app=firefox&type=dictionary&page_size=50&sort=users"));

    public DictionaryEndpoint WordNet { get; init; } = new(
        true,
        new Uri("https://api.github.com/repos/globalwordnet/english-wordnet/releases/latest"),
        new Uri("https://github.com/globalwordnet/english-wordnet/releases/download/"));

    public DictionaryEndpoint OpenThesaurus { get; init; } = new(
        true,
        new Uri("https://www.openthesaurus.de/export/"),
        new Uri("https://www.openthesaurus.de/export/"));

    public TimeSpan ListingLifetime { get; init; } = TimeSpan.FromDays(7);
}

public interface IDictionaryServiceOptionsStore
{
    DictionaryServiceOptions Load();
}

public sealed class FileDictionaryServiceOptionsStore(string path) : IDictionaryServiceOptionsStore
{
    public DictionaryServiceOptions Load()
    {
        var defaults = new DictionaryServiceOptions();
        if (!File.Exists(path)) return defaults;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return defaults;

            return defaults with
            {
                Spelling = Read(document.RootElement, "spelling", defaults.Spelling),
                Hyphenation = Read(document.RootElement, "hyphenation", defaults.Hyphenation),
                Mozilla = Read(document.RootElement, "mozilla", defaults.Mozilla),
                WordNet = Read(document.RootElement, "wordnet", defaults.WordNet),
                OpenThesaurus = Read(document.RootElement, "openthesaurus", defaults.OpenThesaurus),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return defaults;
        }
    }

    private DictionaryEndpoint Read(JsonElement root, string name, DictionaryEndpoint fallback)
    {
        if (!root.TryGetProperty(name, out var section) || section.ValueKind != JsonValueKind.Object) return fallback;

        var enabled = section.TryGetProperty("enabled", out var flag) && flag.ValueKind is JsonValueKind.False or JsonValueKind.True
            ? flag.GetBoolean()
            : fallback.Enabled;

        return new DictionaryEndpoint(enabled, Url(section, "listing") ?? fallback.Listing, Url(section, "files") ?? fallback.Files);
    }

    private Uri? Url(JsonElement section, string name) =>
        section.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri
            : null;
}
