using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed record HeaderFooterField(string Code, XName Element);

internal sealed class OdfHeaderFooterText(IHeaderFooterCodes codes)
{
    private readonly IReadOnlyList<HeaderFooterField> _fields =
    [
        new("&P", Namespaces.Text + "page-number"),
        new("&N", Namespaces.Text + "page-count"),
        new("&D", Namespaces.Text + "date"),
        new("&T", Namespaces.Text + "time"),
        new("&F", Namespaces.Text + "file-name"),
        new("&A", Namespaces.Text + "sheet-name"),
    ];

    private readonly IHeaderFooterCodes _codes = codes;

    public bool HasFormatting(HeaderFooter content) =>
        new[] { content.Left, content.Center, content.Right }
            .Any(region => _codes.Tokens(region).Any(token => token.Kind == HeaderFooterTokenKind.Formatting));

    public XElement Paragraph(string text)
    {
        var paragraph = new XElement(Namespaces.Text + "p");
        foreach (var token in _codes.Tokens(text))
        {
            if (token.Kind == HeaderFooterTokenKind.Text) paragraph.Add(token.Value);
            else if (token.Kind == HeaderFooterTokenKind.Field
                && _fields.FirstOrDefault(field => field.Code == "&" + (token.Value == "Z" ? "F" : token.Value)) is { } match)
                paragraph.Add(new XElement(match.Element));
        }
        return paragraph;
    }

    public string Read(XElement? region)
    {
        if (region is null) return string.Empty;
        var text = new StringBuilder();
        foreach (var paragraph in region.Elements(Namespaces.Text + "p"))
        {
            if (text.Length > 0) text.Append('\n');
            Append(paragraph, text);
        }
        return text.ToString();
    }

    private void Append(XElement container, StringBuilder text)
    {
        foreach (var node in container.Nodes())
        {
            if (node is XText literal) text.Append(literal.Value.Replace("&", "&&", StringComparison.Ordinal));
            else if (node is XElement element)
            {
                if (_fields.FirstOrDefault(field => field.Element == element.Name) is { } field) text.Append(field.Code);
                else if (element.Name == Namespaces.Text + "s") text.Append(' ');
                else Append(element, text);
            }
        }
    }
}

internal sealed class OdfPageStyleMapper
{
    private readonly OdfLength _length = new();
    private readonly OdfStyleNames _names = new();
    private readonly OdfHeaderFooterText _text = new(new HeaderFooterCodes());
    private readonly IPaperCatalog _papers;

    public OdfPageStyleMapper(IPaperCatalog? papers = null) => _papers = papers ?? new PaperCatalog();

    private readonly TokenMap<PageOrder> _orders = new(PageOrder.DownThenAcross, "ttb",
        (PageOrder.DownThenAcross, "ttb"), (PageOrder.AcrossThenDown, "ltr"));

    public string MasterName(string pageStyle) => _names.Encode(pageStyle);

    public (IReadOnlyList<XElement> Layouts, IReadOnlyList<XElement> Masters) Build(PageStyleCatalog catalog)
    {
        var layouts = new List<XElement>();
        var masters = new List<XElement>();
        foreach (var (style, index) in catalog.All.Select((style, index) => (style, index)))
        {
            var layout = $"pm{index + 1}";
            layouts.Add(Layout(layout, style.Setup));
            masters.Add(Master(style, layout));
        }
        return (layouts, masters);
    }

    private XElement Layout(string name, PageSetup setup)
    {
        var properties = new XElement(Namespaces.Style + "page-layout-properties",
            new XAttribute(Namespaces.Fo + "page-width", Measure(setup.PageWidth)),
            new XAttribute(Namespaces.Fo + "page-height", Measure(setup.PageHeight)),
            new XAttribute(Namespaces.Style + "print-orientation", setup.Orientation == PageOrientation.Landscape ? "landscape" : "portrait"),
            new XAttribute(Namespaces.Fo + "margin-top", Measure(setup.Margins.Top)),
            new XAttribute(Namespaces.Fo + "margin-bottom", Measure(setup.Margins.Bottom)),
            new XAttribute(Namespaces.Fo + "margin-left", Measure(setup.Margins.Left)),
            new XAttribute(Namespaces.Fo + "margin-right", Measure(setup.Margins.Right)),
            new XAttribute(Namespaces.Style + "print-page-order", _orders.Token(setup.Order)),
            new XAttribute(Namespaces.Style + "print", Print(setup)),
            new XAttribute(Namespaces.Style + "table-centering", Centering(setup)));

        if (setup.FitToWidthPages is { } across) properties.Add(new XAttribute(Namespaces.Style + "scale-to-X", Integer(across)));
        if (setup.FitToHeightPages is { } down) properties.Add(new XAttribute(Namespaces.Style + "scale-to-Y", Integer(down)));
        if (setup.FirstPageNumber is { } first) properties.Add(new XAttribute(Namespaces.Style + "first-page-number", Integer(Math.Max(first, 1))));
        if (!setup.FitsToPages)
            properties.Add(new XAttribute(Namespaces.Style + "scale-to", Math.Round(setup.Scale * 100, 2).ToString(CultureInfo.InvariantCulture) + "%"));

        return new XElement(Namespaces.Style + "page-layout",
            new XAttribute(Namespaces.Style + "name", name),
            properties,
            Band("header-style", setup.HeaderHeight),
            Band("footer-style", setup.FooterHeight));
    }

    private XElement Band(string element, Twip height) =>
        new(Namespaces.Style + element,
            new XElement(Namespaces.Style + "header-footer-properties",
                new XAttribute(Namespaces.Fo + "min-height", Measure(height)),
                new XAttribute(Namespaces.Fo + "margin-left", "0pt"),
                new XAttribute(Namespaces.Fo + "margin-right", "0pt")));

    private XElement Master(PageStyle style, string layout)
    {
        var master = new XElement(Namespaces.Style + "master-page",
            new XAttribute(Namespaces.Style + "name", MasterName(style.Name)),
            new XAttribute(Namespaces.Style + "display-name", style.Name),
            new XAttribute(Namespaces.Style + "page-layout-name", layout));
        var setup = style.Setup;
        master.Add(Regions("header", setup.Header));
        if (setup.DifferentOddEven) master.Add(Regions("header-left", setup.EvenHeader));
        if (setup.DifferentFirstPage) master.Add(Regions("header-first", setup.FirstHeader));
        master.Add(Regions("footer", setup.Footer));
        if (setup.DifferentOddEven) master.Add(Regions("footer-left", setup.EvenFooter));
        if (setup.DifferentFirstPage) master.Add(Regions("footer-first", setup.FirstFooter));
        return master;
    }

    private XElement Regions(string element, HeaderFooter content) =>
        new(Namespaces.Style + element,
            new XAttribute(Namespaces.Style + "display", content.IsEmpty && element is "header" or "footer" ? "false" : "true"),
            new XElement(Namespaces.Style + "region-left", _text.Paragraph(content.Left)),
            new XElement(Namespaces.Style + "region-center", _text.Paragraph(content.Center)),
            new XElement(Namespaces.Style + "region-right", _text.Paragraph(content.Right)));

    public void Read(XDocument? styles, PageStyleCatalog catalog)
    {
        if (styles?.Root is not { } root) return;
        var layouts = root.Descendants(Namespaces.Style + "page-layout")
            .Where(layout => layout.Attribute(Namespaces.Style + "name") is not null)
            .GroupBy(layout => (string)layout.Attribute(Namespaces.Style + "name")!)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var master in root.Descendants(Namespaces.Style + "master-page"))
        {
            if ((string?)master.Attribute(Namespaces.Style + "name") is not { } encoded) continue;
            var name = (string?)master.Attribute(Namespaces.Style + "display-name") ?? _names.Decode(encoded);
            var layout = (string?)master.Attribute(Namespaces.Style + "page-layout-name") is { } layoutName
                ? layouts.GetValueOrDefault(layoutName)
                : null;
            catalog.Define(name, Setup(layout, master), string.Equals(name, "Default", StringComparison.Ordinal));
        }
    }

    public string? SheetStyle(string? masterPageName) => masterPageName is null ? null : _names.Decode(masterPageName);

    private PageSetup Setup(XElement? layout, XElement master)
    {
        var properties = layout?.Element(Namespaces.Style + "page-layout-properties");
        var fallback = new PageSetup { Paper = _papers.Default };
        var orientation = (string?)properties?.Attribute(Namespaces.Style + "print-orientation") == "landscape"
            ? PageOrientation.Landscape
            : PageOrientation.Portrait;
        var width = _length.Parse((string?)properties?.Attribute(Namespaces.Fo + "page-width"));
        var height = _length.Parse((string?)properties?.Attribute(Namespaces.Fo + "page-height"));
        var paper = width is { } w && height is { } h ? Paper(orientation == PageOrientation.Landscape ? (h, w) : (w, h)) : _papers.Default;
        var print = ((string?)properties?.Attribute(Namespaces.Style + "print") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var centering = (string?)properties?.Attribute(Namespaces.Style + "table-centering") ?? "none";

        return fallback with
        {
            Paper = paper,
            Orientation = orientation,
            Margins = new Margins(
                _length.Parse((string?)properties?.Attribute(Namespaces.Fo + "margin-left"), fallback.Margins.Left),
                _length.Parse((string?)properties?.Attribute(Namespaces.Fo + "margin-top"), fallback.Margins.Top),
                _length.Parse((string?)properties?.Attribute(Namespaces.Fo + "margin-right"), fallback.Margins.Right),
                _length.Parse((string?)properties?.Attribute(Namespaces.Fo + "margin-bottom"), fallback.Margins.Bottom)),
            HeaderHeight = BandHeight(layout, "header-style", fallback.HeaderHeight),
            FooterHeight = BandHeight(layout, "footer-style", fallback.FooterHeight),
            Header = Content(master.Element(Namespaces.Style + "header")),
            Footer = Content(master.Element(Namespaces.Style + "footer")),
            DifferentOddEven = Shown(master, "header-left") || Shown(master, "footer-left"),
            EvenHeader = Shown(master, "header-left") ? Content(master.Element(Namespaces.Style + "header-left")) : HeaderFooter.Empty,
            EvenFooter = Shown(master, "footer-left") ? Content(master.Element(Namespaces.Style + "footer-left")) : HeaderFooter.Empty,
            DifferentFirstPage = Shown(master, "header-first") || Shown(master, "footer-first"),
            FirstHeader = Shown(master, "header-first") ? Content(master.Element(Namespaces.Style + "header-first")) : HeaderFooter.Empty,
            FirstFooter = Shown(master, "footer-first") ? Content(master.Element(Namespaces.Style + "footer-first")) : HeaderFooter.Empty,
            FirstPageNumber = Count((string?)properties?.Attribute(Namespaces.Style + "first-page-number")),
            Scale = Scale((string?)properties?.Attribute(Namespaces.Style + "scale-to")),
            FitToWidthPages = Count((string?)properties?.Attribute(Namespaces.Style + "scale-to-X")),
            FitToHeightPages = Count((string?)properties?.Attribute(Namespaces.Style + "scale-to-Y")),
            Order = _orders.Value((string?)properties?.Attribute(Namespaces.Style + "print-page-order")),
            PrintGridLines = print.Contains("grid"),
            PrintHeadings = print.Contains("headers"),
            CenterHorizontally = centering is "horizontal" or "both",
            CenterVertically = centering is "vertical" or "both",
        };
    }

    private PaperSize Paper((Twip Width, Twip Height) size) =>
        _papers.All.FirstOrDefault(paper => Math.Abs(paper.Width.Value - size.Width.Value) <= 20 && Math.Abs(paper.Height.Value - size.Height.Value) <= 20)
        ?? new PaperSize("custom", new DocumentSize(size.Width, size.Height));

    private Twip BandHeight(XElement? layout, string element, Twip fallback) =>
        _length.Parse((string?)layout?.Element(Namespaces.Style + element)?.Element(Namespaces.Style + "header-footer-properties")?.Attribute(Namespaces.Fo + "min-height"), fallback);

    private bool Shown(XElement master, string element) =>
        master.Element(Namespaces.Style + element) is { } band && (string?)band.Attribute(Namespaces.Style + "display") != "false";

    private HeaderFooter Content(XElement? band)
    {
        if (band is null || (string?)band.Attribute(Namespaces.Style + "display") == "false") return HeaderFooter.Empty;
        var left = band.Element(Namespaces.Style + "region-left");
        var center = band.Element(Namespaces.Style + "region-center");
        var right = band.Element(Namespaces.Style + "region-right");
        return left is null && center is null && right is null
            ? new HeaderFooter(Center: _text.Read(band))
            : new HeaderFooter(_text.Read(left), _text.Read(center), _text.Read(right));
    }

    private double Scale(string? text) =>
        text is { Length: > 1 } && text.EndsWith('%')
            && double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0
            ? Math.Clamp(percent, MinimumScalePercent, MaximumScalePercent) / 100
            : 1.0;

    private const double MinimumScalePercent = 10;
    private const double MaximumScalePercent = 400;

    private int? Count(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;

    private string Print(PageSetup setup)
    {
        var parts = new List<string> { "charts", "drawings", "objects", "zero-values" };
        if (setup.PrintGridLines) parts.Insert(0, "grid");
        if (setup.PrintHeadings) parts.Insert(0, "headers");
        return string.Join(' ', parts);
    }

    private string Centering(PageSetup setup) => (setup.CenterHorizontally, setup.CenterVertically) switch
    {
        (true, true) => "both",
        (true, false) => "horizontal",
        (false, true) => "vertical",
        _ => "none",
    };

    private string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private string Measure(Twip value) => value.ToPoints().ToString("0.##", CultureInfo.InvariantCulture) + "pt";
}
