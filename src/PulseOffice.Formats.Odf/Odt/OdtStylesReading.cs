using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using OdfListPictures = PulseOffice.Formats.Odf.TextCodecs.OdfListPictures;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtFontFaceIndex : OdtXml
{
    public Dictionary<string, string> Read(params XElement?[] declarations)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var face in declarations.OfType<XElement>().SelectMany(d => d.Elements(Style + "font-face")))
        {
            var name = (string?)face.Attribute(Style + "name");
            if (name is null) continue;
            var family = ((string?)face.Attribute(Svg + "font-family") ?? name).Trim().Trim('\'', '"');
            map.TryAdd(name, family);
        }
        return map;
    }
}

internal sealed class OdtStylesReader(
    OdtCharacterCodec characters,
    OdtParagraphCodec paragraphs,
    OdtFrameCodec frames,
    OdtColumnsCodec columns,
    OdtMeasure measure,
    OdtStyleNames names,
    OdtNumberFormats numbers,
    OdtVocabulary vocabulary,
    OdtBorders borders,
    OdtWatermarks watermarks,
    OdtChangeReader changes,
    OdfListPictures pictures) : OdtXml
{
    public void RegisterNames(XElement? styles, XElement? masters, XElement? contentAutomatic, XElement? stylesAutomatic, OdtReadContext context)
    {
        foreach (var style in (styles?.Elements() ?? []).Concat(contentAutomatic?.Elements(Text + "list-style") ?? []).Concat(stylesAutomatic?.Elements(Text + "list-style") ?? []))
        {
            if ((string?)style.Attribute(Style + "name") is not { } encoded) continue;
            context.StyleNames.Add(encoded, (string?)style.Attribute(Style + "display-name") ?? names.Decode(encoded));
        }
        foreach (var master in masters?.Elements(Style + "master-page") ?? [])
        {
            if ((string?)master.Attribute(Style + "name") is not { } encoded) continue;
            context.PageNames.Add(encoded, (string?)master.Attribute(Style + "display-name") ?? names.Decode(encoded));
        }
    }

    public void Read(XElement? styles, XElement? contentAutomatic, XElement? stylesAutomatic, OdtReadContext context)
    {
        var sheet = context.Document.Styles;
        var relative = new Dictionary<string, double>(StringComparer.Ordinal);

        if (styles?.Elements(Style + "default-style").FirstOrDefault(d => (string?)d.Attribute(Style + "family") == "paragraph") is { } defaults)
        {
            var text = defaults.Element(Style + "text-properties");
            sheet.DefaultParagraph = paragraphs.Read(defaults.Element(Style + "paragraph-properties"), text).Over(sheet.DefaultParagraph);
            sheet.DefaultCharacter = characters.Read(text, context.FontFaces).Properties.Over(sheet.DefaultCharacter);
            if (measure.Points((string?)defaults.Element(Style + "paragraph-properties")?.Attribute(Style + "tab-stop-distance")) is { } distance && double.IsFinite(distance) && distance >= 0)
                sheet.DefaultTabInterval = distance;
        }

        foreach (var style in styles?.Elements(Style + "style") ?? [])
        {
            var family = (string?)style.Attribute(Style + "family");
            var name = Name(style, context);
            if (name is null) continue;

            switch (family)
            {
                case "paragraph" when !sheet.Paragraph.Contains(name):
                    sheet.Paragraph.Add(ParagraphStyle(style, name, context, relative));
                    break;
                case "text" when !sheet.Character.Contains(name):
                    var reading = characters.Read(style.Element(Style + "text-properties"), context.FontFaces);
                    var character = Common(new CharacterStyle(name) { Character = reading.Properties }, style, context);
                    if (reading.RelativeSize is { } factor) relative["c:" + name] = factor;
                    sheet.Character.Add(character);
                    break;
                case "graphic" when !sheet.Frame.Contains(name):
                    sheet.Frame.Add(Common(new FrameStyle(name) { Frame = frames.Read(style.Element(Style + "graphic-properties")) }, style, context));
                    break;
                case "table" when !sheet.Table.Contains(name):
                    sheet.Table.Add(TableStyle(style, name, context));
                    break;
            }
        }

        foreach (var list in (styles?.Elements(Text + "list-style") ?? []).Concat(contentAutomatic?.Elements(Text + "list-style") ?? []).Concat(stylesAutomatic?.Elements(Text + "list-style") ?? []))
            if (Name(list, context) is { } name && !sheet.List.Contains(name))
                sheet.List.Add(ListStyle(list, name, context));

        if (styles?.Element(Text + "outline-style") is { } outline)
        {
            var levels = sheet.Outline.Levels.ToArray();
            foreach (var level in outline.Elements(Text + "outline-level-style"))
                if (measure.Integer((string?)level.Attribute(Text + "level")) is { } number && number >= 1 && number <= levels.Length)
                    levels[number - 1] = Level(level, context) with { Format = numbers.Value((string?)level.Attribute(Style + "num-format") ?? string.Empty) };
            sheet.Outline = new ListStyle(sheet.Outline.Name, levels);
        }

        if (styles?.Element(Text + "linenumbering-configuration") is { } numbering && (string?)numbering.Attribute(Text + "number-lines") != "false")
            context.Document.LineNumbering = new LineNumbering(Math.Max(1, measure.Integer((string?)numbering.Attribute(Text + "increment")) ?? 5))
            {
                Side = (string?)numbering.Attribute(Text + "number-position") is "right" or "outer" ? LineNumberSide.Right : LineNumberSide.Left,
                Distance = measure.Points((string?)numbering.Attribute(Text + "offset")) ?? 0.5 * 72 / 2.54,
                RestartEachPage = (string?)numbering.Attribute(Text + "restart-on-page") == "true",
                CountBlankLines = (string?)numbering.Attribute(Text + "count-empty-lines") != "false",
            };

        foreach (var configuration in styles?.Elements(Text + "notes-configuration") ?? [])
        {
            var settings = Notes(configuration, context);
            if (vocabulary.NoteClass.Value((string?)configuration.Attribute(Text + "note-class")) == NoteKind.Endnote)
                context.Document.Notes.EndnoteSettings = settings;
            else context.Document.Notes.FootnoteSettings = settings;
        }

        Resolve(sheet, relative);
    }

    public void ReadPages(XElement? automatic, XElement? masters, OdtReadContext context, OdtBlockReader blocks)
    {
        var layouts = automatic?.Elements(Style + "page-layout")
            .Where(layout => layout.Attribute(Style + "name") is not null)
            .GroupBy(layout => (string)layout.Attribute(Style + "name")!)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal) ?? [];

        foreach (var master in masters?.Elements(Style + "master-page") ?? [])
        {
            if ((string?)master.Attribute(Style + "name") is not { } encoded) continue;
            var name = context.PageNames.Resolve(encoded);
            if (context.Document.Styles.Page.Contains(name)) continue;

            var page = new PageStyle(name)
            {
                NextStyleName = (string?)master.Attribute(Style + "next-style-name") is { Length: > 0 } next ? context.PageNames.Resolve(next) : null,
                DisplayName = (string?)master.Attribute(Pt + "display-name"),
                ParentName = (string?)master.Attribute(Pt + "parent-name"),
                Hidden = (string?)master.Attribute(Pt + "hidden") == "true",
            };
            if ((string?)master.Attribute(Style + "page-layout-name") is { } layoutName && layouts.TryGetValue(layoutName, out var layout))
                Layout(page, layout);

            Region(master, "header", page.Header, page, context, blocks);
            Region(master, "footer", page.Footer, page, context, blocks);
            if ((string?)master.Attribute(Pt + "first-page-different") is { } firstFlag) page.FirstPageDifferent = firstFlag == "true";
            if ((string?)master.Attribute(Pt + "even-pages-different") is { } evenFlag) page.EvenPagesDifferent = evenFlag == "true";
            context.Document.Styles.Page.Add(page);
        }

        if (context.Document.Styles.Page.All.Count > 0) return;
        context.Document.Styles.Page.Add(new PageStyle(PulseOffice.Documents.Styles.DefaultStyleNames.Standard));
        context.Document.InitialPageStyle = PulseOffice.Documents.Styles.DefaultStyleNames.Standard;
    }

    private void Region(XElement master, string kind, HeaderFooterContent region, PageStyle page, OdtReadContext context, OdtBlockReader blocks)
    {
        var main = master.Element(Style + kind);
        var left = master.Element(Style + (kind + "-left"));
        var first = master.Element(Style + (kind + "-first")) ?? master.Element(Loext + (kind + "-first"));
        foreach (var tracked in new[] { main, left, first }.OfType<XElement>().Select(part => part.Element(Text + "tracked-changes")).OfType<XElement>())
            changes.Regions(tracked, context, blocks);
        if (kind == "header")
        {
            page.Watermark = watermarks.Extract(main, context.Automatic) ?? page.Watermark;
            foreach (var variant in new[] { left, first }) watermarks.Extract(variant, context.Automatic);
            if ((string?)main?.Attribute(Pt + "watermark-only") == "true") return;
            if (page.Watermark is not null && main is not null && !main.Elements().Any(child => child.Name != Text + "sequence-decls")) return;
        }

        if (main is not null)
        {
            region.Enabled = (string?)main.Attribute(Style + "display") != "false";
            region.Default.Replace(Story(main, context, blocks));
        }
        if (left is not null)
        {
            if (Displayed(left)) page.EvenPagesDifferent = true;
            var even = new BlockContainer(Story(left, context, blocks));
            region.Even = (string?)left.Attribute(Pt + "same-as-default") == "true" ? null : even;
        }
        if (first is not null)
        {
            if (Displayed(first)) page.FirstPageDifferent = true;
            var firstContent = new BlockContainer(Story(first, context, blocks));
            region.First = (string?)first.Attribute(Pt + "same-as-default") == "true" ? null : firstContent;
        }
    }

    private List<Block> Story(XElement part, OdtReadContext context, OdtBlockReader blocks)
    {
        using var story = context.Lists.Story();
        return blocks.Read(part, context);
    }

    private bool Displayed(XElement variant) => (string?)variant.Attribute(Style + "display") != "false";

    private void Layout(PageStyle page, XElement layout)
    {
        var properties = layout.Element(Style + "page-layout-properties");
        if (properties is not null)
        {
            var width = measure.Twips((string?)properties.Attribute(Fo + "page-width")) ?? page.Width;
            var height = measure.Twips((string?)properties.Attribute(Fo + "page-height")) ?? page.Height;
            var landscape = (string?)properties.Attribute(Style + "print-orientation") is { } orientation
                ? orientation == "landscape"
                : width.Value > height.Value;
            page.Orientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
            page.Size = landscape ? new DocumentSize(height, width) : new DocumentSize(width, height);
            page.Margins = new Margins(
                Margin(properties, "margin-left"), Margin(properties, "margin-top"),
                Margin(properties, "margin-right"), Margin(properties, "margin-bottom"));
            if (properties.Attribute(Style + "num-format") is { } format) page.NumberFormat = numbers.Value(format.Value);
            page.Background = measure.Fill((string?)properties.Attribute(Fo + "background-color"));
            page.Borders = borders.Read(properties) is { HasLines: true } box ? box : null;
            page.Columns = columns.Read(properties);
        }

        page.Usage = vocabulary.Usage.Value((string?)layout.Attribute(Style + "page-usage"));
        if (layout.Element(Style + "header-style")?.Element(Style + "header-footer-properties") is { } header)
        {
            page.Header.Spacing = measure.Twips((string?)header.Attribute(Fo + "margin-bottom")) ?? page.Header.Spacing;
            page.Header.MinimumHeight = measure.Twips((string?)header.Attribute(Fo + "min-height")) ?? page.Header.MinimumHeight;
        }
        if (layout.Element(Style + "footer-style")?.Element(Style + "header-footer-properties") is { } footer)
        {
            page.Footer.Spacing = measure.Twips((string?)footer.Attribute(Fo + "margin-top")) ?? page.Footer.Spacing;
            page.Footer.MinimumHeight = measure.Twips((string?)footer.Attribute(Fo + "min-height")) ?? page.Footer.MinimumHeight;
        }
    }

    private Twip Margin(XElement properties, string name) =>
        measure.Twips((string?)properties.Attribute(Fo + name)) ?? measure.Twips((string?)properties.Attribute(Fo + "margin")) ?? Twip.Zero;

    private string? Name(XElement style, OdtReadContext context) =>
        (string?)style.Attribute(Style + "name") is { } encoded ? context.StyleNames.Resolve(encoded) : null;

    private T Common<T>(T style, XElement element, OdtReadContext context) where T : DocumentStyle
    {
        style.ParentName = (string?)element.Attribute(Style + "parent-style-name") is { } parent ? context.StyleNames.Resolve(parent)
            : (string?)element.Attribute(Pt + "parent-name");
        style.DisplayName = (string?)element.Attribute(Pt + "display-name");
        style.Hidden = (string?)element.Attribute(Pt + "hidden") == "true" || (string?)element.Attribute(Style + "hidden") == "true";
        return style;
    }

    private ParagraphStyle ParagraphStyle(XElement element, string name, OdtReadContext context, Dictionary<string, double> relative)
    {
        var text = element.Element(Style + "text-properties");
        var reading = characters.Read(text, context.FontFaces);
        if (reading.RelativeSize is { } factor) relative["p:" + name] = factor;
        var paragraph = paragraphs.Read(element.Element(Style + "paragraph-properties"), text);
        if ((string?)element.Attribute(Style + "master-page-name") is { Length: > 0 } master)
            paragraph = paragraph with { PageStyleName = context.PageNames.Resolve(master) };

        return Common(new ParagraphStyle(name)
        {
            Paragraph = paragraph,
            Character = reading.Properties,
            NextStyleName = (string?)element.Attribute(Style + "next-style-name") is { } next ? context.StyleNames.Resolve(next) : null,
            OutlineLevel = measure.Integer((string?)element.Attribute(Style + "default-outline-level")),
            ListStyleName = (string?)element.Attribute(Style + "list-style-name") is { Length: > 0 } list ? context.StyleNames.Resolve(list) : null,
            ListLevel = measure.Integer((string?)element.Attribute(Style + "list-level")),
            Category = vocabulary.Categories.Value((string?)element.Attribute(Style + "class")),
        }, element, context);
    }

    private TableStyle TableStyle(XElement element, string name, OdtReadContext context) =>
        Common(new TableStyle(name)
        {
            Table = new TableProperties
            {
                Background = measure.Color((string?)element.Element(Style + "table-properties")?.Attribute(Fo + "background-color")),
                BorderColor = measure.Color((string?)element.Attribute(Pt + "border-color")),
                BorderWidth = measure.Points((string?)element.Attribute(Pt + "border-width")),
                HeaderBackground = measure.Color((string?)element.Attribute(Pt + "header-background")),
                HeaderBold = (string?)element.Attribute(Pt + "header-bold") is { } bold ? bold == "true" : null,
                BandBackground = measure.Color((string?)element.Attribute(Pt + "band-background")),
                CellPadding = measure.Points((string?)element.Attribute(Pt + "cell-padding")),
                VerticalCellPadding = measure.Points((string?)element.Attribute(Pt + "cell-padding-vertical")),
                HeaderColor = measure.Color((string?)element.Attribute(Pt + "header-color")),
                InnerLines = (string?)element.Attribute(Pt + "inner-lines") is { } inner ? inner == "true" : null,
            },
        }, element, context);

    private ListStyle ListStyle(XElement element, string name, OdtReadContext context)
    {
        var levels = element.Elements()
            .Where(level => level.Name.LocalName.StartsWith("list-level-style-", StringComparison.Ordinal))
            .Select(level => (Number: measure.Integer((string?)level.Attribute(Text + "level")) ?? 1, Level: Level(level, context)))
            .Where(entry => entry.Number is >= 1 and <= PulseOffice.Documents.Styles.ListStyle.LevelCount)
            .GroupBy(entry => entry.Number)
            .OrderBy(group => group.Key)
            .Select(group => group.First().Level)
            .ToList();
        if (levels.Count == 0) levels.Add(new ListLevel(NumberFormat.Arabic));

        var style = new ListStyle(name, levels)
        {
            DisplayName = (string?)element.Attribute(Pt + "display-name"),
            ParentName = (string?)element.Attribute(Pt + "parent-name"),
            Hidden = (string?)element.Attribute(Pt + "hidden") == "true",
        };
        return style;
    }

    private ListLevel Level(XElement element, OdtReadContext context)
    {
        var bullet = element.Name.LocalName == "list-level-style-bullet";
        var image = element.Name.LocalName == "list-level-style-image";
        var picture = image ? pictures.Read(element, context.ReadBytes) : null;
        if (image && picture is null) context.Losses.Add(OdtReadLosses.PictureBullets);
        var format = bullet ? NumberFormat.Bullet
            : element.Name.LocalName == "list-level-style-image" ? NumberFormat.Bullet
            : numbers.Value((string?)element.Attribute(Style + "num-format") ?? string.Empty);
        var (indent, hanging) = Geometry(element.Element(Style + "list-level-properties"));
        var startText = bullet || image ? (string?)element.Attribute(Pt + "start-value") : (string?)element.Attribute(Text + "start-value");
        var displayText = bullet || image ? (string?)element.Attribute(Pt + "display-levels") : (string?)element.Attribute(Text + "display-levels");
        var characterStyle = (string?)element.Attribute(Text + "style-name") ?? (string?)element.Attribute(Pt + "style-name");

        return new ListLevel(format)
        {
            Prefix = (string?)element.Attribute(Style + "num-prefix") ?? string.Empty,
            Suffix = (string?)element.Attribute(Style + "num-suffix") ?? string.Empty,
            Bullet = (string?)element.Attribute(Pt + "bullet-empty") == "true" ? string.Empty : (string?)element.Attribute(Text + "bullet-char") ?? (string?)element.Attribute(Pt + "bullet-char") ?? "•",
            StartAt = Math.Clamp(measure.Integer(startText) ?? 1, 0, ushort.MaxValue),
            DisplayLevels = Math.Clamp(measure.Integer(displayText) ?? 1, 1, PulseOffice.Documents.Styles.ListStyle.LevelCount),
            Legal = !bullet && !image && (string?)element.Attribute(Pt + "legal") == "true",
            Indent = indent,
            Hanging = hanging,
            CharacterStyleName = characterStyle is { Length: > 0 } character ? context.StyleNames.Resolve(character) : null,
            Picture = picture,
        };
    }

    private (double Indent, double Hanging) Geometry(XElement? properties)
    {
        if (properties?.Element(Style + "list-level-label-alignment") is { } alignment)
        {
            var hanging = -(measure.Points((string?)alignment.Attribute(Fo + "text-indent")) ?? -18);
            return ((measure.Points((string?)alignment.Attribute(Fo + "margin-left")) ?? 0) - hanging, hanging);
        }

        var before = measure.Points((string?)properties?.Attribute(Text + "space-before")) ?? 0;
        var label = measure.Points((string?)properties?.Attribute(Text + "min-label-width")) ?? 18;
        return (before, label);
    }

    private NoteSettings Notes(XElement element, OdtReadContext context) => new()
    {
        FormatName = (string?)element.Attribute(Style + "num-format") ?? "1",
        StartAt = Math.Clamp(measure.Integer((string?)element.Attribute(Text + "start-value")) ?? 0, 0, ushort.MaxValue) + 1,
        Restart = vocabulary.Restart.Value((string?)element.Attribute(Text + "start-numbering-at")),
        CitationStyleName = (string?)element.Attribute(Text + "citation-style-name") is { Length: > 0 } citation ? context.StyleNames.Resolve(citation) : null,
        ParagraphStyleName = (string?)element.Attribute(Text + "default-style-name") is { Length: > 0 } paragraph ? context.StyleNames.Resolve(paragraph) : null,
    };

    private void Resolve(StyleSheet sheet, Dictionary<string, double> relative)
    {
        if (relative.Count == 0) return;
        var resolver = new StyleResolver();
        foreach (var style in sheet.Paragraph.All.OrderBy(style => Depth(sheet, style)))
        {
            if (!relative.TryGetValue("p:" + style.Name, out var factor)) continue;
            var parentSize = resolver.ResolveParagraphStyle(sheet, style.ParentName ?? string.Empty).Character.FontSize ?? sheet.DefaultCharacter.FontSize ?? 12;
            if (style.ParentName is null) parentSize = sheet.DefaultCharacter.FontSize ?? 12;
            style.Character = style.Character with { FontSize = Math.Round(parentSize * factor, 2) };
        }
        foreach (var style in sheet.Character.All)
            if (relative.TryGetValue("c:" + style.Name, out var factor))
                style.Character = style.Character with { FontSize = Math.Round((sheet.DefaultCharacter.FontSize ?? 12) * factor, 2) };
    }

    private int Depth(StyleSheet sheet, ParagraphStyle style)
    {
        var depth = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = style; current?.ParentName is { } parent && seen.Add(parent) && depth < 64; current = sheet.Paragraph.Find(parent)) depth++;
        return depth;
    }
}
