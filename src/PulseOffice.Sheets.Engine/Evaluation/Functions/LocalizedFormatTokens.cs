using System.Text;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface ILocalizedFormatTokens
{
    string ToCanonical(string format);
}

public sealed class LocalizedFormatTokens(ISpreadsheetLocale locale) : ILocalizedFormatTokens
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<char, char>> DateLettersByLanguage =
        new Dictionary<string, IReadOnlyDictionary<char, char>>(StringComparer.OrdinalIgnoreCase)
        {
            ["it"] = new Dictionary<char, char> { ['a'] = 'y', ['g'] = 'd' },
            ["fr"] = new Dictionary<char, char> { ['a'] = 'y', ['j'] = 'd' },
            ["es"] = new Dictionary<char, char> { ['a'] = 'y' },
            ["de"] = new Dictionary<char, char> { ['j'] = 'y', ['t'] = 'd' },
        };

    private static readonly string[] Keywords = ["AM/PM", "A/P", "General", "Standard"];

    public string ToCanonical(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (!DateLettersByLanguage.TryGetValue(locale.Culture.TwoLetterISOLanguageName, out var letters)) return format;

        var sections = Split(format);
        return string.Join(';', sections.Select(section => Translate(section, letters)));
    }

    private static List<string> Split(string format)
    {
        var sections = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character == '"') quoted = !quoted;
            if (!quoted && character == '\\' && index + 1 < format.Length)
            {
                current.Append(character).Append(format[++index]);
                continue;
            }

            if (!quoted && character == ';')
            {
                sections.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        sections.Add(current.ToString());
        return sections;
    }

    private static string Translate(string section, IReadOnlyDictionary<char, char> letters)
    {
        if (!Literals(section).Any(character => letters.ContainsKey(char.ToLowerInvariant(character)))) return section;
        if (Literals(section).Any(character => character is 'y' or 'Y')) return section;

        var result = new StringBuilder(section.Length);
        var index = 0;
        while (index < section.Length)
        {
            var character = section[index];
            if (character == '"')
            {
                var end = section.IndexOf('"', index + 1);
                end = end < 0 ? section.Length : end + 1;
                result.Append(section, index, end - index);
                index = end;
            }
            else if (character == '[')
            {
                var end = section.IndexOf(']', index + 1);
                end = end < 0 ? section.Length : end + 1;
                result.Append(section, index, end - index);
                index = end;
            }
            else if (character == '\\' && index + 1 < section.Length)
            {
                result.Append(section, index, 2);
                index += 2;
            }
            else if (KeywordAt(section, index) is { } keyword)
            {
                result.Append(keyword);
                index += keyword.Length;
            }
            else
            {
                result.Append(letters.TryGetValue(char.ToLowerInvariant(character), out var canonical) ? canonical : character);
                index++;
            }
        }

        return result.ToString();
    }

    private static string? KeywordAt(string section, int index) =>
        Keywords.FirstOrDefault(keyword => string.Compare(section, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0) is { } found
            ? section.Substring(index, found.Length)
            : null;

    private static IEnumerable<char> Literals(string section)
    {
        var quoted = false;
        var bracketed = false;
        for (var index = 0; index < section.Length; index++)
        {
            var character = section[index];
            if (character == '"' && !bracketed) { quoted = !quoted; continue; }
            if (quoted) continue;
            if (character == '[') { bracketed = true; continue; }
            if (character == ']') { bracketed = false; continue; }
            if (bracketed) continue;
            if (character == '\\') { index++; continue; }
            if (KeywordAt(section, index) is { } keyword) { index += keyword.Length - 1; continue; }
            yield return character;
        }
    }
}
