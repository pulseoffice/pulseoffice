using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Documents.Editing;

public interface IWordWildcards
{
    string? Pattern(string wildcards);

    string Replacement(string replacement);
}

public sealed class WordWildcards : IWordWildcards
{
    private const string AnyCharacter = "[^\\n\\f\\u000E\\u001D]";

    private static readonly Regex Repeat = new(@"^\d+(,\d*)?$", RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<char, string> _carets = new Dictionary<char, string>
    {
        ['p'] = "\\n",
        ['t'] = "\\t",
        ['l'] = "\\u2028",
        ['m'] = "\\f",
        ['n'] = "\\u000E",
        ['b'] = "\\u001D",
        ['s'] = "\\u00A0",
        ['~'] = "\\u2011",
        ['-'] = "\\u00AD",
        ['^'] = "\\^",
        ['?'] = AnyCharacter,
        ['#'] = "[0-9]",
        ['$'] = "\\p{L}",
        ['w'] = "[ \\t\\u00A0]+",
    };

    private readonly IReadOnlyDictionary<int, string> _codes = new Dictionary<int, string>
    {
        [9] = "\\t",
        [11] = "\\u2028",
        [12] = "\\f",
        [13] = "\\n",
        [14] = "\\u000E",
    };

    public string? Pattern(string wildcards)
    {
        ArgumentNullException.ThrowIfNull(wildcards);
        var built = new StringBuilder(wildcards.Length * 2);
        var open = 0;
        for (var i = 0; i < wildcards.Length; i++)
        {
            var character = wildcards[i];
            switch (character)
            {
                case '\\':
                    if (++i >= wildcards.Length) return null;
                    built.Append(Regex.Escape(wildcards[i].ToString()));
                    break;
                case '?':
                    built.Append(AnyCharacter);
                    break;
                case '*':
                    built.Append(AnyCharacter).Append("*?");
                    break;
                case '@':
                    if (built.Length == 0) return null;
                    built.Append('+');
                    break;
                case '<':
                    built.Append(@"\b(?=\w)");
                    break;
                case '>':
                    built.Append(@"(?<=\w)\b");
                    break;
                case '(':
                    open++;
                    built.Append('(');
                    break;
                case ')':
                    if (open-- == 0) return null;
                    built.Append(')');
                    break;
                case '{':
                    {
                        var close = wildcards.IndexOf('}', i);
                        if (close < 0 || built.Length == 0) return null;
                        var count = wildcards[(i + 1)..close].Replace(';', ',');
                        if (!Repeat.IsMatch(count)) return null;
                        built.Append('{').Append(count).Append('}');
                        i = close;
                        break;
                    }
                case '[':
                    {
                        var close = wildcards.IndexOf(']', i + 1);
                        if (close < 0) return null;
                        var set = wildcards[(i + 1)..close];
                        var negated = set.StartsWith('!');
                        if (negated) set = set[1..];
                        if (set.Length == 0) return null;
                        built.Append('[').Append(negated ? "^" : string.Empty).Append(Set(set)).Append(']');
                        i = close;
                        break;
                    }
                case '^':
                    if (Caret(wildcards, ref i) is not { } token) return null;
                    built.Append(token);
                    break;
                default:
                    built.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }
        return open == 0 ? built.ToString() : null;
    }

    public string Replacement(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var built = new StringBuilder(replacement.Length + 8);
        for (var i = 0; i < replacement.Length; i++)
        {
            var character = replacement[i];
            if (character == '\\' && i + 1 < replacement.Length)
            {
                var next = replacement[++i];
                if (char.IsAsciiDigit(next)) built.Append("${").Append(next).Append('}');
                else built.Append(next == '$' ? "$$" : next.ToString());
                continue;
            }
            if (character == '^' && i + 1 < replacement.Length && replacement[i + 1] == '&')
            {
                built.Append("$0");
                i++;
                continue;
            }
            built.Append(character == '$' ? "$$" : character.ToString());
        }
        return built.ToString();
    }

    private string? Caret(string text, ref int i)
    {
        if (i + 1 >= text.Length) return null;
        var digits = 0;
        while (i + 1 + digits < text.Length && digits < 3 && char.IsAsciiDigit(text[i + 1 + digits])) digits++;
        if (digits > 0)
        {
            var code = int.Parse(text.AsSpan(i + 1, digits), CultureInfo.InvariantCulture);
            i += digits;
            return _codes.TryGetValue(code, out var known) ? known : Regex.Escape(((char)code).ToString());
        }
        if (!_carets.TryGetValue(text[i + 1], out var token)) return null;
        i++;
        return token;
    }

    private string Set(string set)
    {
        var built = new StringBuilder(set.Length);
        foreach (var character in set) built.Append(character is '\\' or '[' or '^' ? "\\" + character : character.ToString());
        return built.ToString();
    }
}
