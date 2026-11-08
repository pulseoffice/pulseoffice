using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfPrintMasters(IHandoutLayout layout)
{
    public const string PageLayoutName = "PL2";

    private const double PageWidth = 21.0;
    private const double PageHeight = 29.7;

    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    private static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Pulse = "urn:pulseoffice:presentation-master";

    private static readonly PageBox HeaderBox = new(0.057, 0.02, 0.43, 0.035);
    private static readonly PageBox FooterBox = new(0.057, 0.945, 0.43, 0.035);
    private static readonly PageBox NumberBox = new(0.513, 0.945, 0.43, 0.035);

    public OdfPrintMasters() : this(new HandoutLayout()) { }

    public XElement PageLayout() =>
        new(Style + "page-layout",
            new XAttribute(Style + "name", PageLayoutName),
            new XElement(Style + "page-layout-properties",
                new XAttribute(Fo + "page-width", Cm(PageWidth)),
                new XAttribute(Fo + "page-height", Cm(PageHeight)),
                new XAttribute(Style + "print-orientation", "portrait")));

    public XElement Notes(NotesMaster master)
    {
        ArgumentNullException.ThrowIfNull(master);
        var notes = new XElement(Presentation + "notes",
            new XAttribute(Style + "page-layout-name", PageLayoutName),
            Thumbnail(master.SlideBox, "page"),
            Frame(master.NotesBox, "notes", null, new XAttribute(Pulse + "font-size", master.FontSize.ToString("0.##", CultureInfo.InvariantCulture))));
        Chrome(notes, master.Header, master.Footer, master.PageNumber);
        return notes;
    }

    public IEnumerable<XElement> NotesStyles(IEnumerable<string> masterNames, NotesMaster master)
    {
        ArgumentNullException.ThrowIfNull(masterNames);
        ArgumentNullException.ThrowIfNull(master);
        return masterNames.Select(name => new XElement(Style + "style",
            new XAttribute(Style + "name", System.Xml.XmlConvert.EncodeLocalName($"{name}-notes")),
            new XAttribute(Style + "display-name", $"{name}-notes"),
            new XAttribute(Style + "family", "presentation"),
            new XElement(Style + "text-properties",
                new XAttribute(Fo + "font-size", master.FontSize.ToString("0.##", CultureInfo.InvariantCulture) + "pt"))));
    }

    public XElement SlideNotes(NotesMaster master, string notes, string styleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(styleName);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(notes);

        return new XElement(Presentation + "notes",
            Thumbnail(master.SlideBox, "page"),
            new XElement(Draw + "frame",
                new XAttribute(Presentation + "style-name", styleName),
                new XAttribute(Draw + "layer", "layout"),
                new XAttribute(Presentation + "class", "notes"),
                Geometry(master.NotesBox),
                new XElement(Draw + "text-box",
                    notes.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => new XElement(Text + "p", line)))));
    }

    private double? NamedNotesSize(XElement masterStyles, XElement? namedStyles)
    {
        var page = masterStyles.Element(Style + "master-page");
        var wanted = new[] { (string?)page?.Attribute(Style + "display-name"), (string?)page?.Attribute(Style + "name") }
            .OfType<string>().Select(name => $"{name}-notes").ToHashSet(StringComparer.Ordinal);
        var value = (string?)namedStyles?.Elements(Style + "style")
            .FirstOrDefault(style => wanted.Contains((string?)style.Attribute(Style + "display-name") ?? (string?)style.Attribute(Style + "name") ?? string.Empty))?
            .Element(Style + "text-properties")?.Attribute(Fo + "font-size");
        return value is { Length: > 2 } && value.EndsWith("pt", StringComparison.Ordinal)
            && double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var points) ? points : null;
    }

    public XElement Handout(HandoutMaster master)
    {
        ArgumentNullException.ThrowIfNull(master);
        var handout = new XElement(Style + "handout-master",
            new XAttribute(Style + "page-layout-name", PageLayoutName),
            layout.Cells(master.SlidesPerPage).Select(cell => Thumbnail(cell, "handout")));
        Chrome(handout, master.Header, master.Footer, master.PageNumber);
        return handout;
    }

    public void Read(XElement? masterStyles, PulseOffice.Presentations.Presentation deck, XElement? namedStyles = null)
    {
        ArgumentNullException.ThrowIfNull(deck);
        if (masterStyles is null) return;

        if (masterStyles.Elements(Style + "master-page").Select(page => page.Element(Presentation + "notes")).OfType<XElement>().FirstOrDefault() is { } notes)
        {
            var master = deck.NotesMaster;
            var thumbnail = notes.Element(Draw + "page-thumbnail");
            var body = Frames(notes).FirstOrDefault(frame => Class(frame) == "notes");
            deck.NotesMaster = master with
            {
                SlideBox = thumbnail is null ? master.SlideBox : BoxOf(thumbnail),
                NotesBox = body is null ? master.NotesBox : BoxOf(body),
                FontSize = double.TryParse((string?)body?.Attribute(Pulse + "font-size"), NumberStyles.Float, CultureInfo.InvariantCulture, out var size) ? size : NamedNotesSize(masterStyles, namedStyles) ?? master.FontSize,
                Header = TextOf(notes, "header"),
                Footer = TextOf(notes, "footer"),
                PageNumber = Frames(notes).Any(frame => Class(frame) == "page-number"),
            };
        }

        if (masterStyles.Element(Style + "handout-master") is { } handout)
        {
            var count = handout.Elements(Draw + "page-thumbnail").Count();
            deck.Handout = deck.Handout with
            {
                SlidesPerPage = count == 0 ? deck.Handout.SlidesPerPage : layout.Nearest(count),
                Header = TextOf(handout, "header"),
                Footer = TextOf(handout, "footer"),
                PageNumber = Frames(handout).Any(frame => Class(frame) == "page-number"),
            };
        }
    }

    private void Chrome(XElement owner, string header, string footer, bool pageNumber)
    {
        owner.Add(Frame(HeaderBox, "header", header));
        owner.Add(Frame(FooterBox, "footer", footer));
        if (pageNumber) owner.Add(Frame(NumberBox, "page-number", null, null, new XElement(Text + "page-number")));
    }

    private XElement Thumbnail(PageBox box, string kind) =>
        new(Draw + "page-thumbnail", new XAttribute(Presentation + "class", kind), Geometry(box));

    private XElement Frame(PageBox box, string kind, string? text, XAttribute? extra = null, XElement? field = null)
    {
        var paragraph = new XElement(Text + "p");
        if (!string.IsNullOrEmpty(text)) paragraph.Add(text);
        if (field is not null) paragraph.Add(field);

        return new XElement(Draw + "frame",
            new XAttribute(Presentation + "style-name", "Mpr1"),
            new XAttribute(Draw + "layer", "backgroundobjects"),
            new XAttribute(Presentation + "placeholder", "true"),
            new XAttribute(Presentation + "class", kind),
            extra,
            Geometry(box),
            new XElement(Draw + "text-box", paragraph));
    }

    private IEnumerable<XAttribute> Geometry(PageBox box) =>
    [
        new(Svg + "x", Cm(box.X * PageWidth)),
        new(Svg + "y", Cm(box.Y * PageHeight)),
        new(Svg + "width", Cm(box.Width * PageWidth)),
        new(Svg + "height", Cm(box.Height * PageHeight)),
    ];

    private PageBox BoxOf(XElement element) => new PageBox(
        Length(element, "x") / PageWidth,
        Length(element, "y") / PageHeight,
        Length(element, "width") / PageWidth,
        Length(element, "height") / PageHeight).Clamped();

    private double Length(XElement element, string name)
    {
        return (new OdfLength().Parse((string?)element.Attribute(Svg + name)) ?? PulseOffice.Foundation.Units.Twip.Zero).ToCentimeters();
    }

    private IEnumerable<XElement> Frames(XElement owner) => owner.Elements(Draw + "frame");

    private string? Class(XElement frame) => (string?)frame.Attribute(Presentation + "class");

    private string TextOf(XElement owner, string kind) =>
        Frames(owner).FirstOrDefault(frame => Class(frame) == kind)?.Descendants(Text + "p").FirstOrDefault()?.Value.Trim() ?? string.Empty;

    private string Cm(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "cm";
}
