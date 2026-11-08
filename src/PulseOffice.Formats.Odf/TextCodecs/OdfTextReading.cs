using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal readonly record struct ListPosition(bool Item, bool Numbered, int Depth, double? Margin = null, double? Indent = null, BulletSymbol? Symbol = null, ListNumberFormat? NumberFormat = null);

internal interface IOdfListPlacement
{
    ListPosition Of(XElement paragraph, XElement container, IReadOnlyDictionary<string, XElement> styles, XElement? inherited = null, Func<string, byte[]?>? readBytes = null);
}

internal sealed class ListPlacement(OdfValues values, ISymbolFonts symbols, OdfListPictures pictures, IOdfListNumbers numbers) : OdfXml, IOdfListPlacement
{
    private const double LargestBulletPercent = 400;

    private readonly OdfFontFaceNames _faces = new();

    public ListPlacement(OdfValues values) : this(values, new SymbolFonts(), new OdfListPictures(), new OdfListNumbers())
    {
    }

    public ListPosition Of(XElement paragraph, XElement container, IReadOnlyDictionary<string, XElement> styles, XElement? inherited = null, Func<string, byte[]?>? readBytes = null)
    {
        var lists = paragraph.Ancestors().TakeWhile(a => a != container).Where(a => a.Name == Text + "list").ToList();
        if (lists.Count == 0) return default;

        var depth = lists.Count - 1;
        var level = Level(lists, depth, styles);
        var (margin, indent) = Geometry(level);
        var bytes = readBytes ?? (_ => null);
        var symbol = level is null && inherited is not null ? Symbol(LevelOf(inherited, depth), bytes) : Symbol(level, bytes);
        return new ListPosition(paragraph.Parent?.Name == Text + "list-item", level?.Name == Text + "list-level-style-number", depth, margin, indent, symbol, numbers.Read(level));
    }

    private BulletSymbol? Symbol(XElement? level, Func<string, byte[]?> readBytes)
    {
        if (level?.Name == Text + "list-level-style-image")
            return pictures.Read(level, readBytes) is { } picture ? new BulletSymbol("•") { Picture = picture } : null;
        if (level?.Name != Text + "list-level-style-bullet" || (string?)level.Attribute(Text + "bullet-char") is not { Length: > 0 } character) return null;
        var properties = level.Element(Style + "text-properties");
        var size = (string?)properties?.Attribute(Fo + "font-size");
        var font = _faces.Unquoted((string?)properties?.Attribute(Fo + "font-family")) ?? _faces.Family(level, (string?)properties?.Attribute(Style + "font-name"));
        return new BulletSymbol(character)
        {
            Font = font,
            Color = (string?)properties?.Attribute(Style + "use-window-font-color") == "true" ? null : values.Color((string?)properties?.Attribute(Fo + "color")),
            RelativeSize = size is not null && size.EndsWith('%') && double.TryParse(size[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0 && double.IsFinite(percent) ? Math.Min(percent, LargestBulletPercent) / 100 : null,
            SymbolCharset = _faces.Symbolic(level, properties) || symbols.IsSymbolic(font),
        };
    }

    private XElement? Level(IReadOnlyList<XElement> lists, int depth, IReadOnlyDictionary<string, XElement> styles)
    {
        var name = lists.Select(list => (string?)list.Attribute(Text + "style-name")).LastOrDefault(n => n is not null);
        return name is not null && styles.TryGetValue(name, out var style) ? LevelOf(style, depth) : null;
    }

    private XElement? LevelOf(XElement style, int depth)
    {
        return style.Elements().FirstOrDefault(e =>
                int.TryParse((string?)e.Attribute(Text + "level"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && number == depth + 1)
            ?? style.Elements().FirstOrDefault();
    }

    private (double? Margin, double? Indent) Geometry(XElement? level)
    {
        var properties = level?.Element(Style + "list-level-properties");
        if (properties is null) return (null, null);

        if (properties.Element(Style + "list-level-label-alignment") is { } alignment)
            return (Length((string?)alignment.Attribute(Fo + "margin-left")), Length((string?)alignment.Attribute(Fo + "text-indent")));

        var before = Length((string?)properties.Attribute(Text + "space-before"));
        var label = Length((string?)properties.Attribute(Text + "min-label-width"));
        return before is null && label is null ? (null, null) : ((before ?? 0) + (label ?? 0), label is { } width ? -width : null);
    }

    private double? Length(string? value) => string.IsNullOrEmpty(value) ? null : values.Length(value);
}

internal interface IOdfFieldKinds
{
    TextFieldKind Of(XElement span);

    FieldDetail? DetailOf(XElement span, IReadOnlyDictionary<string, string> dataStyles);
}

internal sealed class FieldKinds(IOdfFieldElements elements) : OdfXml, IOdfFieldKinds
{
    public TextFieldKind Of(XElement span) =>
        span.Elements().Select(child => elements.KindOf(child.Name)).FirstOrDefault(kind => kind != TextFieldKind.None);

    public FieldDetail? DetailOf(XElement span, IReadOnlyDictionary<string, string> dataStyles)
    {
        var field = span.Elements().FirstOrDefault(child => elements.KindOf(child.Name) is TextFieldKind.Date or TextFieldKind.Time);
        return (string?)field?.Attribute(Style + "data-style-name") is { } name && dataStyles.TryGetValue(name, out var pattern)
            ? new FieldDetail { Format = pattern }
            : null;
    }
}

internal interface IOdfRunReader
{
    IReadOnlyList<TextRun> Read(XElement paragraph, ParagraphFormat properties, OdfTextStyles styles);
}

internal sealed class RunReader(IOdfFieldKinds fields, IOdfParagraphText text) : OdfXml, IOdfRunReader
{
    public IReadOnlyList<TextRun> Read(XElement paragraph, ParagraphFormat properties, OdfTextStyles styles)
    {
        var inline = Inline(paragraph, null).ToList();
        if (!inline.Any(item => item.Link is not null || item.Node is XElement e && (e.Name == Text + "span" || IsField(e)))) return [];

        var runs = new List<TextRun>();
        var loose = new XElement(Text + "p");
        string? looseLink = null;

        foreach (var (node, link) in inline)
        {
            if (node is XElement span && span.Name == Text + "span")
            {
                Flush();
                runs.Add(Styled(span, properties, styles.Spans) with { FieldDetail = fields.DetailOf(span, styles.DataStyles), Hyperlink = link });
            }
            else if (node is XElement field && IsField(field))
            {
                Flush();
                runs.Add(Plain(text.Line(Wrapped(field)), properties) with { Field = fields.Of(Wrapped(field)), FieldDetail = fields.DetailOf(Wrapped(field), styles.DataStyles), Hyperlink = link });
            }
            else
            {
                if (link != looseLink) Flush();
                looseLink = link;
                loose.Add(node is XText plain ? new XText(plain.Value) : new XElement((XElement)node));
            }
        }

        Flush();
        return runs;

        void Flush()
        {
            var line = text.Line(loose);
            if (line.Length > 0) runs.Add(Plain(line, properties) with { Hyperlink = looseLink });
            loose.RemoveNodes();
            looseLink = null;
        }
    }

    private bool IsField(XElement element) => fields.Of(Wrapped(element)) != TextFieldKind.None;

    private XElement Wrapped(XElement element) => new(Text + "span", new XElement(element));

    private IEnumerable<(XNode Node, string? Link)> Inline(XElement container, string? link) =>
        container.Nodes().SelectMany(node => node switch
        {
            XElement anchor when anchor.Name == Text + "a" => Inline(anchor, (string?)anchor.Attribute(Xlink + "href") ?? link),
            XElement span when span.Name == Text + "span" && span.Elements(Text + "a").Any() => Split(span, link),
            _ => [(node, link)],
        });

    private IEnumerable<(XNode Node, string? Link)> Split(XElement span, string? link) =>
        span.Nodes().Select(child => child is XElement anchor && anchor.Name == Text + "a"
            ? ((XNode)new XElement(span.Name, span.Attributes(), anchor.Nodes()), (string?)anchor.Attribute(Xlink + "href") ?? link)
            : (new XElement(span.Name, span.Attributes(), child), link));

    private TextRun Plain(string value, ParagraphFormat properties) => new(value)
    {
        FontSize = properties.FontSize,
        Bold = properties.Bold,
        Italic = properties.Italic,
        Color = properties.Color,
    };

    private TextRun Styled(XElement span, ParagraphFormat properties, IReadOnlyDictionary<string, SpanProperties> spans)
    {
        var style = (string?)span.Attribute(Text + "style-name") is { } name && spans.TryGetValue(name, out var found) ? found : null;
        return new TextRun(text.Line(span))
        {
            Field = fields.Of(span),
            FontSize = style?.FontSize ?? properties.FontSize,
            Font = style?.Font,
            Bold = style?.Bold ?? properties.Bold,
            Italic = style?.Italic ?? properties.Italic,
            Underline = (style?.Underline ?? false) && !(style?.DoubleUnderline ?? false),
            DoubleUnderline = style?.DoubleUnderline ?? false,
            Overline = style?.Overline ?? false,
            Shadow = style?.Shadow ?? false,
            Outline = style?.Outline ?? false,
            SmallCaps = style?.SmallCaps ?? false,
            Strikethrough = style?.Strikethrough ?? false,
            Baseline = style?.Baseline ?? TextBaseline.Normal,
            Color = style?.Color ?? properties.Color,
            Highlight = style?.Highlight,
            AsianFont = style?.AsianFont,
            Language = style?.Language,
            AsianLanguage = style?.AsianLanguage,
        };
    }
}

internal interface IOdfParagraphReader
{
    IReadOnlyList<TextParagraph> Read(XElement container, OdfTextStyles styles, OdfParagraphLevels levels);
}

internal sealed class ParagraphReader(IOdfListPlacement lists, IOdfRunReader runs, IOdfParagraphText text) : OdfXml, IOdfParagraphReader
{
    public IReadOnlyList<TextParagraph> Read(XElement container, OdfTextStyles styles, OdfParagraphLevels levels)
    {
        var paragraphs = container.Descendants(Text + "p").Where(p => !p.Ancestors().TakeWhile(a => a != container).Any(a => a.Name.Namespace == Office)).Select(p => Read(p, container, styles, levels)).ToList();
        return paragraphs.Count > 0 ? paragraphs : [new TextParagraph(string.Empty)];
    }

    private TextParagraph Read(XElement paragraph, XElement container, OdfTextStyles styles, OdfParagraphLevels levels)
    {
        var list = lists.Of(paragraph, container, styles.Lists, levels.InheritedList);
        var own = (string?)paragraph.Attribute(Text + "style-name") is { } name
            && styles.Paragraphs.TryGetValue(name, out var found) ? found : ParagraphProperties.Empty;
        var properties = own.Over(levels.At(list.Depth)).Resolve();
        var shallow = text.Shallow(paragraph);
        var read = runs.Read(shallow, properties, styles);

        return new TextParagraph(read.Count > 0 ? string.Concat(read.Select(r => r.Text)) : text.Line(shallow))
        {
            Align = properties.Align,
            LineSpacing = properties.LineSpacing,
            SpaceBefore = properties.SpaceBefore,
            SpaceAfter = properties.SpaceAfter,
            MarginLeft = list.Item && (properties.MarginLeft ?? 0) == 0 && list.Margin is { } margin ? margin : properties.MarginLeft,
            Indent = list.Item && (properties.Indent ?? 0) == 0 && list.Indent is { } hanging ? hanging : properties.Indent,
            MarginRight = properties.MarginRight,
            TabStops = properties.TabStops,
            Direction = properties.Direction,
            FontSize = properties.FontSize,
            Bold = properties.Bold,
            Italic = properties.Italic,
            Color = properties.Color,
            Bullet = list.Item && !list.Numbered,
            BulletSymbol = list.Item && !list.Numbered ? list.Symbol : null,
            Numbered = list.Item && list.Numbered,
            NumberFormat = list.Item && list.Numbered ? list.NumberFormat : null,
            BulletLevel = list.Depth,
            Runs = read,
        };
    }
}
