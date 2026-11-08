namespace PulseOffice.Sheets.UI;

public interface INumberFormatCodeEditor
{
    string WithDecimals(string code, int decimals);

    bool HasGrouping(string code);

    string WithGrouping(string code, bool grouped);
}

public sealed class NumberFormatCodeEditor : INumberFormatCodeEditor
{
    public string WithDecimals(string code, int decimals)
    {
        ArgumentNullException.ThrowIfNull(code);

        var fraction = decimals <= 0 ? string.Empty : "." + new string('0', decimals);
        return string.Join(';', SplitSections(code).Select(section => Rewrite(section, (integer, _) => integer + fraction)));
    }

    public bool HasGrouping(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var grouped = false;
        Rewrite(SplitSections(code)[0], (integer, rest) =>
        {
            grouped = integer.TrimEnd(',').Contains(',', StringComparison.Ordinal);
            return integer + rest;
        });
        return grouped;
    }

    public string WithGrouping(string code, bool grouped)
    {
        ArgumentNullException.ThrowIfNull(code);

        return string.Join(';', SplitSections(code).Select(section => Rewrite(section, (integer, rest) =>
        {
            var scaling = integer.Length - integer.TrimEnd(',').Length;
            var digits = integer[..^scaling].Replace(",", string.Empty, StringComparison.Ordinal);
            var body = grouped
                ? digits.PadLeft(4, '#') is var padded ? padded[..^3] + "," + padded[^3..] : digits
                : digits.TrimStart('#') is { Length: > 0 } kept ? kept : "#";
            return body + new string(',', scaling) + rest;
        })));
    }

    private delegate string IntegerRewrite(string integer, string fraction);

    private static List<string> SplitSections(string code)
    {
        var sections = new List<string>();
        var start = 0;
        var quoted = false;
        var bracket = false;

        for (var index = 0; index < code.Length; index++)
        {
            var character = code[index];

            if (character == '\\' && !quoted) { index++; continue; }
            if (character == '"') quoted = !quoted;
            else if (!quoted && character == '[') bracket = true;
            else if (!quoted && character == ']') bracket = false;
            else if (!quoted && !bracket && character == ';')
            {
                sections.Add(code[start..index]);
                start = index + 1;
            }
        }

        sections.Add(code[start..]);
        return sections;
    }

    private static string Rewrite(string section, IntegerRewrite rewrite)
    {
        var quoted = false;
        var bracket = false;

        for (var index = 0; index < section.Length; index++)
        {
            var character = section[index];

            if (character == '\\' && !quoted) { index++; continue; }
            if (character == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (character == '[') { bracket = true; continue; }
            if (character == ']') { bracket = false; continue; }
            if (bracket || character is not ('0' or '#' or '?')) continue;

            var end = index;
            while (end < section.Length && section[end] is '0' or '#' or '?' or ',') end++;

            var integerEnd = end;
            if (end < section.Length && section[end] == '.')
            {
                end++;
                while (end < section.Length && section[end] is '0' or '#' or '?') end++;
            }

            return section[..index] + rewrite(section[index..integerEnd], section[integerEnd..end]) + section[end..];
        }

        return section;
    }
}
