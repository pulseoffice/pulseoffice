using System.Globalization;
using System.Text;
using System.Xml.Linq;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal enum OdfValueType { Float, Percentage, Currency, Date, Time, Text, Boolean }

internal sealed record DatePart(string Code, string Element, bool Long, bool Textual = false);

internal sealed class DatePartTable
{
    public IReadOnlyList<DatePart> Parts { get; } =
    [
        new("yyyy", "year", true), new("yy", "year", false),
        new("mmmm", "month", true, true), new("mmm", "month", false, true),
        new("dddd", "day-of-week", true), new("ddd", "day-of-week", false),
        new("dd", "day", true), new("d", "day", false),
        new("hh", "hours", true), new("h", "hours", false),
        new("ss", "seconds", true), new("s", "seconds", false),
    ];

    public IReadOnlyList<DatePart> Months { get; } = [new("mm", "month", true), new("m", "month", false)];

    public IReadOnlyList<DatePart> Minutes { get; } = [new("mm", "minutes", true), new("m", "minutes", false)];

    public DatePart? Reverse(XElement element)
    {
        var name = element.Name.LocalName;
        var isLong = element.Attribute(Namespaces.Number + "style")?.Value == "long";
        var textual = element.Attribute(Namespaces.Number + "textual")?.Value == "true";
        return Parts.Concat(Months).Concat(Minutes)
            .FirstOrDefault(part => part.Element == name && part.Long == isLong && part.Textual == textual);
    }
}

internal sealed class OdfNumberStyleWriter
{
    public const string BooleanKeyword = "BOOLEAN";

    private readonly DatePartTable _dates = new();

    private readonly NumberFormatLocale _locale = new();

    public OdfValueType TypeOf(string code)
    {
        var section = Sections(_locale.Strip(code))[0];
        var plain = Unquoted(section);
        if (plain.Contains(BooleanKeyword, StringComparison.OrdinalIgnoreCase)) return OdfValueType.Boolean;
        if (plain.Contains('@') && !plain.Any(c => c is '0' or '#')) return OdfValueType.Text;
        if (IsDateTime(plain))
            return plain.IndexOfAny(['y', 'd', 'Y', 'D']) >= 0 || (HasMonth(plain) && !HasTimeNeighbour(plain)) ? OdfValueType.Date : OdfValueType.Time;
        if (plain.Contains('%')) return OdfValueType.Percentage;
        if (CurrencySymbol(section) is not null) return OdfValueType.Currency;
        return OdfValueType.Float;
    }

    public IReadOnlyList<XElement> Build(string name, string code)
    {
        var type = TypeOf(code);
        var all = Sections(code);
        var sections = (all.Count > 1 ? all.Where(section => !Unquoted(section).Contains('@')) : all).Take(3).ToList();
        if (sections.Count == 0) sections = [all[0]];
        while (sections.Count > 1 && string.IsNullOrEmpty(sections[^1])) sections.RemoveAt(sections.Count - 1);
        if (sections.Count == 1) return [BuildSection(name, sections[0], type)];

        var conditions = sections.Count == 2 ? new[] { "value()>=0" } : ["value()>0", "value()<0"];
        var result = new List<XElement>();
        var main = BuildSection(name, SplitCondition(sections[^1]).Body, type);
        for (var index = 0; index < sections.Count - 1; index++)
        {
            var part = $"{name}P{index}";
            var (condition, body) = SplitCondition(sections[index]);
            result.Add(BuildSection(part, body, type));
            main.Add(new XElement(Namespaces.Style + "map",
                new XAttribute(Namespaces.Style + "condition", condition is null ? conditions[index] : "value()" + condition),
                new XAttribute(Namespaces.Style + "apply-style-name", part)));
        }
        result.Add(main);
        return result;
    }

    private readonly System.Text.RegularExpressions.Regex _condition = new(
        @"\[(<=|>=|<>|<|>|=)\s*(-?[0-9]+(?:\.[0-9]+)?(?:[Ee][+-]?[0-9]+)?)\]",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private (string? Condition, string Body) SplitCondition(string section)
    {
        var match = _condition.Match(section);
        if (!match.Success) return (null, section);
        var comparison = match.Groups[1].Value == "<>" ? "!=" : match.Groups[1].Value;
        return (comparison + match.Groups[2].Value, section.Remove(match.Index, match.Length));
    }

    private XElement BuildSection(string name, string section, OdfValueType type)
    {
        var element = new XElement(Namespaces.Number + type switch
        {
            OdfValueType.Date => "date-style",
            OdfValueType.Time => "time-style",
            OdfValueType.Percentage => "percentage-style",
            OdfValueType.Currency => "currency-style",
            OdfValueType.Text => "text-style",
            OdfValueType.Boolean => "boolean-style",
            _ => "number-style",
        }, new XAttribute(Namespaces.Style + "name", name));

        _locale.Apply(section, element);
        section = _locale.Strip(section);
        if (Colour(section) is { } colour)
            element.Add(new XElement(Namespaces.Style + "text-properties", new XAttribute(Namespaces.Fo + "color", colour)));

        var children = type switch
        {
            OdfValueType.Date or OdfValueType.Time => DateChildren(section, element),
            OdfValueType.Boolean => BooleanChildren(section),
            _ => NumberChildren(section),
        };
        element.Add(children);
        return element;
    }

    private IEnumerable<XElement> DateChildren(string section, XElement style)
    {
        var output = new List<XElement>();
        var literal = new StringBuilder();
        var lastWasHour = false;
        var i = 0;

        void Flush()
        {
            if (literal.Length == 0) return;
            output.Add(new XElement(Namespaces.Number + "text", literal.ToString()));
            literal.Clear();
        }

        while (i < section.Length)
        {
            var c = section[i];
            if (c == '"')
            {
                var end = section.IndexOf('"', i + 1);
                if (end < 0) end = section.Length;
                literal.Append(section, i + 1, Math.Max(0, end - i - 1));
                i = end + 1;
                continue;
            }
            if (c == '\\' && i + 1 < section.Length)
            {
                literal.Append(section[i + 1]);
                i += 2;
                continue;
            }
            if (c == '[')
            {
                var end = section.IndexOf(']', i);
                if (end < 0) break;
                var inside = section[(i + 1)..end].ToLowerInvariant();
                if (inside is "h" or "hh") { Flush(); style.SetAttributeValue(Namespaces.Number + "truncate-on-overflow", "false"); output.Add(Part(new DatePart(inside, "hours", inside.Length == 2))); lastWasHour = true; }
                else if (inside is "m" or "mm") { Flush(); style.SetAttributeValue(Namespaces.Number + "truncate-on-overflow", "false"); output.Add(Part(_dates.Minutes[inside.Length == 2 ? 0 : 1])); }
                else if (inside is "s" or "ss") { Flush(); style.SetAttributeValue(Namespaces.Number + "truncate-on-overflow", "false"); output.Add(Part(_dates.Parts.First(p => p.Code == inside))); }
                i = end + 1;
                continue;
            }
            if (Matches(section, i, "AM/PM") || Matches(section, i, "A/P"))
            {
                Flush();
                output.Add(new XElement(Namespaces.Number + "am-pm"));
                i += Matches(section, i, "AM/PM") ? 5 : 3;
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (lower == 'm')
            {
                var run = Run(section, i, 'm');
                Flush();
                if (run >= 3) output.Add(Part(_dates.Parts.First(p => p.Code == new string('m', Math.Min(run, 4)))));
                else
                {
                    var minute = lastWasHour || NextIsSecond(section, i + run);
                    output.Add(Part((minute ? _dates.Minutes : _dates.Months)[run == 2 ? 0 : 1]));
                }
                lastWasHour = false;
                i += run;
                continue;
            }
            if (lower is 'y' or 'd' or 'h' or 's')
            {
                var run = Run(section, i, lower);
                Flush();
                var code = new string(lower, run);
                var part = _dates.Parts.Where(p => p.Code[0] == lower).OrderByDescending(p => p.Code.Length).FirstOrDefault(p => p.Code.Length <= run)
                    ?? _dates.Parts.Last(p => p.Code[0] == lower);
                if (lower == 'y' && run <= 2) part = _dates.Parts.First(p => p.Code == "yy");
                var element = Part(part);
                i += run;
                if (lower == 's' && i < section.Length && section[i] == '.' && i + 1 < section.Length && section[i + 1] == '0')
                {
                    var decimals = Run(section, i + 1, '0');
                    element.Add(new XAttribute(Namespaces.Number + "decimal-places", decimals));
                    i += decimals + 1;
                }
                output.Add(element);
                lastWasHour = lower == 'h';
                _ = code;
                continue;
            }
            literal.Append(c);
            i++;
        }
        Flush();
        return output;
    }

    private IEnumerable<XElement> BooleanChildren(string section)
    {
        var output = new List<XElement>();
        var literal = new StringBuilder();
        void Flush()
        {
            if (literal.Length == 0) return;
            output.Add(new XElement(Namespaces.Number + "text", literal.ToString()));
            literal.Clear();
        }

        for (var i = 0; i < section.Length;)
        {
            if (section[i] == '"')
            {
                var end = section.IndexOf('"', i + 1);
                if (end < 0) end = section.Length;
                literal.Append(section, i + 1, Math.Max(0, end - i - 1));
                i = end + 1;
            }
            else if (section[i] == '\\' && i + 1 < section.Length)
            {
                literal.Append(section[i + 1]);
                i += 2;
            }
            else if (section[i] == '[')
            {
                var end = section.IndexOf(']', i);
                i = end < 0 ? section.Length : end + 1;
            }
            else if (string.Compare(section, i, BooleanKeyword, 0, BooleanKeyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                Flush();
                output.Add(new XElement(Namespaces.Number + "boolean"));
                i += BooleanKeyword.Length;
            }
            else
            {
                literal.Append(section[i]);
                i++;
            }
        }

        Flush();
        return output;
    }

    private XElement Part(DatePart part)
    {
        var element = new XElement(Namespaces.Number + part.Element);
        if (part.Long) element.Add(new XAttribute(Namespaces.Number + "style", "long"));
        if (part.Textual) element.Add(new XAttribute(Namespaces.Number + "textual", "true"));
        return element;
    }

    private IEnumerable<XElement> NumberChildren(string section)
    {
        var output = new List<XElement>();
        var literal = new StringBuilder();
        var body = new StringBuilder();
        var bodyDone = false;
        var i = 0;

        void Flush()
        {
            if (literal.Length == 0) return;
            output.Add(new XElement(Namespaces.Number + "text", literal.ToString()));
            literal.Clear();
        }

        while (i < section.Length)
        {
            var c = section[i];
            if (c == '"')
            {
                var end = section.IndexOf('"', i + 1);
                if (end < 0) end = section.Length;
                literal.Append(section, i + 1, Math.Max(0, end - i - 1));
                i = end + 1;
                continue;
            }
            if (c == '\\' && i + 1 < section.Length) { literal.Append(section[i + 1]); i += 2; continue; }
            if (c == '_' && i + 1 < section.Length) { literal.Append(' '); i += 2; continue; }
            if (c == '*' && i + 1 < section.Length) { i += 2; continue; }
            if (c == '[')
            {
                var end = section.IndexOf(']', i);
                if (end < 0) break;
                var inside = section[(i + 1)..end];
                if (inside.StartsWith('$'))
                {
                    Flush();
                    var parts = inside[1..].Split('-');
                    var currency = new XElement(Namespaces.Number + "currency-symbol", parts[0]);
                    if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lcid) && Culture(lcid) is { } culture)
                    {
                        var names = culture.Name.Split('-');
                        currency.Add(new XAttribute(Namespaces.Number + "language", names[0]));
                        if (names.Length > 1) currency.Add(new XAttribute(Namespaces.Number + "country", names[^1]));
                    }
                    output.Add(currency);
                }
                i = end + 1;
                continue;
            }
            if (c == '@') { Flush(); output.Add(new XElement(Namespaces.Number + "text-content")); i++; continue; }
            if (!bodyDone && Matches(section, i, "General"))
            {
                Flush();
                output.Add(new XElement(Namespaces.Number + "number", new XAttribute(Namespaces.Number + "min-integer-digits", 1)));
                bodyDone = true;
                i += "General".Length;
                continue;
            }
            if (!bodyDone && (c is '0' or '#' or '?' || (body.Length > 0 && c is ',' or '.' or 'E' or 'e' or '+' or '-' or '/' or ' ' && Continues(section, i))))
            {
                if (body.Length == 0) Flush();
                body.Append(c);
                i++;
                if (i >= section.Length || !(section[i] is '0' or '#' or '?' or ',' or '.' or 'E' or 'e' or '+' or '-' or '/' or ' ' && (section[i] is '0' or '#' or '?' || Continues(section, i))))
                {
                    output.Add(NumberElement(body.ToString()));
                    bodyDone = true;
                }
                continue;
            }
            if (c is '€' or '$' or '£' or '¥')
            {
                Flush();
                output.Add(new XElement(Namespaces.Number + "currency-symbol", c.ToString()));
                i++;
                continue;
            }
            literal.Append(c);
            i++;
        }
        if (body.Length > 0 && !bodyDone) output.Add(NumberElement(body.ToString()));
        Flush();
        if (output.Count == 0) output.Add(new XElement(Namespaces.Number + "number", new XAttribute(Namespaces.Number + "min-integer-digits", 1)));
        return output;
    }

    private CultureInfo? Culture(int lcid)
    {
        try { return lcid > 0 ? CultureInfo.GetCultureInfo(lcid) : null; }
        catch (CultureNotFoundException) { return null; }
    }

    private bool Continues(string section, int i)
    {
        for (var j = i; j < section.Length; j++)
        {
            if (section[j] is '0' or '#' or '?') return true;
            if (section[j] is not (',' or '.' or 'E' or 'e' or '+' or '-' or '/' or ' ')) return false;
        }
        return false;
    }

    private XElement NumberElement(string body)
    {
        var trimmed = body.Trim();
        var exponent = trimmed.IndexOfAny(['E', 'e']);
        if (exponent >= 0)
        {
            var mantissa = trimmed[..exponent];
            var element = Numeric("scientific-number", mantissa);
            element.Add(new XAttribute(Namespaces.Number + "min-exponent-digits", trimmed[(exponent + 1)..].Count(ch => ch == '0')));
            var interval = mantissa.Split('.')[0].Count(ch => ch is '0' or '#' or '?');
            if (interval > 1) element.Add(new XAttribute(Namespaces.Number + "exponent-interval", interval));
            return element;
        }

        var slash = trimmed.IndexOf('/');
        if (slash >= 0)
        {
            var left = trimmed[..slash].TrimEnd();
            var space = left.LastIndexOf(' ');
            var whole = space >= 0 ? left[..space] : string.Empty;
            var numerator = space >= 0 ? left[(space + 1)..] : left;
            var denominator = trimmed[(slash + 1)..].Trim();
            var fraction = new XElement(Namespaces.Number + "fraction",
                new XAttribute(Namespaces.Number + "min-integer-digits", whole.Count(ch => ch == '0')),
                new XAttribute(Namespaces.Number + "min-numerator-digits", Math.Max(1, numerator.Count(ch => ch is '0' or '?' or '#'))),
                new XAttribute(Namespaces.Number + "min-denominator-digits", Math.Max(1, denominator.Count(ch => ch is '0' or '?' or '#'))));
            if (denominator.All(char.IsAsciiDigit) && denominator.Length > 0)
                fraction.Add(new XAttribute(Namespaces.Number + "denominator-value", denominator));
            return fraction;
        }

        return Numeric("number", trimmed);
    }

    private XElement Numeric(string elementName, string body)
    {
        var scale = 0;
        while (body.EndsWith(',')) { scale++; body = body[..^1]; }
        var point = body.IndexOf('.');
        var integer = point >= 0 ? body[..point] : body;
        var fraction = point >= 0 ? body[(point + 1)..] : string.Empty;

        var element = new XElement(Namespaces.Number + elementName,
            new XAttribute(Namespaces.Number + "decimal-places", fraction.Count(ch => ch is '0' or '#' or '?')),
            new XAttribute(Namespaces.Number + "min-decimal-places", fraction.Count(ch => ch == '0')),
            new XAttribute(Namespaces.Number + "min-integer-digits", integer.Count(ch => ch == '0')));
        if (integer.Contains(',')) element.Add(new XAttribute(Namespaces.Number + "grouping", "true"));
        if (scale > 0) element.Add(new XAttribute(Namespaces.Number + "display-factor", Math.Pow(1000, scale).ToString(CultureInfo.InvariantCulture)));
        return element;
    }

    public IReadOnlyList<string> Sections(string code)
    {
        var sections = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '"') quoted = !quoted;
            if (c == '\\' && i + 1 < code.Length) { current.Append(c).Append(code[++i]); continue; }
            if (c == ';' && !quoted) { sections.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        sections.Add(current.ToString());
        return sections;
    }

    private string Unquoted(string section)
    {
        var builder = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < section.Length; i++)
        {
            var c = section[i];
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (c == '\\') { i++; continue; }
            if (c == '[')
            {
                var end = section.IndexOf(']', i);
                if (end < 0) break;
                var inside = section[(i + 1)..end].ToLowerInvariant();
                if (inside is "h" or "hh" or "m" or "mm" or "s" or "ss") builder.Append(inside);
                i = end;
                continue;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }

    private bool IsDateTime(string plain)
    {
        plain = System.Text.RegularExpressions.Regex.Replace(plain, "[sS]\\.0+", "s");
        var withoutAmPm = plain.Replace("AM/PM", "", StringComparison.OrdinalIgnoreCase).Replace("A/P", "", StringComparison.OrdinalIgnoreCase);
        if (plain.Length != withoutAmPm.Length) return true;
        return withoutAmPm.Any(c => char.ToLowerInvariant(c) is 'y' or 'd' or 'h' or 's' or 'm') && !withoutAmPm.Contains('0') && !withoutAmPm.Contains('#')
            || withoutAmPm.Any(c => char.ToLowerInvariant(c) is 'y' or 'd' or 'h');
    }

    private bool HasMonth(string plain) => plain.Contains('m', StringComparison.OrdinalIgnoreCase);

    private bool HasTimeNeighbour(string plain) => plain.IndexOfAny(['h', 'H', 's', 'S']) >= 0;

    private string? CurrencySymbol(string section)
    {
        var plain = Unquoted(section);
        if (plain.IndexOfAny(['€', '$', '£', '¥']) >= 0) return "symbol";
        return section.Contains("[$", StringComparison.Ordinal) ? "symbol" : null;
    }

    private string? Colour(string section)
    {
        var start = section.IndexOf('[');
        while (start >= 0)
        {
            var end = section.IndexOf(']', start);
            if (end < 0) return null;
            var name = section[(start + 1)..end].ToLowerInvariant();
            var hex = name switch
            {
                "red" => "#ff0000", "blue" => "#0000ff", "green" => "#00ff00", "black" => "#000000",
                "white" => "#ffffff", "yellow" => "#ffff00", "magenta" => "#ff00ff", "cyan" => "#00ffff",
                _ => null,
            };
            if (hex is not null) return hex;
            start = section.IndexOf('[', end);
        }
        return null;
    }

    private int Run(string text, int start, char c)
    {
        var length = 0;
        while (start + length < text.Length && char.ToLowerInvariant(text[start + length]) == c) length++;
        return length;
    }

    private bool Matches(string text, int index, string token) =>
        index + token.Length <= text.Length && string.Compare(text, index, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private bool NextIsSecond(string text, int index)
    {
        for (var j = index; j < text.Length; j++)
        {
            var c = char.ToLowerInvariant(text[j]);
            if (c == 's') return true;
            if (char.IsLetter(c)) return false;
        }
        return false;
    }
}

internal sealed class OdfNumberStyleReader
{
    private readonly DatePartTable _dates = new();
    private readonly Dictionary<string, XElement> _styles = new(StringComparer.Ordinal);

    public OdfNumberStyleReader(params XDocument?[] documents)
    {
        foreach (var document in documents)
        {
            if (document is null) continue;
            foreach (var element in document.Descendants().Where(element => element.Name.Namespace == Namespaces.Number && element.Name.LocalName.EndsWith("-style", StringComparison.Ordinal)))
                if (element.Attribute(Namespaces.Style + "name")?.Value is { } name) _styles.TryAdd(name, element);
        }
    }

    public string? CodeOf(string? dataStyleName)
    {
        if (dataStyleName is null || !_styles.TryGetValue(dataStyleName, out var style)) return null;
        var main = Section(style);
        if (main is null) return null;

        var mapped = style.Elements(Namespaces.Style + "map")
            .Select(map => (Condition: map.Attribute(Namespaces.Style + "condition")?.Value?.Replace(" ", "", StringComparison.Ordinal) ?? string.Empty,
                Target: map.Attribute(Namespaces.Style + "apply-style-name")?.Value))
            .Where(map => map.Target is not null && _styles.ContainsKey(map.Target))
            .Select(map => (map.Condition, Code: Section(_styles[map.Target!])))
            .Where(map => map.Code is not null)
            .Select(map => (map.Condition, Code: map.Code!))
            .ToList();
        return _sections.Compose(main, mapped);
    }

    private readonly NumberSectionComposer _sections = new();

    private readonly NumberFormatLocale _locale = new();

    private string? Section(XElement style)
    {
        var builder = new StringBuilder(_locale.Tag(style));
        if (style.Element(Namespaces.Style + "text-properties")?.Attribute(Namespaces.Fo + "color")?.Value is { } colour && ColourName(colour) is { } name)
            builder.Append('[').Append(name).Append(']');
        var elapsed = style.Attribute(Namespaces.Number + "truncate-on-overflow")?.Value == "false";
        var firstTime = true;

        foreach (var child in style.Elements())
        {
            if (child.Name.Namespace != Namespaces.Number) continue;
            switch (child.Name.LocalName)
            {
                case "text":
                    builder.Append(Literal(child.Value, style.Name.LocalName == "percentage-style"));
                    break;
                case "number":
                case "scientific-number":
                    builder.Append(Number(child));
                    break;
                case "fraction":
                    builder.Append(Fraction(child));
                    break;
                case "currency-symbol":
                    builder.Append("[$").Append(Symbol(child.Value)).Append(Locale(child)).Append(']');
                    break;
                case "text-content":
                    builder.Append('@');
                    break;
                case "am-pm":
                    builder.Append("AM/PM");
                    break;
                case "boolean":
                    builder.Append(OdfNumberStyleWriter.BooleanKeyword);
                    break;
                default:
                    if (_dates.Reverse(child) is { } part)
                    {
                        var code = part.Code;
                        if (elapsed && firstTime && part.Element is "hours" or "minutes") code = "[" + code + "]";
                        if (part.Element is "hours" or "minutes" or "seconds") firstTime = false;
                        builder.Append(code);
                        if (part.Element == "seconds" && int.TryParse(child.Attribute(Namespaces.Number + "decimal-places")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimals) && decimals > 0)
                            builder.Append('.').Append('0', Math.Min(decimals, 9));
                    }
                    break;
            }
        }
        var result = builder.ToString();
        return result.Length == 0 ? null : result;
    }

    private string Symbol(string value) => new([.. value.Where(c => c is not ('[' or ']' or ';' or '"'))]);

    private string Locale(XElement currency)
    {
        if (currency.Attribute(Namespaces.Number + "language")?.Value is not { Length: > 0 } language) return string.Empty;
        var country = currency.Attribute(Namespaces.Number + "country")?.Value;
        try
        {
            var lcid = CultureInfo.GetCultureInfo(country is { Length: > 0 } ? $"{language}-{country}" : language).LCID;
            return lcid is > 0 and not 4096 ? "-" + lcid.ToString("X", CultureInfo.InvariantCulture) : string.Empty;
        }
        catch (CultureNotFoundException)
        {
            return string.Empty;
        }
    }

    private string Literal(string text, bool percentage)
    {
        if (text.Length == 0) return string.Empty;
        if (text.All(c => c is ' ' or '-' or '/' or ':' or ',' or '.' or '(' or ')' || (percentage && c == '%'))) return text;
        return "\"" + text.Replace("\"", "", StringComparison.Ordinal) + "\"";
    }

    private string Number(XElement element)
    {
        if (IsGeneral(element)) return "General";
        var decimals = Int(element, "decimal-places", 0);
        var minDecimals = Int(element, "min-decimal-places", decimals);
        var minInteger = Int(element, "min-integer-digits", 1);
        var grouping = element.Attribute(Namespaces.Number + "grouping")?.Value == "true";

        var integer = grouping ? Grouped(minInteger) : new string('0', minInteger);
        if (integer.Length == 0) integer = "#";
        if (element.Name.LocalName == "scientific-number" && ExponentInterval(element) is > 1 and var interval && integer.Length < interval)
            integer = new string('#', interval - integer.Length) + integer;
        var result = integer;
        if (decimals > 0) result += "." + new string('0', Math.Min(minDecimals, decimals)) + new string('#', decimals - Math.Min(minDecimals, decimals));

        if (double.TryParse(element.Attribute(Namespaces.Number + "display-factor")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) && factor >= 1000 && double.IsFinite(factor))
            result += new string(',', Math.Min((int)Math.Round(Math.Log(factor, 1000)), MostScaleCommas));

        if (element.Name.LocalName == "scientific-number")
            result += "E+" + new string('0', Math.Max(1, Int(element, "min-exponent-digits", 2)));
        return result;
    }

    private int ExponentInterval(XElement element) =>
        int.TryParse((element.Attribute(Namespaces.Number + "exponent-interval") ?? element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "exponent-interval"))?.Value,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) ? Math.Clamp(interval, 1, MaxDigits) : 1;

    private bool IsGeneral(XElement element) =>
        element.Name.LocalName == "number"
        && element.Attribute(Namespaces.Number + "decimal-places") is null
        && element.Attribute(Namespaces.Number + "grouping")?.Value != "true"
        && element.Attribute(Namespaces.Number + "display-factor") is null
        && Int(element, "min-integer-digits", 1) <= 1;

    private string Grouped(int digits)
    {
        var builder = new StringBuilder(new string('#', Math.Max(0, 4 - digits)) + new string('0', digits));
        for (var i = builder.Length - 3; i > 0; i -= 3) builder.Insert(i, ',');
        return builder.ToString();
    }

    private string Fraction(XElement element)
    {
        var whole = Int(element, "min-integer-digits", 0);
        var numerator = Int(element, "min-numerator-digits", 1);
        var denominator = element.Attribute(Namespaces.Number + "denominator-value")?.Value is { Length: > 0 and <= MaxDenominatorDigits } fixedValue && fixedValue.All(char.IsAsciiDigit)
            ? fixedValue
            : new string('?', Int(element, "min-denominator-digits", 1));
        var prefix = whole > 0 ? new string('0', whole) + " " : "# ";
        return prefix + new string('?', numerator) + "/" + denominator;
    }

    private int Int(XElement element, string attribute, int fallback) =>
        int.TryParse(element.Attribute(Namespaces.Number + attribute)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 0, MaxDigits) : fallback;

    private const int MaxDigits = 64;

    private const int MostScaleCommas = 5;

    private const int MaxDenominatorDigits = 9;

    private string? ColourName(string hex) => hex.ToLowerInvariant() switch
    {
        "#ff0000" => "RED", "#0000ff" => "BLUE", "#00ff00" => "GREEN", "#000000" => "BLACK",
        "#ffffff" => "WHITE", "#ffff00" => "YELLOW", "#ff00ff" => "MAGENTA", "#00ffff" => "CYAN",
        _ => null,
    };
}
