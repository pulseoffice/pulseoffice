using System.Globalization;
using System.Text.Json;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;

namespace PulseOffice.Typography.Sources;

public sealed class FontsourceSource(
    HttpClient http,
    FontServiceOptions options,
    IFontLicenseClassifier licenses,
    IFontCategoryClassifier categories,
    IFontScriptCoverage scripts,
    IClassicFaceSelector faces,
    OpenType.IOpenTypeReader reader) : OnlineFontSource(http, faces, reader)
{
    public const string SourceId = "fontsource";

    public override string Id => SourceId;

    protected override bool Enabled => options.Fontsource.Enabled;

    protected override Uri ListingUri => new(options.Fontsource.Endpoint, "fonts");

    protected override IReadOnlyList<FontFamilyInfo> ParseListing(JsonElement root) =>
        [.. Array(root).Select(Describe).OfType<FontFamilyInfo>()];

    private FontFamilyInfo? Describe(JsonElement item)
    {
        if (String(item, "id") is not { Length: > 0 } id || String(item, "family") is not { Length: > 0 } family) return null;

        var weights = Array(item, "weights")
            .Where(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            .Select(value => value.GetInt32()).ToList();
        if (weights.Count == 0) weights.Add(400);
        var styles = Array(item, "styles").Select(value => value.GetString()).OfType<string>().ToList();
        if (styles.Count == 0) styles.Add("normal");

        return new FontFamilyInfo
        {
            Family = family,
            SourceId = Id,
            Origin = Origin,
            OnlineId = id,
            Category = categories.FromName(String(item, "category") ?? string.Empty),
            Scripts = scripts.FromSubsets(Array(item, "subsets").Select(value => value.GetString()).OfType<string>()),
            License = licenses.FromSpdx(String(item, "license")),
            Homepage = $"https://fontsource.org/fonts/{Uri.EscapeDataString(id)}",
            Faces = [.. weights.SelectMany(weight => styles.Select(style => new FontFaceRef(weight, style == "italic", string.Empty)))],
        };
    }

    protected override async Task<IReadOnlyList<FontFaceRef>> ResolveFacesAsync(FontFamilyInfo family, CancellationToken ct)
    {
        var id = family.OnlineId ?? family.Family.ToLowerInvariant().Replace(' ', '-');
        using var document = await GetJsonAsync(new Uri(options.Fontsource.Endpoint, $"fonts/{Uri.EscapeDataString(id)}"), ct).ConfigureAwait(false);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("variants", out var variants) || variants.ValueKind != JsonValueKind.Object)
            return [];

        var subset = String(root, "defSubset") ?? "latin";
        var found = new List<FontFaceRef>();

        foreach (var weight in variants.EnumerateObject())
        {
            if (!int.TryParse(weight.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || weight.Value.ValueKind != JsonValueKind.Object) continue;

            foreach (var style in weight.Value.EnumerateObject())
            {
                if (style.Value.ValueKind != JsonValueKind.Object) continue;
                var chosen = style.Value.TryGetProperty(subset, out var preferred) ? preferred : style.Value.EnumerateObject().Select(entry => entry.Value).FirstOrDefault();
                if (chosen.ValueKind != JsonValueKind.Object || !chosen.TryGetProperty("url", out var urls)) continue;
                if (String(urls, "ttf") is { Length: > 0 } ttf) found.Add(new FontFaceRef(value, style.Name == "italic", ttf));
            }
        }

        return found;
    }
}
