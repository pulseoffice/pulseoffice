using System.Globalization;
using System.Text.Json;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;

namespace PulseOffice.Typography.Sources;

public sealed class GoogleFontsSource(
    HttpClient http,
    FontServiceOptions options,
    IFontCategoryClassifier categories,
    IFontScriptCoverage scripts,
    IClassicFaceSelector faces,
    GoogleFontsMetadataParser metadata,
    OpenType.IOpenTypeReader reader) : OnlineFontSource(http, faces, reader)
{
    public const string SourceId = "google";

    private static readonly string[] LicenseFolders = ["ofl", "apache", "ufl"];

    public static readonly FontLicense GoogleLicense = new(FontLicenseKind.OpenSource, "OFL / Apache / UFL (Google Fonts)", null, "https://fonts.google.com/attribution");

    public override string Id => SourceId;

    protected override bool Enabled => options.GoogleFonts.Enabled && options.GoogleFonts.Files is not null;

    protected override Uri ListingUri => options.GoogleFonts.Endpoint;

    protected override IReadOnlyList<FontFamilyInfo> ParseListing(JsonElement root) =>
        [.. Array(root, "familyMetadataList").Select(Describe).OfType<FontFamilyInfo>()];

    private FontFamilyInfo? Describe(JsonElement item)
    {
        if (String(item, "family") is not { Length: > 0 } family) return null;
        if (item.TryGetProperty("isOpenSource", out var open) && open.ValueKind == JsonValueKind.False) return null;

        var styles = new List<FontFaceRef>();
        if (item.TryGetProperty("fonts", out var fonts) && fonts.ValueKind == JsonValueKind.Object)
            foreach (var font in fonts.EnumerateObject())
            {
                var italic = font.Name.EndsWith('i');
                if (int.TryParse(italic ? font.Name[..^1] : font.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight))
                    styles.Add(new FontFaceRef(weight, italic, string.Empty));
            }

        return new FontFamilyInfo
        {
            Family = family,
            SourceId = Id,
            Origin = Origin,
            OnlineId = family,
            Category = categories.FromName(String(item, "category") ?? string.Empty),
            Scripts = scripts.FromSubsets(Array(item, "subsets").Select(value => value.GetString()).OfType<string>()),
            License = GoogleLicense,
            Designer = Array(item, "designers").Select(value => value.GetString()).FirstOrDefault(),
            Homepage = $"https://fonts.google.com/specimen/{Uri.EscapeDataString(family.Replace(' ', '+'))}",
            Faces = styles,
        };
    }

    protected override async Task<IReadOnlyList<FontFaceRef>> ResolveFacesAsync(FontFamilyInfo family, CancellationToken ct)
    {
        var root = options.GoogleFonts.Files!;
        var slug = new string([.. family.Family.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

        foreach (var folder in LicenseFolders)
        {
            var directory = new Uri(root, $"{folder}/{slug}/");
            if (await TryGetTextAsync(new Uri(directory, "METADATA.pb"), ct).ConfigureAwait(false) is not { } text) continue;
            return [.. metadata.Parse(text).Select(face => face with { Location = new Uri(directory, Uri.EscapeDataString(face.Location)).AbsoluteUri })];
        }

        return [];
    }
}

public sealed class GoogleFontsMetadataParser
{
    public IReadOnlyList<FontFaceRef> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var faces = new List<FontFaceRef>();
        var inside = false;
        int weight = 400;
        var italic = false;
        string? file = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("fonts {", StringComparison.Ordinal))
            {
                inside = true;
                weight = 400;
                italic = false;
                file = null;
                continue;
            }

            if (!inside) continue;

            if (line == "}")
            {
                inside = false;
                if (file is not null && file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    faces.Add(new FontFaceRef(weight, italic, file));
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"');

            switch (key)
            {
                case "style":
                    italic = value == "italic";
                    break;
                case "weight" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    weight = parsed;
                    break;
                case "filename":
                    file = value;
                    break;
            }
        }

        return faces;
    }

    public string? License(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split('\n').Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("license:", StringComparison.Ordinal))?[8..].Trim().Trim('"');
    }
}
