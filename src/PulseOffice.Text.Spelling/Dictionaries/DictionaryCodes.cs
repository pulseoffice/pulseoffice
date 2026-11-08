namespace PulseOffice.Text.Spelling.Dictionaries;

public interface IDictionaryCodes
{
    string? Normalize(string raw);

    string FileStem(string code);

    LanguageTag Language(string code);

    string Base(string code);

    int Version { get; }
}

public sealed class DictionaryCodes : IDictionaryCodes
{
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase) { "1996", "monoton" };

    private static readonly HashSet<string> PrimaryRegions = new(StringComparer.Ordinal)
    {
        "af-ZA", "be-BY", "bg-BG", "bn-BD", "ca-ES", "cs-CZ", "cy-GB", "da-DK", "de-DE", "el-GR", "en-US", "es-ES", "et-EE", "eu-ES",
        "fa-IR", "fi-FI", "fo-FO", "fr-FR", "fy-NL", "ga-IE", "gd-GB", "gl-ES", "he-IL", "hi-IN", "hr-HR", "hu-HU", "hy-AM", "id-ID",
        "is-IS", "it-IT", "ka-GE", "kk-KZ", "ko-KR", "lt-LT", "lv-LV", "mk-MK", "mn-MN", "ms-MY", "nb-NO", "ne-NP", "nl-NL", "nn-NO",
        "pl-PL", "ro-RO", "ru-RU", "sk-SK", "sl-SI", "sq-AL", "sr-RS", "sv-SE", "tr-TR", "uk-UA", "vi-VN",
    };

    public string? Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var segments = raw.Trim().Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments[0].Length is < 2 or > 3 || !segments[0].All(char.IsAsciiLetter)) return null;

        var parts = new List<string> { segments[0].ToLowerInvariant() };
        string? script = null;
        string? region = null;

        foreach (var segment in segments.Skip(1))
        {
            if (Ignored.Contains(segment)) continue;

            if (segment.Length == 4 && segment.All(char.IsAsciiLetter) && script is null && region is null)
                script = char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant();
            else if ((segment.Length == 2 && segment.All(char.IsAsciiLetter) || segment.Length == 3 && segment.All(char.IsAsciiDigit)) && region is null)
                region = segment.ToUpperInvariant();
            else
                return null;
        }

        if (script is not null) parts.Add(script);
        if (region is not null && !(script is null && PrimaryRegions.Contains($"{parts[0]}-{region}"))) parts.Add(region);
        return string.Join('-', parts);
    }

    public int Version => 2;

    public string Base(string code) => code.Split('-')[0].ToLowerInvariant();

    public string FileStem(string code) => code.Replace('-', '_');

    public LanguageTag Language(string code)
    {
        var segments = code.Split('-');
        var region = segments.Skip(1).FirstOrDefault(segment => segment.Length != 4);
        return LanguageTag.Parse(region is null ? segments[0] : $"{segments[0]}-{region}");
    }
}
