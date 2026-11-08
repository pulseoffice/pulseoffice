using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Pages;
using PulseOffice.Text.Rich;
using OdfListPictures = PulseOffice.Formats.Odf.TextCodecs.OdfListPictures;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtStylesWriter(
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
    OdtChangeWriter changes,
    OdfListPictures pictures) : OdtXml
{
    public XElement Styles(TextDocument document, OdtWriteSession session)
    {
        var sheet = document.Styles;
        var styles = new XElement(Office + "styles");

        var defaultParagraph = paragraphs.Paragraph(sheet.DefaultParagraph) ?? new XElement(Style + "paragraph-properties");
        defaultParagraph.SetAttributeValue(Style + "tab-stop-distance", measure.Points(sheet.DefaultTabInterval));
        styles.Add(new XElement(Style + "default-style",
            new XAttribute(Style + "family", "paragraph"),
            defaultParagraph,
            TextProperties(sheet.DefaultCharacter, sheet.DefaultParagraph, session)));

        foreach (var style in sheet.Paragraph.All) styles.Add(ParagraphStyle(style, session));
        foreach (var style in sheet.Character.All)
            styles.Add(Named(style, "text", null, TextProperties(style.Character, ParagraphProperties.None, session)));
        foreach (var style in sheet.Frame.All)
            styles.Add(Named(style, "graphic", null, frames.Graphic(style.Frame)));
        foreach (var style in sheet.Table.All) styles.Add(TableStyle(style));
        foreach (var style in sheet.List.All) styles.Add(ListStyle(style, session));
        styles.Add(Outline(sheet.Outline));

        styles.Add(Notes(document.Notes.FootnoteSettings, NoteKind.Footnote));
        styles.Add(Notes(document.Notes.EndnoteSettings, NoteKind.Endnote));
        if (document.LineNumbering is { } lines)
            styles.Add(new XElement(Text + "linenumbering-configuration",
                new XAttribute(Text + "number-lines", "true"),
                new XAttribute(Text + "offset", measure.Points(lines.Distance)),
                new XAttribute(Style + "num-format", "1"),
                new XAttribute(Text + "number-position", lines.Side == LineNumberSide.Right ? "right" : "left"),
                new XAttribute(Text + "increment", lines.Increment),
                new XAttribute(Text + "restart-on-page", lines.RestartEachPage ? "true" : "false"),
                new XAttribute(Text + "count-empty-lines", lines.CountBlankLines ? "true" : "false")));
        return styles;
    }

    public (XElement Automatic, XElement Masters) Pages(TextDocument document, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var automatic = new List<XElement>();
        var masters = new XElement(Office + "master-styles");
        var index = 0;

        foreach (var page in document.Styles.Page.All)
        {
            var layoutName = "pm" + (++index).ToString(CultureInfo.InvariantCulture);
            automatic.Add(Layout(layoutName, page));

            var master = new XElement(Style + "master-page",
                new XAttribute(Style + "name", names.Encode(page.Name)),
                names.Encode(page.Name) != page.Name ? new XAttribute(Style + "display-name", page.Name) : null,
                new XAttribute(Style + "page-layout-name", layoutName),
                page.NextStyleName is { } next ? new XAttribute(Style + "next-style-name", names.Encode(next)) : null,
                page.DisplayName is { } display ? new XAttribute(Pt + "display-name", display) : null,
                page.ParentName is { } parent ? new XAttribute(Pt + "parent-name", parent) : null,
                page.Hidden ? new XAttribute(Pt + "hidden", "true") : null,
                new XAttribute(Pt + "first-page-different", page.FirstPageDifferent ? "true" : "false"),
                new XAttribute(Pt + "even-pages-different", page.EvenPagesDifferent ? "true" : "false"));

            AddRegion(master, "header", page.Header, page, session, blocks);
            AddRegion(master, "footer", page.Footer, page, session, blocks);
            foreach (var region in master.Elements()) changes.Embed(region, document, session, blocks);
            masters.Add(master);
        }

        return (new XElement(Office + "automatic-styles", automatic, session.HeaderStyles.Styles), masters);
    }

    private void AddRegion(XElement master, string kind, HeaderFooterContent region, PageStyle page, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var watermark = kind == "header" ? page.Watermark : null;
        if (watermark is not null && !region.Enabled)
        {
            master.Add(new XElement(Style + kind, new XAttribute(Pt + "watermark-only", "true"), watermarks.Paragraph(watermark, page, session.HeaderStyles)));
            return;
        }
        var hasContent = region.Default.Count > 0;
        if (!region.Enabled && !hasContent && region.First is null && region.Even is null) return;

        var parts = new List<XElement> { Region(Style + kind, region.Default, region.Enabled, session, blocks) };
        if (page.EvenPagesDifferent || region.Even is not null) parts.Add(Variant(Style + (kind + "-left"), region.Even, region, page.EvenPagesDifferent, session, blocks));
        if (page.FirstPageDifferent || region.First is not null) parts.Add(Variant(Style + (kind + "-first"), region.First, region, page.FirstPageDifferent, session, blocks));
        if (watermark is not null)
            foreach (var part in parts) part.AddFirst(watermarks.Paragraph(watermark, page, session.HeaderStyles));
        master.Add(parts);
    }

    private XElement Variant(XName name, BlockContainer? content, HeaderFooterContent region, bool shown, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var element = Region(name, content ?? region.Default, region.Enabled && shown, session, blocks);
        if (content is null) element.Add(new XAttribute(Pt + "same-as-default", "true"));
        return element;
    }

    private XElement Region(XName name, BlockContainer? content, bool enabled, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var element = new XElement(name);
        if (!enabled) element.Add(new XAttribute(Style + "display", "false"));
        if (content is not null)
        {
            using var story = session.Lists.Story();
            blocks.Write(element, content.Blocks, session.HeaderStyles, session);
        }
        if (!element.HasElements) element.Add(new XElement(Text + "p"));
        return element;
    }

    private XElement Layout(string name, PageStyle page)
    {
        var properties = new XElement(Style + "page-layout-properties",
            new XAttribute(Fo + "page-width", measure.Length(page.Width)),
            new XAttribute(Fo + "page-height", measure.Length(page.Height)),
            new XAttribute(Style + "num-format", page.NumberFormat == NumberFormat.Bullet ? string.Empty : numbers.Token(page.NumberFormat)),
            new XAttribute(Style + "print-orientation", page.Orientation == PageOrientation.Landscape ? "landscape" : "portrait"),
            new XAttribute(Fo + "margin-top", measure.Length(page.Margins.Top)),
            new XAttribute(Fo + "margin-bottom", measure.Length(page.Margins.Bottom)),
            new XAttribute(Fo + "margin-left", measure.Length(page.Margins.Left)),
            new XAttribute(Fo + "margin-right", measure.Length(page.Margins.Right)),
            new XAttribute(Style + "writing-mode", "lr-tb"));
        if (page.Background is { } background) properties.Add(new XAttribute(Fo + "background-color", measure.Fill(background)));
        if (page.Borders is { } box) borders.Write(properties, box);
        if (page.Columns.Count > 1 || page.Columns.Separator) properties.Add(columns.Write(page.Columns));

        return new XElement(Style + "page-layout",
            new XAttribute(Style + "name", name),
            page.Usage != PageUsage.All ? new XAttribute(Style + "page-usage", vocabulary.Usage.Token(page.Usage)) : null,
            properties,
            new XElement(Style + "header-style", page.Watermark is not null && !page.Header.Enabled
                ? new XElement(Style + "header-footer-properties", new XAttribute(Fo + "min-height", "0pt"), new XAttribute(Fo + "margin-bottom", "0pt"))
                : Spacing(page.Header, "margin-bottom")),
            new XElement(Style + "footer-style", Spacing(page.Footer, "margin-top")));
    }

    private XElement Spacing(HeaderFooterContent region, string side) =>
        new(Style + "header-footer-properties",
            new XAttribute(Fo + "min-height", measure.Length(region.MinimumHeight)),
            new XAttribute(Fo + side, measure.Length(region.Spacing)));

    private XElement? TextProperties(CharacterProperties character, ParagraphProperties paragraph, OdtWriteSession session)
    {
        if (character.Font is { } font) session.Fonts.Add(font);
        var attributes = characters.Write(character).Concat(paragraphs.Hyphenation(paragraph)).ToList();
        return attributes.Count > 0 ? new XElement(Style + "text-properties", attributes) : null;
    }

    private XElement ParagraphStyle(ParagraphStyle style, OdtWriteSession session)
    {
        var element = Named(style, "paragraph", style.NextStyleName,
            paragraphs.Paragraph(style.Paragraph),
            TextProperties(style.Character, style.Paragraph, session));
        if (style.OutlineLevel is { } outline) element.Add(new XAttribute(Style + "default-outline-level", outline));
        if (style.ListStyleName is { } list) element.Add(new XAttribute(Style + "list-style-name", names.Encode(list)));
        if (style.ListLevel is { } listLevel) element.Add(new XAttribute(Style + "list-level", listLevel));
        if (style.Paragraph.PageStyleName is { } master) element.Add(new XAttribute(Style + "master-page-name", names.Encode(master)));
        element.Add(new XAttribute(Style + "class", vocabulary.Categories.Token(style.Category)));
        return element;
    }

    private XElement TableStyle(TableStyle style)
    {
        var table = style.Table;
        var properties = new XElement(Style + "table-properties");
        if (table.Background is { } background) properties.Add(new XAttribute(Fo + "background-color", measure.Hex(background)));
        var element = Named(style, "table", null, properties);
        if (table.BorderColor is { } borderColor) element.Add(new XAttribute(Pt + "border-color", measure.Hex(borderColor)));
        if (table.BorderWidth is { } borderWidth) element.Add(new XAttribute(Pt + "border-width", measure.Points(borderWidth)));
        if (table.HeaderBackground is { } header) element.Add(new XAttribute(Pt + "header-background", measure.Hex(header)));
        if (table.HeaderBold is { } bold) element.Add(new XAttribute(Pt + "header-bold", bold ? "true" : "false"));
        if (table.BandBackground is { } band) element.Add(new XAttribute(Pt + "band-background", measure.Hex(band)));
        if (table.CellPadding is { } padding) element.Add(new XAttribute(Pt + "cell-padding", measure.Points(padding)));
        if (table.VerticalCellPadding is { } vertical) element.Add(new XAttribute(Pt + "cell-padding-vertical", measure.Points(vertical)));
        if (table.HeaderColor is { } ink) element.Add(new XAttribute(Pt + "header-color", measure.Hex(ink)));
        if (table.InnerLines is { } inner) element.Add(new XAttribute(Pt + "inner-lines", inner ? "true" : "false"));
        return element;
    }

    private XElement Named(DocumentStyle style, string family, string? next, params object?[] content)
    {
        var encoded = names.Encode(style.Name);
        return new XElement(Style + "style",
            new XAttribute(Style + "name", encoded),
            encoded != style.Name ? new XAttribute(Style + "display-name", style.Name) : null,
            new XAttribute(Style + "family", family),
            style.ParentName is { } parent ? new XAttribute(Style + "parent-style-name", names.Encode(parent)) : null,
            next is null ? null : new XAttribute(Style + "next-style-name", names.Encode(next)),
            style.DisplayName is { } display ? new XAttribute(Pt + "display-name", display) : null,
            style.Hidden ? new XAttribute(Pt + "hidden", "true") : null,
            content);
    }

    private XElement ListStyle(ListStyle style, OdtWriteSession session)
    {
        var encoded = names.Encode(style.Name);
        var element = new XElement(Text + "list-style",
            new XAttribute(Style + "name", encoded),
            encoded != style.Name ? new XAttribute(Style + "display-name", style.Name) : null,
            style.DisplayName is { } display ? new XAttribute(Pt + "display-name", display) : null,
            style.ParentName is { } parent ? new XAttribute(Pt + "parent-name", parent) : null,
            style.Hidden ? new XAttribute(Pt + "hidden", "true") : null);

        for (var index = 0; index < style.Levels.Count; index++)
            element.Add(style.Levels[index].Picture is { } picture ? Pictured(style.Levels[index], picture, index + 1, session) : Level(style.Levels[index], index + 1));
        return element;
    }

    private XElement Outline(ListStyle outline)
    {
        var element = new XElement(Text + "outline-style", new XAttribute(Style + "name", names.Encode(outline.Name)));
        for (var index = 0; index < outline.Levels.Count; index++)
            element.Add(Level(outline.Levels[index] with { Format = outline.Levels[index].Format == NumberFormat.Bullet ? NumberFormat.None : outline.Levels[index].Format }, index + 1, "outline-level-style"));
        return element;
    }

    private XElement Pictured(ListLevel level, ListBulletPicture picture, int number, OdtWriteSession session)
    {
        var element = pictures.Element(picture, number, session.AddMedia);
        element.Add(new XAttribute(Pt + "bullet-char", level.Bullet), new XAttribute(Pt + "start-value", level.StartAt), new XAttribute(Pt + "display-levels", level.DisplayLevels));
        if (level.CharacterStyleName is { } character) element.Add(new XAttribute(Pt + "style-name", names.Encode(character)));
        var properties = Properties(level);
        pictures.Size(properties, picture);
        element.Add(properties);
        return element;
    }

    private XElement Properties(ListLevel level) =>
        new(Style + "list-level-properties",
            new XAttribute(Text + "list-level-position-and-space-mode", "label-alignment"),
            new XElement(Style + "list-level-label-alignment",
                new XAttribute(Text + "label-followed-by", "listtab"),
                new XAttribute(Text + "list-tab-stop-position", measure.Points(level.Indent + level.Hanging)),
                new XAttribute(Fo + "text-indent", measure.Points(-level.Hanging)),
                new XAttribute(Fo + "margin-left", measure.Points(level.Indent + level.Hanging))));

    private XElement Level(ListLevel level, int number, string? name = null)
    {
        var bullet = level.Format == NumberFormat.Bullet;
        var element = new XElement(Text + (name ?? (bullet ? "list-level-style-bullet" : "list-level-style-number")),
            new XAttribute(Text + "level", number),
            level.CharacterStyleName is { } character ? new XAttribute(Text + "style-name", names.Encode(character)) : null,
            new XAttribute(Style + "num-prefix", level.Prefix),
            new XAttribute(Style + "num-suffix", level.Suffix));
        if (bullet) element.Add(new XAttribute(Text + "bullet-char", level.Bullet.Length > 0 ? level.Bullet : "•"));
        else
        {
            element.Add(new XAttribute(Style + "num-format", numbers.Token(level.Format)));
            element.Add(new XAttribute(Text + "start-value", level.StartAt));
            if (level.DisplayLevels > 1) element.Add(new XAttribute(Text + "display-levels", level.DisplayLevels));
            if (level.Legal) element.Add(new XAttribute(Pt + "legal", "true"));
        }
        if (bullet && level.Bullet.Length == 0) element.Add(new XAttribute(Pt + "bullet-empty", "true"));
        if (bullet) element.Add(new XAttribute(Pt + "start-value", level.StartAt), new XAttribute(Pt + "display-levels", level.DisplayLevels));

        element.Add(Properties(level));
        return element;
    }

    private XElement Notes(NoteSettings settings, NoteKind kind) =>
        new(Text + "notes-configuration",
            new XAttribute(Text + "note-class", vocabulary.NoteClass.Token(kind)),
            new XAttribute(Style + "num-format", settings.FormatName),
            new XAttribute(Text + "start-value", Math.Max(0, settings.StartAt - 1)),
            new XAttribute(Text + "start-numbering-at", vocabulary.Restart.Token(settings.Restart)),
            settings.CitationStyleName is { } citation ? new XAttribute(Text + "citation-style-name", names.Encode(citation)) : null,
            settings.ParagraphStyleName is { } paragraph ? new XAttribute(Text + "default-style-name", names.Encode(paragraph)) : null,
            kind == NoteKind.Footnote ? new XAttribute(Text + "footnotes-position", "page") : null);
}
