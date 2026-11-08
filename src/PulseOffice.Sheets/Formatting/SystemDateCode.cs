using System.Globalization;
using System.Text;

namespace PulseOffice.Sheets.Formatting;

public interface ISystemDateCode
{
    string ShortDate(CultureInfo culture);
}

public sealed class SystemDateCode : ISystemDateCode
{
    private const string Fallback = "dd/mm/yyyy";

    public string ShortDate(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (culture.Equals(CultureInfo.InvariantCulture)) return Fallback;
        var format = culture.DateTimeFormat;
        var pattern = format.ShortDatePattern;
        var code = new StringBuilder(pattern.Length + 4);
        var index = 0;
        while (index < pattern.Length)
        {
            var character = pattern[index];
            switch (character)
            {
                case 'M': code.Append('m'); break;
                case 'd' or 'y': code.Append(character); break;
                case '/': Literal(code, format.DateSeparator); break;
                case '\'' or '"':
                    var end = pattern.IndexOf(character, index + 1);
                    end = end < 0 ? pattern.Length : end;
                    Literal(code, pattern[(index + 1)..end]);
                    index = end;
                    break;
                case '\\' when index + 1 < pattern.Length:
                    Literal(code, pattern[++index].ToString());
                    break;
                case 'g':
                    break;
                default:
                    Literal(code, character.ToString());
                    break;
            }
            index++;
        }
        var result = code.ToString().Trim();
        return result.Contains('d') && result.Contains('m') && result.Contains('y') ? result : Fallback;
    }

    private void Literal(StringBuilder code, string text)
    {
        if (text.Length == 0) return;
        if (text.All(character => character is '/' or '-' or '.' or ' ' or ',')) code.Append(text);
        else code.Append('"').Append(text.Replace("\"", string.Empty, StringComparison.Ordinal)).Append('"');
    }
}
