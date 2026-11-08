using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace PulseOffice.Formats.OpenXml;

internal sealed partial class OoxmlTextEscape
{
    public string Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!NeedsEncoding(text)) return text;

        var builder = new StringBuilder(text.Length + 16);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                builder.Append(character).Append(text[++index]);
                continue;
            }

            if (character == '_' && StartsEscape(text, index))
                builder.Append("_x005F_");
            else if (!XmlConvert.IsXmlChar(character) || character == '\r')
                builder.Append(CultureInfo.InvariantCulture, $"_x{(int)character:X4}_");
            else
                builder.Append(character);
        }

        return builder.ToString();
    }

    public string Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains("_x", StringComparison.Ordinal)) return text;
        return Escape().Replace(text, match => ((char)int.Parse(match.Groups[1].ValueSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
    }

    private bool NeedsEncoding(string text) =>
        text.Contains("_x", StringComparison.Ordinal) || text.Any(character => character == '\r' || (!XmlConvert.IsXmlChar(character) && !char.IsSurrogate(character)));

    private bool StartsEscape(string text, int index) =>
        index + 7 <= text.Length && text[index + 1] == 'x' && text[index + 6] == '_'
        && text.AsSpan(index + 2, 4).ToString().All(Uri.IsHexDigit);

    private Regex Escape() => Single();

    [GeneratedRegex("_x([0-9A-Fa-f]{4})_")]
    private static partial Regex Single();
}
