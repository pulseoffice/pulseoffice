using PulseOffice.Foundation.Colors;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf.Odt;

internal abstract class OdtXml : OdfXml
{
    protected static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    protected static readonly XNamespace Meta = OdfPackage.Namespaces.Meta;
    protected static readonly XNamespace Loext = OdfPackage.Namespaces.Loext;
    protected static readonly XNamespace Officeooo = "http://openoffice.org/2009/office";
    protected static readonly XNamespace Pt = OdtNamespaces.Pulse;
}

internal static class OdtNamespaces
{
    public static readonly XNamespace Pulse = "urn:pulseoffice:text";
}

internal sealed class OdtMeasure
{
    private readonly OdfLength _length = new();

    public string Points(double value) => value.ToString("0.####", CultureInfo.InvariantCulture) + "pt";

    public string Length(Twip value) => (value.Value / (double)Twip.PerPoint).ToString("0.##", CultureInfo.InvariantCulture) + "pt";

    public Twip? Twips(string? value) => _length.Parse(value);

    public double? Points(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        var unitStart = text.Length;
        while (unitStart > 0 && char.IsLetter(text[unitStart - 1])) unitStart--;
        if (!double.TryParse(text[..unitStart], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return null;
        double? points = text[unitStart..].ToLowerInvariant() switch
        {
            "pt" => number,
            "cm" => number * 72 / 2.54,
            "mm" => number * 72 / 25.4,
            "in" => number * 72,
            "pc" => number * 12,
            "px" => number * 0.75,
            "" => number * 72 / 2.54,
            _ => null,
        };
        return points is { } value2 && double.IsFinite(value2) ? Math.Round(value2, 4) : null;
    }

    public int? Integer(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    public double? Percent(string? value) =>
        value is not null && value.EndsWith('%')
        && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && double.IsFinite(percent)
            ? percent / 100.0
            : null;

    public string Percent(double factor) => (factor * 100).ToString("0.####", CultureInfo.InvariantCulture) + "%";

    public string Hex(uint argb) => new ArgbColor(argb).ToLowerHashedHex();

    public string Fill(uint argb) => argb >> 24 == 0 ? "transparent" : Hex(argb);

    public uint? Fill(string? value) => value == "transparent" ? 0x00FFFFFF : Color(value);

    public uint? Color(string? value) =>
        ArgbColor.FromHashedRgbHex(value)?.Value;
}

internal sealed partial class OdtStyleNames
{
    public string Encode(string name)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < name.Length; index++)
        {
            var c = name[index];
            var allowed = index == 0 ? XmlConvert.IsStartNCNameChar(c) : XmlConvert.IsNCNameChar(c);
            if (allowed && c != '_') builder.Append(c);
            else if (c == '_' && !Escaped().IsMatch(name[index..])) builder.Append(c);
            else builder.Append('_').Append(((int)c).ToString("x", CultureInfo.InvariantCulture)).Append('_');
        }
        return builder.Length == 0 ? "_" : builder.ToString();
    }

    public string Decode(string name) =>
        Escaped().Replace(name, match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());

    [GeneratedRegex("_([0-9a-fA-F]{2,4})_")]
    private static partial Regex Escaped();
}

internal sealed class OdtLanguageTag
{
    public IEnumerable<XAttribute> Attributes(string tag)
    {
        var parts = tag.Split('-', 2);
        yield return new XAttribute(OdfPackage.Namespaces.Fo + "language", parts[0]);
        yield return new XAttribute(OdfPackage.Namespaces.Fo + "country", parts.Length > 1 ? parts[1] : "none");
    }

    public string? Read(XElement properties)
    {
        var language = (string?)properties.Attribute(OdfPackage.Namespaces.Fo + "language");
        if (string.IsNullOrEmpty(language) || language == "none") return null;
        var country = (string?)properties.Attribute(OdfPackage.Namespaces.Fo + "country");
        return string.IsNullOrEmpty(country) || country == "none" ? language : language + "-" + country;
    }
}
