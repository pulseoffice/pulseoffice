using System.Globalization;
using System.Text;
using System.Text.Json;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public sealed class MozillaSpellingSource(
    IDictionaryHttp http,
    IDictionaryCodes codes,
    IDictionaryLicenseClassifier licenses,
    IDictionaryArchive archive,
    DictionaryServiceOptions options) : IDictionarySource
{
    public const string SourceId = "amo";

    private const int MaximumPages = 20;
    private const string ManifestName = "manifest.json";

    private sealed record Offer(DictionaryPackage Package, long Users);

    public string Id => SourceId;

    public DictionaryPart Part => DictionaryPart.Spelling;

    public int Priority => 20;

    private DictionaryEndpoint Endpoint => options.Mozilla;

    public async Task<IReadOnlyList<DictionaryPackage>> ListAsync(CancellationToken ct = default)
    {
        if (!Endpoint.Enabled) return [];

        var locales = await LocalesAsync(ct).ConfigureAwait(false);
        var offers = new Dictionary<string, Offer>(StringComparer.OrdinalIgnoreCase);
        Uri? page = Endpoint.Files;

        for (var count = 0; page is not null && count < MaximumPages; count++)
        {
            using var document = await http.JsonAsync(page, ct).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The add-on search answer has no results.");

            foreach (var result in results.EnumerateArray())
                if (Describe(result, locales) is { } offer
                    && (!offers.TryGetValue(offer.Package.Code, out var previous) || previous.Users < offer.Users))
                    offers[offer.Package.Code] = offer;

            page = Next(root);
        }

        return [.. offers.Values.Select(offer => offer.Package).OrderBy(package => package.Code, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyList<DictionaryFileContent>> FetchAsync(
        DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var file = package.Files.FirstOrDefault() ?? throw new InvalidDataException("The add-on has no file to download.");
        progress?.Report(new DictionaryProgress(0, 1, 0));
        var data = await http.BytesAsync(file.Location, ct).ConfigureAwait(false);
        progress?.Report(new DictionaryProgress(1, 1, data.Length));

        var entries = archive.Extract(data, name =>
            name == ManifestName
            || name.EndsWith(".aff", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dic", StringComparison.OrdinalIgnoreCase));

        var words = DictionaryPath(entries, package.Code);
        var affix = Path.ChangeExtension(words, ".aff");
        if (!entries.TryGetValue(words, out var dic) || !entries.TryGetValue(affix, out var aff) || dic.Length == 0 || aff.Length == 0)
            throw new InvalidDataException("The add-on does not contain a Hunspell dictionary.");

        var stem = codes.FileStem(package.Code);
        return
        [
            new DictionaryFileContent($"{stem}.aff", aff),
            new DictionaryFileContent($"{stem}.dic", dic),
            new DictionaryFileContent($"{stem}.license.txt", Encoding.UTF8.GetBytes($"{package.License.Name}\n{package.License.Url}\n{package.Homepage}\n")),
        ];
    }

    private async Task<Dictionary<long, string>> LocalesAsync(CancellationToken ct)
    {
        using var document = await http.JsonAsync(Endpoint.Listing, ct).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The language tools answer has no results.");

        var locales = new Dictionary<long, string>();
        foreach (var tool in results.EnumerateArray())
            if (Number(tool, "id") is { } id && Text(tool, "target_locale") is { } locale) locales[id] = locale;
        return locales;
    }

    private Offer? Describe(JsonElement result, Dictionary<long, string> locales)
    {
        if (Number(result, "id") is not { } id || !locales.TryGetValue(id, out var locale) || codes.Normalize(locale) is not { } code) return null;
        if (!result.TryGetProperty("current_version", out var version) || version.ValueKind != JsonValueKind.Object) return null;
        if (!version.TryGetProperty("file", out var file) || file.ValueKind != JsonValueKind.Object) return null;
        if (Text(file, "url") is not { } address || !Uri.TryCreate(address, UriKind.Absolute, out var location) || location.Scheme != Uri.UriSchemeHttps) return null;
        if (Number(file, "size") is { } size && size > DictionaryHttp.FileMaximum) return null;

        var license = version.TryGetProperty("license", out var terms) && terms.ValueKind == JsonValueKind.Object
            ? licenses.FromSpdx(Text(terms, "slug"), Text(terms, "url"))
            : DictionaryLicense.Unknown;

        var package = new DictionaryPackage(
            Id,
            code,
            Part,
            Text(version, "version") ?? Number(file, "id")?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            license,
            [new DictionaryRemoteFile("xpi", location)],
            $"addons.mozilla.org: {Name(result) ?? code}",
            Text(result, "url"));

        return new Offer(package, Number(result, "average_daily_users") ?? 0);
    }

    private Uri? Next(JsonElement root) =>
        Text(root, "next") is { } next && Uri.TryCreate(next, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Host == Endpoint.Files.Host
            ? uri
            : null;

    private string DictionaryPath(IReadOnlyDictionary<string, byte[]> entries, string code)
    {
        var declared = new List<(string Locale, string? Path)>();

        if (entries.TryGetValue(ManifestName, out var manifest))
        {
            try
            {
                using var document = JsonDocument.Parse(manifest);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("dictionaries", out var dictionaries)
                    && dictionaries.ValueKind == JsonValueKind.Object)
                    foreach (var property in dictionaries.EnumerateObject())
                        if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { Length: > 0 } path)
                            declared.Add((property.Name, path.TrimStart('/')));
            }
            catch (JsonException)
            {
            }
        }

        var chosen = declared.FirstOrDefault(entry => string.Equals(codes.Normalize(entry.Locale), code, StringComparison.OrdinalIgnoreCase)).Path
            ?? declared.FirstOrDefault().Path
            ?? entries.Keys.Where(name => name.EndsWith(".dic", StringComparison.OrdinalIgnoreCase)).OrderBy(name => name, StringComparer.Ordinal).FirstOrDefault();

        return chosen ?? throw new InvalidDataException("The add-on does not declare a dictionary.");
    }

    private string? Name(JsonElement result)
    {
        if (!result.TryGetProperty("name", out var name)) return null;
        if (name.ValueKind == JsonValueKind.String) return name.GetString();
        if (name.ValueKind != JsonValueKind.Object) return null;

        return Text(name, "en-US") ?? name.EnumerateObject().Select(property => property.Value)
            .Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()).FirstOrDefault();
    }

    private string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private long? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;
}
