using System.Xml.Linq;

namespace PulseOffice.Presentations;

public sealed class HeaderFooterXml
{
    public const string Namespace = "urn:pulseoffice:headerfooter";

    private readonly XNamespace _ns = Namespace;

    private readonly IReadOnlyList<HeaderFooterField> _fields =
    [
        new FlagField("date", s => s.ShowDate, (s, v) => s with { ShowDate = v }),
        new FlagField("dateFixed", s => s.DateIsFixed, (s, v) => s with { DateIsFixed = v }),
        new TextField("fixedDate", s => s.FixedDate, (s, v) => s with { FixedDate = v }),
        new TextField("dateFormat", s => ((int)s.DateFormat).ToString(System.Globalization.CultureInfo.InvariantCulture),
            (s, v) => s with { DateFormat = DateFormatOf(v) }),
        new FlagField("footer", s => s.ShowFooter, (s, v) => s with { ShowFooter = v }),
        new TextField("footerText", s => s.FooterText, (s, v) => s with { FooterText = v }),
        new FlagField("number", s => s.ShowSlideNumber, (s, v) => s with { ShowSlideNumber = v }),
        new FlagField("onTitle", s => s.ShowOnTitleSlide, (s, v) => s with { ShowOnTitleSlide = v }),
    ];

    private static HeaderFooterDateFormat DateFormatOf(string value) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
        && Enum.IsDefined((HeaderFooterDateFormat)number)
            ? (HeaderFooterDateFormat)number
            : HeaderFooterDateFormat.ShortDate;

    public string Attributes(HeaderFooterSettings settings) =>
        string.Join(" ", Element(settings).Attributes().Select(attribute => attribute.ToString()));

    public XElement Element(HeaderFooterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new XElement(_ns + "hf", _fields.Select(field => field.Write(settings)));
    }

    public HeaderFooterSettings? Parse(XElement? element) =>
        element is null ? null : _fields.Aggregate(new HeaderFooterSettings(), (settings, field) => field.Read(element, settings));

    public XElement? Find(XElement root) => root.Descendants(_ns + "hf").FirstOrDefault();
}

internal abstract class HeaderFooterField(string name)
{
    protected string Name => name;

    public abstract XAttribute Write(HeaderFooterSettings settings);

    public abstract HeaderFooterSettings Read(XElement element, HeaderFooterSettings settings);
}

internal sealed class FlagField(
    string name, Func<HeaderFooterSettings, bool> get, Func<HeaderFooterSettings, bool, HeaderFooterSettings> set)
    : HeaderFooterField(name)
{
    public override XAttribute Write(HeaderFooterSettings settings) => new(Name, get(settings) ? "1" : "0");

    public override HeaderFooterSettings Read(XElement element, HeaderFooterSettings settings) =>
        set(settings, (string?)element.Attribute(Name) == "1");
}

internal sealed class TextField(
    string name, Func<HeaderFooterSettings, string> get, Func<HeaderFooterSettings, string, HeaderFooterSettings> set)
    : HeaderFooterField(name)
{
    public override XAttribute Write(HeaderFooterSettings settings) => new(Name, get(settings));

    public override HeaderFooterSettings Read(XElement element, HeaderFooterSettings settings) =>
        set(settings, (string?)element.Attribute(Name) ?? string.Empty);
}
