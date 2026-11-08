using System.Text;

namespace PulseOffice.Sheets.Formatting;

public sealed class FormatCodeVocabulary
{
    public static readonly IReadOnlyList<string> SupportedLanguages = ["en", "it", "de", "es", "fr"];

    private static readonly Dictionary<string, string> ColorsToCanonical =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["BLACK"] = "BLACK",
        ["BLUE"] = "BLUE",
        ["CYAN"] = "CYAN",
        ["GREEN"] = "GREEN",
        ["MAGENTA"] = "MAGENTA",
        ["RED"] = "RED",
        ["WHITE"] = "WHITE",
        ["YELLOW"] = "YELLOW",

        ["NERO"] = "BLACK",
        ["BLU"] = "BLUE",
        ["CIANO"] = "CYAN",
        ["VERDE"] = "GREEN",
        ["ROSSO"] = "RED",
        ["BIANCO"] = "WHITE",
        ["GIALLO"] = "YELLOW",

        ["SCHWARZ"] = "BLACK",
        ["BLAU"] = "BLUE",
        ["ZYAN"] = "CYAN",
        ["GRÜN"] = "GREEN",
        ["GRUEN"] = "GREEN",
        ["ROT"] = "RED",
        ["WEISS"] = "WHITE",
        ["WEISS "] = "WHITE",
        ["WEIß"] = "WHITE",
        ["GELB"] = "YELLOW",

        ["NEGRO"] = "BLACK",
        ["AZUL"] = "BLUE",
        ["CIAN"] = "CYAN",
        ["VERDE_ES"] = "GREEN",
        ["ROJO"] = "RED",
        ["BLANCO"] = "WHITE",
        ["AMARILLO"] = "YELLOW",

        ["NOIR"] = "BLACK",
        ["BLEU"] = "BLUE",
        ["CYAN_FR"] = "CYAN",
        ["VERT"] = "GREEN",
        ["ROUGE"] = "RED",
        ["BLANC"] = "WHITE",
        ["JAUNE"] = "YELLOW",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> ColorsFromCanonical = new()
    {
        ["en"] = new() { ["BLACK"] = "BLACK", ["BLUE"] = "BLUE", ["CYAN"] = "CYAN", ["GREEN"] = "GREEN", ["MAGENTA"] = "MAGENTA", ["RED"] = "RED", ["WHITE"] = "WHITE", ["YELLOW"] = "YELLOW" },
        ["it"] = new() { ["BLACK"] = "NERO", ["BLUE"] = "BLU", ["CYAN"] = "CIANO", ["GREEN"] = "VERDE", ["MAGENTA"] = "MAGENTA", ["RED"] = "ROSSO", ["WHITE"] = "BIANCO", ["YELLOW"] = "GIALLO" },
        ["de"] = new() { ["BLACK"] = "SCHWARZ", ["BLUE"] = "BLAU", ["CYAN"] = "ZYAN", ["GREEN"] = "GRÜN", ["MAGENTA"] = "MAGENTA", ["RED"] = "ROT", ["WHITE"] = "WEISS", ["YELLOW"] = "GELB" },
        ["es"] = new() { ["BLACK"] = "NEGRO", ["BLUE"] = "AZUL", ["CYAN"] = "CIAN", ["GREEN"] = "VERDE", ["MAGENTA"] = "MAGENTA", ["RED"] = "ROJO", ["WHITE"] = "BLANCO", ["YELLOW"] = "AMARILLO" },
        ["fr"] = new() { ["BLACK"] = "NOIR", ["BLUE"] = "BLEU", ["CYAN"] = "CYAN", ["GREEN"] = "VERT", ["MAGENTA"] = "MAGENTA", ["RED"] = "ROUGE", ["WHITE"] = "BLANC", ["YELLOW"] = "JAUNE" },
    };

    private static readonly Dictionary<string, DatePlaceholders> DateTokens = new()
    {
        ["en"] = new('d', 'm', 'y', 'h', 'm', 's'),
        ["it"] = new('g', 'm', 'a', 'h', 'm', 's'),
        ["de"] = new('t', 'm', 'j', 'h', 'm', 's'),
        ["es"] = new('d', 'm', 'a', 'h', 'm', 's'),
        ["fr"] = new('j', 'm', 'a', 'h', 'm', 's'),
    };

    private static readonly string[] ProtectedKeywords = ["General", "Standard", "AM/PM", "A/P"];

    private readonly record struct DatePlaceholders(
        char Day, char Month, char Year, char Hour, char Minute, char Second);

    public string ToCanonical(string code, string language)
    {
        ArgumentNullException.ThrowIfNull(code);

        var tokens = DateTokens.GetValueOrDefault(Normalize(language), DateTokens["en"]);
        return Translate(code, from: tokens, to: DateTokens["en"], colorMap: ColorsToCanonical);
    }

    public string ToLanguage(string canonicalCode, string language)
    {
        ArgumentNullException.ThrowIfNull(canonicalCode);

        var normalized = Normalize(language);
        var tokens = DateTokens.GetValueOrDefault(normalized, DateTokens["en"]);
        var colors = ColorsFromCanonical.GetValueOrDefault(normalized, ColorsFromCanonical["en"]);

        return Translate(canonicalCode, from: DateTokens["en"], to: tokens,
            colorMap: colors.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase));
    }

    public static string? CanonicalColor(string name) =>
    ColorsToCanonical.GetValueOrDefault(name.Trim());

    private static string Normalize(string language) =>
        string.IsNullOrWhiteSpace(language) ? "en" : language.Split('-')[0].ToLowerInvariant();

    private static string Translate(
    string code, DatePlaceholders from, DatePlaceholders to, Dictionary<string, string> colorMap)
    {
        var result = new StringBuilder(code.Length);
        var inQuotes = false;

        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                result.Append(c);
                continue;
            }

            if (inQuotes)
            {
                result.Append(c);
                continue;
            }

            if (c == '\\' && i + 1 < code.Length)
            {
                result.Append(c).Append(code[++i]);
                continue;
            }

            if (c == '[')
            {
                var end = code.IndexOf(']', i);
                if (end < 0)
                {
                    result.Append(c);
                    continue;
                }

                result.Append(TranslateBracket(code[(i + 1)..end], colorMap));
                i = end;
                continue;
            }

            if (ProtectedKeywords.FirstOrDefault(k => string.Compare(code, i, k, 0, k.Length, StringComparison.OrdinalIgnoreCase) == 0) is { } keyword)
            {
                result.Append(code, i, keyword.Length);
                i += keyword.Length - 1;
                continue;
            }

            result.Append(TranslateChar(c, from, to));
        }

        return result.ToString();
    }

    private static string TranslateBracket(string content, Dictionary<string, string> colorMap)
    {
        var mapped = colorMap.GetValueOrDefault(content.Trim());
        return mapped is null ? $"[{content}]" : $"[{mapped}]";
    }

    private static char TranslateChar(char c, DatePlaceholders from, DatePlaceholders to)
    {
        var lower = char.ToLowerInvariant(c);
        var upper = char.IsUpper(c);

        var translated =
            lower == from.Day ? to.Day :
            lower == from.Year ? to.Year :
            lower == from.Month ? to.Month :
            lower == from.Hour ? to.Hour :
            lower == from.Second ? to.Second :
            (char?)null;

        if (translated is null) return c;
        return upper ? char.ToUpperInvariant(translated.Value) : translated.Value;
    }
}
