using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed partial class NumberFormatLocale
{
    public string Strip(string code) => LocaleTag().Replace(code, string.Empty);

    public void Apply(string section, XElement style)
    {
        var match = LocaleTag().Match(section);
        if (!match.Success || !int.TryParse(match.Groups["lcid"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lcid) || lcid <= 0) return;
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(lcid);
        }
        catch (CultureNotFoundException)
        {
            return;
        }
        var names = culture.Name.Split('-');
        if (names[0].Length == 0) return;
        style.SetAttributeValue(Namespaces.Number + "language", names[0]);
        if (names.Length > 1) style.SetAttributeValue(Namespaces.Number + "country", names[^1]);
    }

    public string Tag(XElement style)
    {
        if (style.Attribute(Namespaces.Number + "language")?.Value is not { Length: > 0 } language) return string.Empty;
        var country = style.Attribute(Namespaces.Number + "country")?.Value;
        try
        {
            var lcid = CultureInfo.GetCultureInfo(country is { Length: > 0 } ? $"{language}-{country}" : language).LCID;
            return lcid is > 0 and not 4096 ? "[$-" + lcid.ToString("X", CultureInfo.InvariantCulture) + "]" : string.Empty;
        }
        catch (CultureNotFoundException)
        {
            return string.Empty;
        }
    }

    [GeneratedRegex(@"\[\$-(?<lcid>[0-9A-Fa-f]{1,8})\]", RegexOptions.CultureInvariant)]
    private static partial Regex LocaleTag();
}
