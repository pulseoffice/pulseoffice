namespace PulseOffice.Typography.Licensing;

public interface IFontLicenseClassifier
{
    FontLicense FromSpdx(string? spdxId, string? url = null);

    FontLicense FromDescription(string? description, string? url, string? copyright = null);
}

public sealed class FontLicenseClassifier : IFontLicenseClassifier
{
    private sealed record Rule(FontLicenseKind Kind, string Name, string Spdx, string[] Markers);

    private static readonly Rule[] Rules =
    [
        new(FontLicenseKind.Ofl, "SIL Open Font License 1.1", "OFL-1.1", ["open font license", "scripts.sil.org/ofl", "openfontlicense.org", "ofl-1.1", "ofl"]),
        new(FontLicenseKind.Apache, "Apache License 2.0", "Apache-2.0", ["apache license", "apache.org/licenses", "apache-2.0", "apache"]),
        new(FontLicenseKind.Ubuntu, "Ubuntu Font Licence 1.0", "UFL-1.0", ["ubuntu font licen", "font.ubuntu.com/ufl", "ubuntu.com/legal/font-licen", "ufl-1.0", "ufl"]),
        new(FontLicenseKind.Mit, "MIT License", "MIT", ["mit license", "opensource.org/licenses/mit", "mit"]),
        new(FontLicenseKind.Cc0, "CC0 1.0", "CC0-1.0", ["cc0", "creativecommons.org/publicdomain/zero"]),
        new(FontLicenseKind.PublicDomain, "Public domain", "Unlicense", ["public domain", "unlicense"]),
        new(FontLicenseKind.Bitstream, "Bitstream Vera License", "Bitstream-Vera", ["bitstream vera", "dejavu fonts", "dejavu-fonts"]),
        new(FontLicenseKind.Gpl, "GNU GPL with font exception", "GPL-2.0-with-font-exception", ["gnu general public license", "gpl"]),
    ];

    private static readonly string[] ProprietaryMarkers =
    [
        "all rights reserved", "eula", "end user license", "property of", "may not be", "licensed to you",
        "commercial", "permitted by", "apple inc", "microsoft", "monotype", "adobe systems", "proprietary",
        "you may use this font", "trademark of",
    ];

    public FontLicense FromSpdx(string? spdxId, string? url = null)
    {
        if (string.IsNullOrWhiteSpace(spdxId)) return FontLicense.Unknown;

        var lower = spdxId.Trim().ToLowerInvariant();
        foreach (var rule in Rules)
            if (string.Equals(rule.Spdx, spdxId.Trim(), StringComparison.OrdinalIgnoreCase)
                || rule.Markers.Any(marker => lower == marker || lower.StartsWith(marker + "-", StringComparison.Ordinal)))
                return new FontLicense(rule.Kind, rule.Name, rule.Spdx, url ?? KnownUrl(rule.Kind));

        return new FontLicense(FontLicenseKind.OpenSource, spdxId.Trim(), spdxId.Trim(), url);
    }

    public FontLicense FromDescription(string? description, string? url, string? copyright = null)
    {
        var text = $"{description} {url}".ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(text))
            foreach (var rule in Rules)
                if (rule.Markers.Where(marker => marker.Length > 4).Any(text.Contains))
                    return new FontLicense(rule.Kind, rule.Name, rule.Spdx, url ?? KnownUrl(rule.Kind), description);

        var all = $"{text} {copyright}".ToLowerInvariant();
        if (ProprietaryMarkers.Any(all.Contains))
            return new FontLicense(FontLicenseKind.Proprietary, "Proprietary", null, url, description);

        return FontLicense.Unknown with { Url = url, Text = description };
    }

    private string? KnownUrl(FontLicenseKind kind) => kind switch
    {
        FontLicenseKind.Ofl => "https://openfontlicense.org",
        FontLicenseKind.Apache => "https://www.apache.org/licenses/LICENSE-2.0",
        FontLicenseKind.Ubuntu => "https://ubuntu.com/legal/font-licence",
        FontLicenseKind.Mit => "https://opensource.org/licenses/MIT",
        FontLicenseKind.Cc0 => "https://creativecommons.org/publicdomain/zero/1.0/",
        _ => null,
    };
}
