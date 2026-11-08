using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Formats.Packaging;

namespace PulseOffice.Formats.Odf;

public interface IOdfTextStyleSink
{
    string Register(XElement properties);

    string RegisterParagraph(XElement properties);

    string RegisterList(XElement listStyle);
}

public sealed class OdfTextStyleSink(Func<string> next, Action<XElement> add) : IOdfTextStyleSink
{
    private readonly Dictionary<string, string> _known = new(StringComparer.Ordinal);

    public string Register(XElement properties) => Registered(properties, "text");

    public string RegisterParagraph(XElement properties) => Registered(properties, "paragraph");

    public string RegisterList(XElement listStyle)
    {
        ArgumentNullException.ThrowIfNull(listStyle);
        var unnamed = new XElement(listStyle);
        unnamed.Attribute(OdfPackage.Namespaces.Style + "name")?.Remove();
        var key = "list" + unnamed.ToString(SaveOptions.DisableFormatting);
        if (_known.TryGetValue(key, out var existing)) return existing;
        var name = next();
        unnamed.SetAttributeValue(OdfPackage.Namespaces.Style + "name", name);
        add(unnamed);
        _known[key] = name;
        return name;
    }

    private string Registered(XElement properties, string family)
    {
        var key = family + properties.ToString(SaveOptions.DisableFormatting);
        if (_known.TryGetValue(key, out var existing)) return existing;
        var name = next();
        add(new XElement(OdfPackage.Namespaces.Style + "style",
            new XAttribute(OdfPackage.Namespaces.Style + "name", name),
            new XAttribute(OdfPackage.Namespaces.Style + "family", family),
            properties));
        _known[key] = name;
        return name;
    }
}

internal sealed class OdfShapeTextRuns(IOdfParagraphText paragraphs, IShapeTextEditing editing, IShapeTextLists lists, OdfShapeTextLists listCodec)
{
    private const double MinimumSize = 1;
    private const double MaximumSize = 4000;

    private readonly XNamespace _text = OdfPackage.Namespaces.Text;
    private readonly XNamespace _xlink = OdfPackage.Namespaces.Xlink;
    private readonly XNamespace _office = OdfPackage.Namespaces.Office;
    private readonly XNamespace _style = OdfPackage.Namespaces.Style;
    private readonly XNamespace _fo = OdfPackage.Namespaces.Fo;

    public OdfShapeTextRuns(IOdfParagraphText paragraphs) : this(paragraphs, new ShapeTextEditing(), new ShapeTextLists(), new OdfShapeTextLists()) { }

    public bool Formatted(ShapeObject shape) => editing.Formatted(shape) || lists.Listed(shape);

    public OdfShapeListSource Lists(IEnumerable<XElement> candidates, Func<string, byte[]?>? readBytes = null) =>
        new(listCodec.Styles(candidates), readBytes);

    public IEnumerable<XElement> Build(ShapeObject shape, IOdfTextStyleSink styles)
    {
        var aligned = Alignment(shape.Style.Text.Horizontal) is { } alignment
            ? styles.RegisterParagraph(new XElement(_style + "paragraph-properties", new XAttribute(_fo + "text-align", alignment)))
            : null;
        XElement Paragraph() => aligned is null ? new XElement(_text + "p") : new XElement(_text + "p", new XAttribute(_text + "style-name", aligned));
        var current = Paragraph();
        var built = new List<XElement> { current };
        foreach (var run in shape.Runs ?? [new ShapeTextRun(shape.Text ?? string.Empty)])
        {
            var lines = run.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (index > 0)
                {
                    current = Paragraph();
                    built.Add(current);
                }
                if (lines[index].Length == 0) continue;
                object nodes = paragraphs.Paragraph(lines[index].Replace(ShapeTextRun.LineBreak, '\n')).Nodes().ToList();
                if (Properties(run) is { } properties)
                    nodes = new XElement(_text + "span", new XAttribute(_text + "style-name", styles.Register(properties)), nodes);
                current.Add(run.Link is { } link
                    ? new XElement(_text + "a", new XAttribute(_xlink + "type", "simple"), new XAttribute(_xlink + "href", link), nodes)
                    : nodes);
            }
        }
        return lists.Listed(shape) ? listCodec.Wrap(built, lists.Items(shape), shape.Style.Text.Size, styles) : built;
    }

    public ShapeObject Read(XElement node, ShapeObject shape, Func<string, XElement?> textStyle, OdfShapeListSource? source = null)
    {
        var runs = new List<ShapeTextRun>();
        var items = new List<ShapeListItem?>();
        var first = true;
        foreach (var paragraph in Paragraphs(node))
        {
            items.Add(source is null ? null : listCodec.Read(paragraph, node, source));
            if (!first) runs.Add(runs.Count > 0 ? runs[^1] with { Text = "\n" } : new ShapeTextRun("\n"));
            first = false;
            Collect(paragraph, new ShapeTextRun(string.Empty), runs, textStyle);
        }
        var read = editing.WithRuns(shape, runs);
        if (items.Any(item => item is not null)) return lists.WithItems(read.Text is null ? shape : read, items);
        return editing.Formatted(read) || read.Text?.Contains(ShapeTextRun.LineBreak, StringComparison.Ordinal) == true ? read : shape;
    }

    private string? Alignment(TextHorizontalPlacement placement) => placement switch
    {
        TextHorizontalPlacement.Centre => "center",
        TextHorizontalPlacement.Right => "end",
        TextHorizontalPlacement.Justify => "justify",
        _ => null,
    };

    private XElement? Properties(ShapeTextRun run)
    {
        var properties = new XElement(_style + "text-properties");
        if (run.Bold) properties.Add(new XAttribute(_fo + "font-weight", "bold"));
        if (run.Italic) properties.Add(new XAttribute(_fo + "font-style", "italic"));
        if (run.Underline)
            properties.Add(new XAttribute(_style + "text-underline-style", "solid"), new XAttribute(_style + "text-underline-width", "auto"),
                new XAttribute(_style + "text-underline-color", "font-color"));
        if (run.Size is { } size) properties.Add(new XAttribute(_fo + "font-size", size.ToString("0.##", CultureInfo.InvariantCulture) + "pt"));
        if (run.ColorHex is { } colour) properties.Add(new XAttribute(_fo + "color", colour.ToLowerInvariant()));
        if (run.Family is { } family) properties.Add(new XAttribute(_fo + "font-family", family));
        return properties.HasAttributes ? properties : null;
    }

    private void Collect(XElement container, ShapeTextRun format, List<ShapeTextRun> runs, Func<string, XElement?> textStyle)
    {
        foreach (var child in container.Nodes())
        {
            if (child is XElement anchor && anchor.Name == _text + "a")
                Collect(anchor, format with { Link = (string?)anchor.Attribute(_xlink + "href") }, runs, textStyle);
            else if (child is XElement span && span.Name == _text + "span")
                Collect(span, Styled(format, (string?)span.Attribute(_text + "style-name") is { } name ? textStyle(name) : null), runs, textStyle);
            else if (paragraphs.Line(new XElement(_text + "p", child)) is { Length: > 0 } text)
                runs.Add(format with { Text = text.Replace('\n', ShapeTextRun.LineBreak) });
        }
    }

    private ShapeTextRun Styled(ShapeTextRun format, XElement? style)
    {
        if (style?.Element(_style + "text-properties") is not { } properties) return format;
        var weight = (string?)properties.Attribute(_fo + "font-weight");
        var slant = (string?)properties.Attribute(_fo + "font-style");
        var underline = (string?)properties.Attribute(_style + "text-underline-style");
        var size = (string?)properties.Attribute(_fo + "font-size");
        return format with
        {
            Bold = weight is null ? format.Bold : weight == "bold" || int.TryParse(weight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && numeric >= 600,
            Italic = slant is null ? format.Italic : slant is "italic" or "oblique",
            Underline = underline is null ? format.Underline : underline != "none",
            Size = size is not null && size.EndsWith("pt", StringComparison.Ordinal)
                   && double.TryParse(size[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var points) && points is >= MinimumSize and <= MaximumSize ? points : format.Size,
            ColorHex = (string?)properties.Attribute(_fo + "color") is { Length: 7 } colour ? colour.ToUpperInvariant() : format.ColorHex,
            Family = ((string?)properties.Attribute(_fo + "font-family") ?? (string?)properties.Attribute(_style + "font-name")) is { Length: > 0 } family
                ? family.Trim('\'') : format.Family,
        };
    }

    private IEnumerable<XElement> Paragraphs(XElement container) =>
        container.Descendants()
            .Where(element => element.Name == _text + "p" || element.Name == _text + "h")
            .Where(element => !element.Ancestors().TakeWhile(ancestor => ancestor != container)
                .Any(ancestor => ancestor.Name.Namespace == _office || ancestor.Name == _text + "p" || ancestor.Name == _text + "h"));
}
