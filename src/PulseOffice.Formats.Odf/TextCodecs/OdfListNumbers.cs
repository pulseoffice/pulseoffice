using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal interface IOdfListNumbers
{
    ListNumberFormat? Read(XElement? level);

    void Write(XElement level, ListNumberFormat? format);
}

internal sealed class OdfListNumbers(IListNumberFormats formats) : OdfXml, IOdfListNumbers
{
    private readonly XNamespace _pulse = OdtNamespaces.Pulse;

    private readonly IReadOnlyDictionary<string, ListNumberStyle> _styles = new Dictionary<string, ListNumberStyle>(StringComparer.Ordinal)
    {
        ["1"] = ListNumberStyle.Arabic,
        ["a"] = ListNumberStyle.LowerLetter,
        ["A"] = ListNumberStyle.UpperLetter,
        ["i"] = ListNumberStyle.LowerRoman,
        ["I"] = ListNumberStyle.UpperRoman,
    };

    public OdfListNumbers() : this(new ListNumberFormats())
    {
    }

    public ListNumberFormat? Read(XElement? level)
    {
        if (level?.Name != Text + "list-level-style-number") return null;
        var token = (string?)level.Attribute(Style + "num-format") ?? "1";
        var known = _styles.TryGetValue(token, out var style);
        return formats.Normalized(new ListNumberFormat
        {
            Style = known ? style : ListNumberStyle.Arabic,
            Prefix = (string?)level.Attribute(Style + "num-prefix") ?? string.Empty,
            Suffix = (string?)level.Attribute(Style + "num-suffix") ?? string.Empty,
            Start = int.TryParse((string?)level.Attribute(Text + "start-value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ? start : 1,
            OdfFormat = known ? null : token.Length == 0 ? " " : token,
            DrawingScheme = (string?)level.Attribute(_pulse + "number-scheme"),
        });
    }

    public void Write(XElement level, ListNumberFormat? format)
    {
        var used = formats.Normalized(format) ?? new ListNumberFormat();
        var token = used.OdfFormat is { } native ? native.Trim() : _styles.First(pair => pair.Value == used.Style).Key;
        level.SetAttributeValue(Style + "num-format", token);
        level.SetAttributeValue(Style + "num-prefix", used.Prefix.Length > 0 ? used.Prefix : null);
        level.SetAttributeValue(Style + "num-suffix", used.Suffix.Length > 0 ? used.Suffix : null);
        level.SetAttributeValue(Text + "start-value", used.Start > 1 ? used.Start.ToString(CultureInfo.InvariantCulture) : null);
        level.SetAttributeValue(_pulse + "number-scheme", used.DrawingScheme);
    }
}
