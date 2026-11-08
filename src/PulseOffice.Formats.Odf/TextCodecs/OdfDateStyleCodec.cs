using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal interface IOdfDateStyleCodec
{
    bool IsDataStyle(XElement element);

    string? Read(XElement style);

    XElement Build(string name, string pattern);
}

internal sealed record OdfDateToken(string Element, bool Long, bool Textual, string Pattern);

internal sealed class OdfDateStyleCodec : OdfXml, IOdfDateStyleCodec
{
    private const string AmPm = " tt";

    private const int MostDecimals = 7;

    private static readonly XNamespace Number = OdfPackage.Namespaces.Number;

    private readonly IReadOnlyList<OdfDateToken> _tokens =
    [
        new("day", false, false, "d"),
        new("day", true, false, "dd"),
        new("day-of-week", false, false, "ddd"),
        new("day-of-week", true, false, "dddd"),
        new("month", false, false, "M"),
        new("month", true, false, "MM"),
        new("month", false, true, "MMM"),
        new("month", true, true, "MMMM"),
        new("year", false, false, "yy"),
        new("year", true, false, "yyyy"),
        new("hours", false, false, "H"),
        new("hours", true, false, "HH"),
        new("minutes", false, false, "mm"),
        new("minutes", true, false, "mm"),
        new("seconds", false, false, "ss"),
        new("seconds", true, false, "ss"),
    ];

    private readonly IReadOnlyDictionary<string, XElement[]> _standard;

    public OdfDateStyleCodec()
    {
        _standard = new Dictionary<string, XElement[]>(StringComparer.Ordinal)
        {
            ["d"] = [Part("day", true), Literal("."), Part("month", true), Literal("."), Part("year", true)],
            ["D"] = [Part("day-of-week", true), Literal(", "), Part("day", false), Literal(". "), Part("month", true, true), Literal(" "), Part("year", true)],
            ["T"] = [Part("hours", false), Literal(":"), Part("minutes", false), Literal(":"), Part("seconds", false), new XElement(Number + "am-pm")],
            ["t"] = [Part("hours", false), Literal(":"), Part("minutes", false)],
        };
    }

    public bool IsDataStyle(XElement element) => element.Name == Number + "date-style" || element.Name == Number + "time-style";

    public string? Read(XElement style)
    {
        var time = style.Name == Number + "time-style";
        var parts = style.Elements().ToList();
        if ((string?)style.Attribute(Number + "automatic-order") == "true")
            return time ? parts.Any(part => part.Name == Number + "seconds") ? "T" : "t"
                : parts.Any(part => part.Name == Number + "day-of-week") ? "D" : "d";

        var twelve = parts.Any(part => part.Name == Number + "am-pm");
        var pattern = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Name == Number + "text") pattern.Append(Quoted(part.Value));
            else if (part.Name == Number + "am-pm") pattern.Append(AmPm);
            else if (Token(part) is { } token)
            {
                pattern.Append(twelve && part.Name.LocalName == "hours" ? token.ToLowerInvariant() : token);
                if (part.Name.LocalName == "seconds" && Decimals(part) is > 0 and var decimals) pattern.Append('.').Append('f', decimals);
            }
        }
        return pattern.Length == 0 ? null : pattern.ToString();
    }

    public XElement Build(string name, string pattern)
    {
        var parts = _standard.TryGetValue(pattern, out var known) ? known.Select(part => new XElement(part)).ToList() : Parse(pattern);
        var date = parts.Any(part => part.Name.LocalName is "day" or "day-of-week" or "month" or "year");
        var style = new XElement(Number + (date ? "date-style" : "time-style"), new XAttribute(Style + "name", name));
        if (known is not null && pattern != "t") style.Add(new XAttribute(Number + "automatic-order", "true"));
        style.Add(parts);
        return style;
    }

    private List<XElement> Parse(string pattern)
    {
        var parts = new List<XElement>();
        var literal = new StringBuilder();
        var index = 0;
        while (index < pattern.Length)
        {
            var current = pattern[index];
            if (current == '\'')
            {
                var close = pattern.IndexOf('\'', index + 1);
                if (close < 0) close = pattern.Length;
                literal.Append(pattern, index + 1, Math.Max(0, close - index - 1));
                index = close + 1;
                continue;
            }
            if (current == '\\' && index + 1 < pattern.Length)
            {
                literal.Append(pattern[index + 1]);
                index += 2;
                continue;
            }
            if (string.CompareOrdinal(pattern, index, AmPm, 0, AmPm.Length) == 0 || string.CompareOrdinal(pattern, index, "tt", 0, 2) == 0)
            {
                Flush();
                parts.Add(new XElement(Number + "am-pm"));
                index += current == ' ' ? AmPm.Length : 2;
                continue;
            }
            if ("dMyHhmsf".Contains(current, StringComparison.Ordinal))
            {
                var length = 1;
                while (index + length < pattern.Length && pattern[index + length] == current) length++;
                var run = pattern.Substring(index, length);
                index += length;
                if (current == 'f')
                {
                    if (parts.LastOrDefault() is { } last && last.Name == Number + "seconds" && literal.ToString() == ".")
                    {
                        literal.Clear();
                        last.SetAttributeValue(Number + "decimal-places", length.ToString(CultureInfo.InvariantCulture));
                    }
                    continue;
                }
                Flush();
                parts.Add(Element(run));
                continue;
            }
            literal.Append(current);
            index++;
        }
        Flush();
        return parts;

        void Flush()
        {
            if (literal.Length == 0) return;
            parts.Add(Literal(literal.ToString()));
            literal.Clear();
        }
    }

    private XElement Element(string run)
    {
        var upper = run[0] == 'h' ? run.ToUpperInvariant() : run;
        var token = _tokens.FirstOrDefault(entry => entry.Pattern == upper)
            ?? _tokens.Where(entry => entry.Pattern[0] == upper[0]).MaxBy(entry => entry.Pattern.Length)
            ?? _tokens[0];
        return Part(token.Element, token.Long && upper.Length > 1, token.Textual);
    }

    private string? Token(XElement part)
    {
        var isLong = (string?)part.Attribute(Number + "style") == "long";
        var textual = (string?)part.Attribute(Number + "textual") == "true";
        return _tokens.FirstOrDefault(entry => entry.Element == part.Name.LocalName && entry.Long == isLong && entry.Textual == textual)?.Pattern;
    }

    private int Decimals(XElement part) =>
        int.TryParse((string?)part.Attribute(Number + "decimal-places"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 0, MostDecimals) : 0;

    private string Quoted(string text) => "'" + text.Replace("'", "\\'", StringComparison.Ordinal) + "'";

    private XElement Part(string element, bool isLong, bool textual = false) =>
        new(Number + element,
            isLong ? new XAttribute(Number + "style", "long") : null,
            textual ? new XAttribute(Number + "textual", "true") : null);

    private XElement Literal(string text) => new(Number + "text", text);
}
