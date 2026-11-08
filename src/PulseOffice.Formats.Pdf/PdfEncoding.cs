using System.Text;

namespace PulseOffice.Formats.Pdf;

public static class PdfEncoding
{
    private static readonly Dictionary<char, char> WindowsSpecific = new()
    {
        ['€'] = '\u0080',
        ['‚'] = '\u0082',
        ['ƒ'] = '\u0083',
        ['„'] = '\u0084',
        ['…'] = '\u0085',
        ['†'] = '\u0086',
        ['‡'] = '\u0087',
        ['ˆ'] = '\u0088',
        ['‰'] = '\u0089',
        ['Š'] = '\u008A',
        ['‹'] = '\u008B',
        ['Œ'] = '\u008C',
        ['Ž'] = '\u008E',
        ['‘'] = '\u0091',
        ['’'] = '\u0092',
        ['“'] = '\u0093',
        ['”'] = '\u0094',
        ['•'] = '\u0095',
        ['–'] = '\u0096',
        ['—'] = '\u0097',
        ['˜'] = '\u0098',
        ['™'] = '\u0099',
        ['š'] = '\u009A',
        ['›'] = '\u009B',
        ['œ'] = '\u009C',
        ['ž'] = '\u009E',
        ['Ÿ'] = '\u009F',
    };

    public static char ToWinAnsi(char character)
    {
        if (character < 128) return character;
        if (WindowsSpecific.TryGetValue(character, out var mapped)) return mapped;
        if (character <= 255) return character;
        if (char.IsSurrogate(character)) return '?';

        var folded = Fold(character);
        return folded <= 255 ? folded : '?';
    }

    public static string Composed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var composed = text.IsNormalized(NormalizationForm.FormC) ? text : text.Normalize(NormalizationForm.FormC);
        if (!composed.Any(IsMark)) return composed;

        return string.Concat(composed.Where(character => !IsMark(character)));
    }

    private static bool IsMark(char character) =>
        System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
            is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.EnclosingMark;

    public static byte[] ToBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = Composed(text);

        var buffer = new byte[text.Length];

        for (var index = 0; index < text.Length; index++)
            buffer[index] = (byte)ToWinAnsi(text[index]);

        return buffer;
    }

    public static string Literal(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = Composed(text);
        var result = new StringBuilder("(");

        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsLowSurrogate(text[index]) && index > 0 && char.IsHighSurrogate(text[index - 1])) continue;

            var mapped = ToWinAnsi(text[index]);
            switch (mapped)
            {
                case '(' or ')' or '\\':
                    result.Append('\\').Append(mapped);
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                case < ' ':
                    result.Append('\\').Append(Convert.ToString(mapped, 8).PadLeft(3, '0'));
                    break;
                default:
                    result.Append(mapped);
                    break;
            }
        }

        return result.Append(')').ToString();
    }

    private static char Fold(char character)
    {
        var decomposed = character.ToString().Normalize(NormalizationForm.FormD);

        foreach (var part in decomposed)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(part)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                return part;

        return '?';
    }
}
