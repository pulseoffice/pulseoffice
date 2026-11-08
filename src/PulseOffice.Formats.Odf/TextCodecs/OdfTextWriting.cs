using PulseOffice.Foundation.Colors;
using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal interface IOdfStyleSink
{
    string NextStyle(string prefix);

    void AddStyle(XElement style);

    void UseListStyles();

    void AddMedia(string path, byte[] data, string mediaType);
}

internal sealed class ParagraphStyleBuilder(OdfUnits units, OdfTextFrameWriter frames) : OdfXml
{
    private readonly OdfTabStops _tabs = new();

    public XElement Build(string name, TextParagraph paragraph, ITextTheme theme, bool indentedByList)
    {
        var text = new XElement(Style + "text-properties",
            new XAttribute(Fo + "font-size", units.Pt(paragraph.FontSize)),
            new XAttribute(Fo + "color", units.Hex(paragraph.Color ?? theme.Resolve(paragraph.ColorRole ?? ColorRole.Text))),
            new XAttribute(Style + "font-name", paragraph.MajorFont ? theme.MajorFont : theme.MinorFont));
        text.Add(new XAttribute(Fo + "font-weight", paragraph.Bold ? "bold" : "normal"));
        text.Add(new XAttribute(Fo + "font-style", paragraph.Italic ? "italic" : "normal"));

        var layout = new XElement(Style + "paragraph-properties",
            new XAttribute(Fo + "text-align", units.Align(paragraph.Align)),
            new XAttribute(Fo + "line-height", $"{(int)Math.Round(paragraph.LineSpacing * 100)}%"),
            new XAttribute(Fo + "margin-top", units.Pt(paragraph.SpaceBefore)),
            new XAttribute(Fo + "margin-bottom", units.Pt(paragraph.SpaceAfter)));
        var mirrored = paragraph.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        var physical = mirrored ? paragraph with { MarginLeft = paragraph.MarginRight, MarginRight = paragraph.MarginLeft } : paragraph;
        if (!indentedByList) layout.Add(frames.Indents(physical));
        if (physical.MarginRight is { } right) layout.Add(new XAttribute(Fo + "margin-right", units.Pt(right)));
        if (paragraph.Direction is { } direction) layout.Add(new XAttribute(Style + "writing-mode", mirrored ? "rl-tb" : "lr-tb"));
        if (paragraph.TabStops is { Count: > 0 } stops) layout.Add(_tabs.Write(stops));

        return new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "paragraph"),
            layout,
            text);
    }
}

internal sealed class SpanStyleBuilder(OdfUnits units, OdfLanguageCodec languages) : OdfXml
{
    public SpanStyleBuilder(OdfUnits units) : this(units, new OdfLanguageCodec())
    {
    }

    public XElement Build(string name, TextRun run, ITextTheme theme)
    {
        var text = new XElement(Style + "text-properties",
            new XAttribute(Fo + "font-size", units.Pt(run.FontSize ?? 18)),
            new XAttribute(Fo + "color", units.Hex(run.Color ?? theme.Resolve(run.ColorRole ?? ColorRole.Text))),
            new XAttribute(Style + "font-name", run.Font ?? (run.MajorFont ? theme.MajorFont : theme.MinorFont)));

        foreach (var attribute in Effects(run)) text.Add(attribute);
        if (run.AsianFont is { Length: > 0 } asian) text.Add(new XAttribute(Style + "font-name-asian", asian));
        text.Add(languages.Write(run.Language, OdfScript.Western));
        text.Add(languages.Write(run.AsianLanguage, OdfScript.Asian));

        return new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "text"),
            text);
    }

    private IEnumerable<XAttribute> Effects(TextRun run)
    {
        if (run.Bold) yield return new XAttribute(Fo + "font-weight", "bold");
        if (run.Italic) yield return new XAttribute(Fo + "font-style", "italic");
        if (run.Underline || run.DoubleUnderline)
        {
            yield return new XAttribute(Style + "text-underline-style", "solid");
            yield return new XAttribute(Style + "text-underline-width", "auto");
            yield return new XAttribute(Style + "text-underline-color", "font-color");
            yield return new XAttribute(Style + "text-underline-type", run.DoubleUnderline ? "double" : "single");
        }
        if (run.Strikethrough) yield return new XAttribute(Style + "text-line-through-style", "solid");
        if (run.Overline) yield return new XAttribute(Style + "text-overline-style", "solid");
        if (run.Shadow) yield return new XAttribute(Fo + "text-shadow", "1pt 1pt");
        if (run.Outline) yield return new XAttribute(Style + "text-outline", "true");
        if (run.SmallCaps) yield return new XAttribute(Fo + "font-variant", "small-caps");
        if (run.Highlight is { } highlight) yield return new XAttribute(Fo + "background-color", units.Hex(highlight));
        if (run.Baseline == TextBaseline.Superscript) yield return new XAttribute(Style + "text-position", "super 58%");
        else if (run.Baseline == TextBaseline.Subscript) yield return new XAttribute(Style + "text-position", "sub 58%");
    }
}

internal sealed class ListStyleBuilder(OdfListPictures pictures, IOdfListNumbers numbers) : OdfXml
{
    public ListStyleBuilder() : this(new OdfListPictures(), new OdfListNumbers())
    {
    }

    public const string Bullets = "LQBullet";

    public const string Numbers = "LQNumber";

    public XElement Entry(XElement paragraph, TextParagraph model)
    {
        XElement entry = new(Text + "list-item", paragraph);
        for (var level = Math.Clamp(model.BulletLevel, 0, 8); level > 0; level--)
            entry = new XElement(Text + "list-item", new XElement(Text + "list", entry));
        return entry;
    }

    public IEnumerable<XElement> Styles()
    {
        yield return new XElement(Text + "list-style",
            new XAttribute(Style + "name", Bullets),
            Enumerable.Range(1, 9).Select(level => new XElement(Text + "list-level-style-bullet",
                new XAttribute(Text + "level", level),
                new XAttribute(Text + "bullet-char", "•"),
                new XAttribute(Text + "bullet-relative-size", "100%"),
                Level(level),
                FullSize())));
        yield return new XElement(Text + "list-style",
            new XAttribute(Style + "name", Numbers),
            Enumerable.Range(1, 9).Select(level => new XElement(Text + "list-level-style-number",
                new XAttribute(Text + "level", level),
                new XAttribute(Style + "num-format", "1"),
                new XAttribute(Style + "num-suffix", "."),
                Level(level),
                FullSize())));
    }

    public XElement Automatic(string name, bool numbered, BulletSymbol? symbol, Action<string, byte[], string> addMedia, ListNumberFormat? format = null) =>
        new(Text + "list-style",
            new XAttribute(Style + "name", name),
            Enumerable.Range(1, 9).Select(level => numbered ? NumberLevel(level, format)
                : symbol?.Picture is { Data.Length: > 0 } picture ? PictureLevel(level, picture, addMedia)
                : BulletLevel(level, symbol ?? new BulletSymbol("•"))));

    public ListLevelGeometry? Geometry(TextParagraph paragraph)
    {
        if (paragraph.MarginLeft is not { } margin) return null;
        var hanging = Math.Max(0, -(paragraph.Indent ?? 0));
        return new ListLevelGeometry(margin - hanging, hanging);
    }

    public void Place(XElement style, int level, ListLevelGeometry geometry)
    {
        var properties = style.Elements().ElementAtOrDefault(level)?.Element(Style + "list-level-properties");
        if (properties is null) return;
        properties.SetAttributeValue(Text + "space-before", Centimetres(Math.Max(0, geometry.LabelStart)));
        properties.SetAttributeValue(Text + "min-label-width", Centimetres(geometry.LabelWidth));
    }

    private XElement NumberLevel(int level, ListNumberFormat? format)
    {
        var element = new XElement(Text + "list-level-style-number", new XAttribute(Text + "level", level), Level(level), FullSize());
        numbers.Write(element, format);
        return element;
    }

    private XElement PictureLevel(int level, ListBulletPicture picture, Action<string, byte[], string> addMedia)
    {
        var element = pictures.Element(picture, level, addMedia);
        var properties = Level(level);
        pictures.Size(properties, picture);
        element.Add(properties);
        return element;
    }

    private XElement BulletLevel(int level, BulletSymbol symbol) =>
        new(Text + "list-level-style-bullet",
            new XAttribute(Text + "level", level),
            new XAttribute(Text + "bullet-char", symbol.Character),
            Level(level),
            new XElement(Style + "text-properties",
                symbol.Font is { } font ? new XAttribute(Fo + "font-family", font) : null,
                symbol.SymbolCharset ? new XAttribute(Style + "font-charset", "x-symbol") : null,
                symbol.Color is { } color ? new XAttribute(Fo + "color", new ArgbColor(color).ToHashedHex()) : new XAttribute(Style + "use-window-font-color", "true"),
                new XAttribute(Fo + "font-size", Math.Round((symbol.RelativeSize ?? 1) * 100).ToString(CultureInfo.InvariantCulture) + "%")));

    private string Centimetres(double points) => (points * 2.54 / 72).ToString("0.###", CultureInfo.InvariantCulture) + "cm";

    private XElement FullSize() => new(Style + "text-properties", new XAttribute(Fo + "font-size", "100%"));

    private XElement Level(int level) =>
        new(Style + "list-level-properties",
            new XAttribute(Text + "space-before", (0.6 * (level - 1)).ToString("0.##", CultureInfo.InvariantCulture) + "cm"),
            new XAttribute(Text + "min-label-width", "0.8cm"));
}

internal readonly record struct ListLevelGeometry(double LabelStart, double LabelWidth);

internal sealed class AutomaticListStyle(string name, XElement element, bool numbered, BulletSymbol? symbol, ListNumberFormat? format)
{
    private readonly Dictionary<int, ListLevelGeometry> _levels = [];

    public string Name => name;

    public XElement Element => element;

    public bool Accepts(bool isNumbered, BulletSymbol? bullet, ListNumberFormat? numbering, int level, ListLevelGeometry? geometry) =>
        numbered == isNumbered && Equals(symbol, bullet) && Equals(format, numbering)
        && (geometry is not { } wanted || !_levels.TryGetValue(level, out var placed) || placed == wanted);

    public bool Claim(int level, ListLevelGeometry? geometry)
    {
        if (geometry is not { } wanted || _levels.ContainsKey(level)) return false;
        _levels[level] = wanted;
        return true;
    }
}

internal interface IOdfRichTextWriter
{
    void Write(XElement container, IEnumerable<TextParagraph> paragraphs, ITextTheme theme, IOdfStyleSink sink);
}

internal sealed class OdfRichTextWriter(
    ParagraphStyleBuilder paragraphs,
    SpanStyleBuilder spans,
    ListStyleBuilder lists,
    IOdfFieldElements fields,
    IOdfParagraphText text,
    IOdfDateStyleCodec dates,
    IBulletPictureSizing pictures,
    IListNumberFormats numberFormats) : OdfXml, IOdfRichTextWriter
{
    public void Write(XElement container, IEnumerable<TextParagraph> paragraphs, ITextTheme theme, IOdfStyleSink sink)
    {
        XElement? list = null;
        string? current = null;
        var custom = new List<AutomaticListStyle>();

        foreach (var paragraph in paragraphs)
        {
            var p = Paragraph(paragraph, theme, sink);
            if (!paragraph.IsListItem)
            {
                list = null;
                container.Add(p);
                continue;
            }

            var styleName = ListStyle(paragraph, custom, current, sink);
            if (list is null || current != styleName)
            {
                current = styleName;
                list = new XElement(Text + "list", new XAttribute(Text + "style-name", styleName));
                container.Add(list);
            }

            list.Add(lists.Entry(p, paragraph));
        }
    }

    private string ListStyle(TextParagraph paragraph, List<AutomaticListStyle> custom, string? current, IOdfStyleSink sink)
    {
        var symbol = paragraph.Numbered ? null : Fixed(paragraph.BulletSymbol, paragraph);
        var format = paragraph.Numbered ? numberFormats.Normalized(paragraph.NumberFormat) : null;
        var geometry = lists.Geometry(paragraph);
        if (symbol is null && format is null && geometry is null)
        {
            sink.UseListStyles();
            return paragraph.Numbered ? ListStyleBuilder.Numbers : ListStyleBuilder.Bullets;
        }

        var level = Math.Clamp(paragraph.BulletLevel, 0, 8);
        var chosen = custom.FirstOrDefault(style => style.Name == current && style.Accepts(paragraph.Numbered, symbol, format, level, geometry))
            ?? custom.FirstOrDefault(style => style.Accepts(paragraph.Numbered, symbol, format, level, geometry));
        if (chosen is null)
        {
            var name = sink.NextStyle("L");
            chosen = new AutomaticListStyle(name, lists.Automatic(name, paragraph.Numbered, symbol, sink.AddMedia, format), paragraph.Numbered, symbol, format);
            sink.AddStyle(chosen.Element);
            custom.Add(chosen);
        }
        if (chosen.Claim(level, geometry)) lists.Place(chosen.Element, level, geometry!.Value);
        return chosen.Name;
    }

    private BulletSymbol? Fixed(BulletSymbol? symbol, TextParagraph paragraph) =>
        symbol?.Picture is { ScalesWithFont: true } picture
            ? symbol with { Picture = pictures.Size(picture, paragraph.FontSize * (symbol.RelativeSize ?? 1)) with { ScalesWithFont = false }, RelativeSize = null }
            : symbol;

    private XElement Paragraph(TextParagraph paragraph, ITextTheme theme, IOdfStyleSink sink)
    {
        var name = sink.NextStyle("P");
        sink.AddStyle(paragraphs.Build(name, paragraph, theme, paragraph.IsListItem && lists.Geometry(paragraph) is not null));
        var p = new XElement(Text + "p", new XAttribute(Text + "style-name", name));

        if (paragraph.Runs.Count == 0)
        {
            p.Add(text.Paragraph(paragraph.Text).Nodes());
            return p;
        }

        foreach (var run in paragraph.Runs)
        {
            var spanName = sink.NextStyle("T");
            sink.AddStyle(spans.Build(spanName, run, theme));
            var span = new XElement(Text + "span",
                new XAttribute(Text + "style-name", spanName),
                run.Field == TextFieldKind.None ? text.Paragraph(run.Text).Nodes() : Field(run, sink));
            p.Add(run.Hyperlink is { Length: > 0 } link
                ? new XElement(Text + "a", new XAttribute(Xlink + "type", "simple"), new XAttribute(Xlink + "href", link), span)
                : span);
        }
        return p;
    }

    private XElement Field(TextRun run, IOdfStyleSink sink)
    {
        var element = fields.Build(run.Field, run.Text);
        if (run.Field is not (TextFieldKind.Date or TextFieldKind.Time) || run.FieldDetail?.Format is not { Length: > 0 } pattern) return element;
        var name = sink.NextStyle("N");
        sink.AddStyle(dates.Build(name, pattern));
        element.SetAttributeValue(Style + "data-style-name", name);
        return element;
    }
}
