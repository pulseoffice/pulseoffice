using DocumentFormat.OpenXml;
using PulseOffice.Documents.Styles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxStyleMap
{
    private const string WordNormal = "Normal";

    private readonly Dictionary<(StyleFamily, string), string> _ids = [];
    private readonly Dictionary<string, (StyleFamily Family, string Name)> _names = new(StringComparer.Ordinal);

    public string Bind(StyleFamily family, string name)
    {
        if (_ids.TryGetValue((family, name), out var known)) return known;
        var standard = family == StyleFamily.Paragraph && name == DefaultStyleNames.Standard;
        var id = standard ? WordNormal : Sanitize(name);
        var candidate = id;
        for (var suffix = 2; _names.ContainsKey(candidate) || (!standard && candidate == WordNormal); suffix++) candidate = id + suffix;
        _ids[(family, name)] = candidate;
        _names[candidate] = (family, name);
        return candidate;
    }

    public void Adopt(string id, StyleFamily family, string name)
    {
        _ids.TryAdd((family, name), id);
        _names[id] = (family, name);
    }

    public string? IdOf(StyleFamily family, string? name) =>
        name is not null && _ids.TryGetValue((family, name), out var id) ? id : null;

    public string? NameOf(string? id) => id is not null && _names.TryGetValue(id, out var entry) ? entry.Name : null;

    public string ModelName(string? wordName, string id, string defaultParagraphId)
    {
        if (id == defaultParagraphId) return DefaultStyleNames.Standard;
        if (wordName is null) return id;
        if (wordName.StartsWith("heading ", StringComparison.Ordinal)) return "Heading " + wordName[8..];
        if (LevelOf(wordName, WordContentsPrefix, StringComparison.OrdinalIgnoreCase) is { } level) return DefaultStyleNames.ContentsLevel(level);
        return string.Equals(wordName, WordContentsHeading, StringComparison.OrdinalIgnoreCase) ? DefaultStyleNames.ContentsHeading : wordName;
    }

    public string DefaultParagraphId(IEnumerable<W.Style> styles) =>
        styles.FirstOrDefault(style => (style.Type?.Value ?? W.StyleValues.Paragraph) == W.StyleValues.Paragraph && style.Default?.Value == true)?.StyleId?.Value ?? WordNormal;

    public bool IsDefaultParagraph(string id) => id == WordNormal;

    public string WordName(string id, string name)
    {
        if (id == WordNormal) return WordNormal;
        if (name.StartsWith("Heading ", StringComparison.Ordinal) && name.Length > 8 && char.IsAsciiDigit(name[8])) return "heading " + name[8..];
        if (LevelOf(name, ModelContentsPrefix, StringComparison.Ordinal) is { } level and <= 9) return WordContentsPrefix + level.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return name == DefaultStyleNames.ContentsHeading ? WordContentsHeading : name;
    }

    private const string WordContentsPrefix = "toc ";
    private const string ModelContentsPrefix = "Contents ";
    private const string WordContentsHeading = "TOC Heading";

    private int? LevelOf(string name, string prefix, StringComparison comparison) =>
        name.StartsWith(prefix, comparison) && int.TryParse(name.AsSpan(prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var level) && level is >= 1 and <= 10
            ? level
            : null;

    private string Sanitize(string name)
    {
        var letters = new string([.. name.Where(char.IsLetterOrDigit)]);
        return letters.Length == 0 ? "Style" : letters;
    }
}

public sealed class DocxStylesCodec(DocxCharacterCodec characters, DocxParagraphCodec paragraphs, DocxTableStyleCodec tables, DocxTabPositions tabs)
{
    public void Bind(StyleSheet sheet, DocxStyleMap map)
    {
        foreach (var style in sheet.Paragraph.All) map.Bind(StyleFamily.Paragraph, style.Name);
        foreach (var style in sheet.Character.All) map.Bind(StyleFamily.Character, style.Name);
        foreach (var style in sheet.Table.All) map.Bind(StyleFamily.Table, style.Name);
    }

    public W.Styles Write(StyleSheet sheet, DocxStyleMap map, DocxListMap lists)
    {
        var styles = new W.Styles(new W.DocDefaults(
            new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(characters.Elements(sheet.DefaultCharacter))),
            new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(
                paragraphs.Elements(tabs.Defaults(sheet.DefaultParagraph, sheet), new DocxParagraphExtras(null, null, 0, null))))));
        foreach (var style in sheet.Paragraph.All)
        {
            var element = Header(style, W.StyleValues.Paragraph, StyleFamily.Paragraph, map, map.IdOf(StyleFamily.Paragraph, style.NextStyleName),
                map.IdOf(StyleFamily.Character, style.LinkedStyleName));
            var outlined = style.ListStyleName is null && style.OutlineLevel is { } outline && sheet.Outline.Level(outline - 1).Format != NumberFormat.None;
            element.Append(new W.StyleParagraphProperties(paragraphs.Elements(tabs.Style(style, sheet),
                outlined
                    ? new DocxParagraphExtras(null, lists.IdOf(StyleSheet.OutlineStyleName), style.OutlineLevel!.Value - 1, style.OutlineLevel)
                    : new DocxParagraphExtras(null, lists.IdOf(style.ListStyleName), Math.Max(0, (style.ListLevel ?? 1) - 1), style.OutlineLevel))));
            element.Append(new W.StyleRunProperties(characters.Elements(style.Character)));
            styles.Append(element);
        }

        foreach (var style in sheet.Character.All)
        {
            var element = Header(style, W.StyleValues.Character, StyleFamily.Character, map, null, map.IdOf(StyleFamily.Paragraph, style.LinkedStyleName));
            element.Append(new W.StyleRunProperties(characters.Elements(style.Character)));
            styles.Append(element);
        }

        foreach (var style in sheet.Table.All)
        {
            var element = Header(style, W.StyleValues.Table, StyleFamily.Table, map, null, null);
            tables.Write(element, style.Table);
            styles.Append(element);
        }

        return styles;
    }

    private bool Outline(StyleSheet sheet, DocxParagraphExtras extras, DocxListMap lists)
    {
        if (extras.OutlineLevel is not { } outline || extras.ListLevel != outline - 1 || lists.NameOf(extras.ListId) is not { } name) return false;
        if (sheet.List.Find(name) is not { } list) return false;
        if (sheet.Outline.Levels.All(level => level.Format == NumberFormat.None)) sheet.Outline = new ListStyle(StyleSheet.OutlineStyleName, list.Levels);
        return true;
    }

    public void Adopt(W.Styles? styles, StyleSheet sheet, DocxStyleMap map)
    {
        if (styles is null) return;
        var elements = styles.Elements<W.Style>().Where(style => style.StyleId?.Value is not null && !DefaultFont(style)).ToList();
        var defaultParagraph = map.DefaultParagraphId(elements);
        foreach (var style in elements)
            map.Adopt(style.StyleId!.Value!, Family(style),
                Family(style) == StyleFamily.Paragraph ? map.ModelName(style.StyleName?.Val?.Value, style.StyleId!.Value!, defaultParagraph) : map.ModelName(style.StyleName?.Val?.Value, style.StyleId!.Value!, string.Empty));

        if (styles.DocDefaults is { } defaults)
        {
            sheet.DefaultCharacter = characters.Read(defaults.RunPropertiesDefault?.RunPropertiesBaseStyle).Over(sheet.DefaultCharacter);
            sheet.DefaultParagraph = paragraphs.Read(defaults.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle).Over(sheet.DefaultParagraph);
        }
    }

    public void ReadStyles(W.Styles? styles, StyleSheet sheet, DocxStyleMap map, DocxListMap lists)
    {
        if (styles is null) return;
        foreach (var element in styles.Elements<W.Style>().Where(style => style.StyleId?.Value is not null && !DefaultFont(style)))
        {
            var name = map.NameOf(element.StyleId!.Value!)!;
            var parent = map.NameOf(element.BasedOn?.Val?.Value);
            switch (Family(element))
            {
                case StyleFamily.Paragraph:
                    var extras = paragraphs.Extras(element.StyleParagraphProperties);
                    var paragraph = new ParagraphStyle(name)
                    {
                        ParentName = parent,
                        Paragraph = paragraphs.Read(element.StyleParagraphProperties),
                        Character = characters.Read(element.StyleRunProperties),
                        NextStyleName = map.NameOf(element.NextParagraphStyle?.Val?.Value),
                        OutlineLevel = extras.OutlineLevel,
                        ListStyleName = Outline(sheet, extras, lists) ? null : lists.NameOf(extras.ListId),
                        ListLevel = extras.ListId is not null && extras.ListLevel > 0 && !Outline(sheet, extras, lists) ? extras.ListLevel + 1 : null,
                        DisplayName = element.Aliases?.Val?.Value,
                        Hidden = Hidden(element),
                        LinkedStyleName = Linked(element, map, StyleFamily.Character),
                    };
                    sheet.Paragraph.Replace(paragraph);
                    break;
                case StyleFamily.Character:
                    sheet.Character.Replace(new CharacterStyle(name)
                    {
                        ParentName = parent,
                        Character = characters.Read(element.StyleRunProperties),
                        DisplayName = element.Aliases?.Val?.Value,
                        Hidden = Hidden(element),
                        LinkedStyleName = Linked(element, map, StyleFamily.Paragraph),
                    });
                    break;
                case StyleFamily.Table:
                    sheet.Table.Replace(new TableStyle(name) { ParentName = parent, DisplayName = element.Aliases?.Val?.Value, Table = tables.Read(element) });
                    break;
            }
        }
        sheet.BreakInheritanceCycles();
        sheet.DropDanglingLinks([.. sheet.Paragraph.All, .. sheet.Character.All]);
    }

    private W.Style Header(DocumentStyle style, W.StyleValues type, StyleFamily family, DocxStyleMap map, string? next, string? linked)
    {
        var id = map.Bind(family, style.Name);
        var element = new W.Style { Type = type, StyleId = id, CustomStyle = !map.IsDefaultParagraph(id) };
        if (family == StyleFamily.Paragraph && map.IsDefaultParagraph(id)) element.Default = true;
        element.Append(new W.StyleName { Val = map.WordName(id, style.Name) });
        if (style.DisplayName is not null) element.Append(new W.Aliases { Val = style.DisplayName });
        if (style.ParentName is not null && map.IdOf(family, style.ParentName) is { } parent) element.Append(new W.BasedOn { Val = parent });
        if (next is not null) element.Append(new W.NextParagraphStyle { Val = next });
        if (linked is not null) element.Append(new W.LinkedStyle { Val = linked });
        if (style.Hidden) element.Append(new W.SemiHidden());
        return element;
    }

    private string? Linked(W.Style style, DocxStyleMap map, StyleFamily family) =>
        map.NameOf(style.LinkedStyle?.Val?.Value) is { } name && map.IdOf(family, name) == style.LinkedStyle!.Val!.Value ? name : null;

    private bool DefaultFont(W.Style style) =>
        style.Type?.Value == W.StyleValues.Character && style.Default?.Value == true
        && (style.StyleRunProperties is null || !style.StyleRunProperties.HasChildren);

    private bool Hidden(W.Style style) => style.SemiHidden is { } hidden && hidden.Val?.Value != W.OnOffOnlyValues.Off;

    private StyleFamily Family(W.Style style) =>
        style.Type?.Value == W.StyleValues.Character ? StyleFamily.Character
        : style.Type?.Value == W.StyleValues.Table ? StyleFamily.Table
        : style.Type?.Value == W.StyleValues.Numbering ? StyleFamily.List
        : StyleFamily.Paragraph;
}
