using System.Text;
using System.Xml.Linq;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtDatePictures : OdtXml
{
    private readonly XNamespace Number = OdfPackage.Namespaces.Number;

    public XElement? DataStyle(string picture, string name, bool time, string? language = null)
    {
        var separators = Culture(language).DateTimeFormat;
        if (picture.Length < 2) return null;
        var parts = new List<XElement>();
        var literal = new StringBuilder();
        var twelveHour = picture.Contains('t', StringComparison.Ordinal);
        var i = 0;
        while (i < picture.Length)
        {
            var c = picture[i];
            if (c is '\'' or '"')
            {
                var end = picture.IndexOf(c, i + 1);
                if (end < 0) return null;
                literal.Append(picture, i + 1, end - i - 1);
                i = end + 1;
                continue;
            }
            if (c == '\\' && i + 1 < picture.Length)
            {
                literal.Append(picture[i + 1]);
                i += 2;
                continue;
            }
            var run = 1;
            while (i + run < picture.Length && picture[i + run] == c) run++;
            var part = Part(c, run, twelveHour);
            if (part is null && char.IsAsciiLetter(c)) return null;
            if (part is null && c is '/' or ':')
            {
                for (var repeat = 0; repeat < run; repeat++) literal.Append(c == '/' ? separators.DateSeparator : separators.TimeSeparator);
                i += run;
                continue;
            }
            if (part is null)
            {
                literal.Append(picture, i, run);
                i += run;
                continue;
            }
            if (literal.Length > 0) parts.Add(new XElement(Number + "text", literal.ToString()));
            literal.Clear();
            parts.Add(part);
            i += run;
        }
        if (literal.Length > 0) parts.Add(new XElement(Number + "text", literal.ToString()));
        return new XElement(Number + (time ? "time-style" : "date-style"), new XAttribute(Style + "name", name), parts);
    }

    private XElement? Part(char c, int run, bool twelveHour) => c switch
    {
        'd' when run >= 3 => Element("day-of-week", run >= 4),
        'd' => Element("day", run >= 2),
        'M' => run >= 3
            ? new XElement(Number + "month", new XAttribute(Number + "textual", "true"), run >= 4 ? new XAttribute(Number + "style", "long") : null)
            : Element("month", run >= 2),
        'y' => Element("year", run >= 3),
        'H' or 'h' => Element("hours", run >= 2),
        'm' => Element("minutes", run >= 2),
        's' => Element("seconds", run >= 2),
        't' when twelveHour => new XElement(Number + "am-pm"),
        'g' => Element("era", run >= 2),
        _ => null,
    };

    private XElement Element(string name, bool full) =>
        new(Number + name, full ? new XAttribute(Number + "style", "long") : null);

    public string? Picture(XElement? style)
    {
        if (style is null || style.Name != Number + "date-style" && style.Name != Number + "time-style") return null;
        var twelveHour = style.Element(Number + "am-pm") is not null;
        var builder = new StringBuilder();
        foreach (var part in style.Elements())
        {
            var full = (string?)part.Attribute(Number + "style") == "long";
            var piece = part.Name.LocalName switch
            {
                "day" => full ? "dd" : "d",
                "day-of-week" => full ? "dddd" : "ddd",
                "month" when (string?)part.Attribute(Number + "textual") == "true" => full ? "MMMM" : "MMM",
                "month" => full ? "MM" : "M",
                "year" => full ? "yyyy" : "yy",
                "hours" => twelveHour ? full ? "hh" : "h" : full ? "HH" : "H",
                "minutes" => full ? "mm" : "m",
                "seconds" => full ? "ss" : "s",
                "am-pm" => "tt",
                "era" => full ? "gg" : "g",
                "text" => Quoted(part.Value),
                _ => null,
            };
            if (piece is null) return null;
            builder.Append(piece);
        }
        return builder.Length == 0 ? null : builder.ToString();
    }

    private System.Globalization.CultureInfo Culture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(language);
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            return System.Globalization.CultureInfo.InvariantCulture;
        }
    }

    private string Quoted(string text) =>
        text.All(c => !char.IsLetter(c) && c is not (':' or '/' or '%' or '\\' or '\'' or '"'))
            ? text
            : "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
