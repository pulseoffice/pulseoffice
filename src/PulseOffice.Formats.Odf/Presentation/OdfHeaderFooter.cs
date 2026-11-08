using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfHeaderFooter(IHeaderFooterDateFormats formats)
{
    public OdfHeaderFooter()
        : this(new HeaderFooterDateFormats())
    {
    }

    public const string FooterName = "ftr1";
    public const string DateName = "dtd1";
    public const string DateStyleName = "PulseHeaderFooterDate";

    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    private static readonly XNamespace Number = OdfPackage.Namespaces.Number;
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;

    private readonly IHeaderFooterDateFormats _formats = formats;

    public IEnumerable<XElement> Declarations(HeaderFooterSettings settings, XElement automaticStyles)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(automaticStyles);
        if (settings.ShowDate && !settings.DateIsFixed) automaticStyles.Add(DataStyle(_formats.For(settings.DateFormat)));

        if (settings.ShowFooter)
            yield return new XElement(Presentation + "footer-decl",
                new XAttribute(Presentation + "name", FooterName), settings.FooterText);

        if (settings.ShowDate)
            yield return new XElement(Presentation + "date-time-decl",
                new XAttribute(Presentation + "name", DateName),
                new XAttribute(Presentation + "source", settings.DateIsFixed ? "fixed" : "current-date"),
                settings.DateIsFixed ? null : new XAttribute(Style + "data-style-name", DateStyleName),
                settings.DateIsFixed ? settings.FixedDate : string.Empty);
    }

    public void Use(XElement page, HeaderFooterSettings settings)
    {
        if (settings.ShowFooter) page.Add(new XAttribute(Presentation + "use-footer-name", FooterName));
        if (settings.ShowDate) page.Add(new XAttribute(Presentation + "use-date-time-name", DateName));
    }

    public void Display(XElement properties, HeaderFooterSettings settings, Slide slide)
    {
        var shown = slide.ShowMasterObjects && (slide.Layout != SlideLayout.TitleSlide || settings.ShowOnTitleSlide);
        properties.Add(
            new XAttribute(Presentation + "display-footer", Flag(shown && settings.ShowFooter)),
            new XAttribute(Presentation + "display-page-number", Flag(shown && settings.ShowSlideNumber)),
            new XAttribute(Presentation + "display-date-time", Flag(shown && settings.ShowDate)));
    }

    public XElement FieldFor(string kind) => kind switch
    {
        "footer" => new XElement(Text + "p", new XElement(Presentation + "footer")),
        "date-time" => new XElement(Text + "p", new XElement(Presentation + "date-time")),
        _ => new XElement(Text + "p", new XElement(Text + "page-number", "<number>")),
    };

    public HeaderFooterSettings? Read(XElement presentation, IReadOnlyDictionary<string, XElement> pageStyles, IReadOnlyDictionary<string, XElement> dataStyles)
    {
        var footer = presentation.Elements(Presentation + "footer-decl").FirstOrDefault();
        var date = presentation.Elements(Presentation + "date-time-decl").FirstOrDefault();
        var displays = presentation.Elements(OdfPackage.Namespaces.Draw + "page")
            .Select(page => (string?)page.Attribute(OdfPackage.Namespaces.Draw + "style-name"))
            .Select(name => name is not null && pageStyles.TryGetValue(name, out var style)
                ? style.Element(OdfPackage.Namespaces.Style + "drawing-page-properties")
                : null)
            .Where(properties => properties is not null)
            .ToList();

        var classes = presentation.Elements(OdfPackage.Namespaces.Draw + "page")
            .SelectMany(page => page.Elements(OdfPackage.Namespaces.Draw + "frame"))
            .Select(frame => (string?)frame.Attribute(Presentation + "class"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        bool Shown(string attribute, string kind) =>
            classes.Contains(kind) || displays.Any(properties => (string?)properties!.Attribute(Presentation + attribute) == "true");

        var settings = new HeaderFooterSettings
        {
            ShowFooter = (footer is not null && Shown("display-footer", "footer")) || classes.Contains("footer"),
            FooterText = footer?.Value ?? string.Empty,
            ShowDate = (date is not null && Shown("display-date-time", "date-time")) || classes.Contains("date-time"),
            DateIsFixed = (string?)date?.Attribute(Presentation + "source") == "fixed",
            FixedDate = (string?)date?.Attribute(Presentation + "source") == "fixed" ? date!.Value : string.Empty,
            DateFormat = DateFormatOf(date, dataStyles).Format,
            ShowSlideNumber = Shown("display-page-number", "page-number"),
            ShowOnTitleSlide = true,
        };
        return settings.Any ? settings : null;
    }

    public bool DateFormatApproximated(XElement presentation, IReadOnlyDictionary<string, XElement> dataStyles) =>
        presentation.Elements(Presentation + "date-time-decl").FirstOrDefault() is { } date
        && (string?)date.Attribute(Presentation + "source") != "fixed"
        && DateFormatOf(date, dataStyles).Approximated;

    private (HeaderFooterDateFormat Format, bool Approximated) DateFormatOf(XElement? date, IReadOnlyDictionary<string, XElement> dataStyles)
    {
        if ((string?)date?.Attribute(Style + "data-style-name") is not { } name || !dataStyles.TryGetValue(name, out var style))
            return (HeaderFooterDateFormat.ShortDate, false);
        var parts = style.Elements().Where(element => element.Name.Namespace == Number && element.Name.LocalName != "text").ToList();
        var signature = string.Join(" ", parts.Select(PartOf).Select(part => part.Signature).Where(text => text.Length > 0));
        if (_formats.FromSignature(signature) is { } exact) return (exact, false);
        var read = parts.Select(PartOf).ToList();
        var month = read.FirstOrDefault(part => part.Kind == DateFieldPartKind.Month);
        return (_formats.Nearest([.. read.Select(part => part.Kind)], month?.Textual == true, month?.Long == true), true);
    }

    private DateFieldPart PartOf(XElement element)
    {
        var isLong = (string?)element.Attribute(Number + "style") == "long";
        var textual = (string?)element.Attribute(Number + "textual") == "true";
        return element.Name.LocalName switch
        {
            "day-of-week" => new DateFieldPart(DateFieldPartKind.DayOfWeek, isLong),
            "day" => new DateFieldPart(DateFieldPartKind.Day),
            "month" => new DateFieldPart(DateFieldPartKind.Month, textual && isLong, textual),
            "year" => new DateFieldPart(DateFieldPartKind.Year, isLong),
            "hours" => new DateFieldPart(DateFieldPartKind.Hours, !HasAmPm(element.Parent)),
            "minutes" => new DateFieldPart(DateFieldPartKind.Minutes, true),
            "seconds" => new DateFieldPart(DateFieldPartKind.Seconds, true),
            "am-pm" => new DateFieldPart(DateFieldPartKind.AmPm),
            _ => new DateFieldPart(DateFieldPartKind.Literal),
        };
    }

    private bool HasAmPm(XElement? style) => style?.Element(Number + "am-pm") is not null;

    private XElement DataStyle(HeaderFooterDateFormatInfo info) =>
        new(Number + (info.HasDate ? "date-style" : "time-style"),
            new XAttribute(Style + "name", DateStyleName),
            info.AutomaticOrder ? new XAttribute(Number + "automatic-order", "true") : null,
            info.Parts.Select(Element));

    private XElement Element(DateFieldPart part) => part.Kind switch
    {
        DateFieldPartKind.DayOfWeek => new XElement(Number + "day-of-week", Long(part.Long)),
        DateFieldPartKind.Day => new XElement(Number + "day"),
        DateFieldPartKind.Month => new XElement(Number + "month", Long(part.Long), part.Textual ? new XAttribute(Number + "textual", "true") : null),
        DateFieldPartKind.Year => new XElement(Number + "year", Long(part.Long)),
        DateFieldPartKind.Hours => new XElement(Number + "hours", Long(part.Long)),
        DateFieldPartKind.Minutes => new XElement(Number + "minutes", Long(true)),
        DateFieldPartKind.Seconds => new XElement(Number + "seconds", Long(true)),
        DateFieldPartKind.AmPm => new XElement(Number + "am-pm"),
        _ => new XElement(Number + "text", part.Text),
    };

    private XAttribute? Long(bool value) => value ? new XAttribute(Number + "style", "long") : null;

    private string Flag(bool value) => value ? "true" : "false";
}
