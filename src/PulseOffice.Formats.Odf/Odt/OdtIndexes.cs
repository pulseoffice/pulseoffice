using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed record OdtIndexShape(IndexKind Kind, string Element, string Source, string Template, int Levels);

internal sealed class OdtIndexCodec(OdtStyleNames names, IIndexStyles styles) : OdtXml
{
    private readonly IReadOnlyList<OdtIndexShape> _shapes =
    [
        new(IndexKind.Contents, "table-of-content", "table-of-content-source", "table-of-content-entry-template", 10),
        new(IndexKind.Alphabetical, "alphabetical-index", "alphabetical-index-source", "alphabetical-index-entry-template", 3),
        new(IndexKind.Illustrations, "illustration-index", "illustration-index-source", "illustration-index-entry-template", 0),
        new(IndexKind.Tables, "table-index", "table-index-source", "table-index-entry-template", 0),
        new(IndexKind.Bibliography, "bibliography", "bibliography-source", "bibliography-entry-template", 0),
        new(IndexKind.User, "user-index", "user-index-source", "user-index-entry-template", 10),
        new(IndexKind.Objects, "object-index", "object-index-source", "object-index-entry-template", 0),
    ];

    private readonly IReadOnlyList<(IndexObjectSources Source, string Attribute)> _objectSources =
    [
        (IndexObjectSources.Spreadsheets, "use-spreadsheet-objects"), (IndexObjectSources.Formulas, "use-math-objects"),
        (IndexObjectSources.Drawings, "use-draw-objects"), (IndexObjectSources.Charts, "use-chart-objects"), (IndexObjectSources.Other, "use-other-objects"),
    ];

    private readonly IReadOnlyList<(CaptionDisplay Display, string Token)> _captions =
    [
        (CaptionDisplay.Caption, "caption"), (CaptionDisplay.CategoryAndNumber, "category-and-value"), (CaptionDisplay.Text, "text"),
    ];

    private readonly PulseOffice.Documents.Fields.BibliographyFieldNames _bibliography = new();

    public bool Handles(XName name) => name.Namespace == Text && _shapes.Any(shape => shape.Element == name.LocalName);

    public XElement Write(SectionBlock section, IndexDefinition index, string sectionStyle, OdtStyleSink sink, OdtWriteSession session, IOdtBlockWriter blocks)
    {
        var shape = _shapes.First(entry => entry.Kind == index.Kind);
        var element = new XElement(Text + shape.Element,
            new XAttribute(Text + "style-name", sectionStyle),
            new XAttribute(Text + "name", section.Name),
            new XAttribute(Xml + "id", section.Id),
            section.Protected ? new XAttribute(Text + "protected", "true") : null,
            Source(shape, index));
        var body = new XElement(Text + "index-body");
        var paragraphs = section.Content.Blocks.ToList();
        var heading = styles.Heading(index);
        if (index.Title.Length > 0 && paragraphs.FirstOrDefault() is ParagraphBlock { } title && title.StyleName == heading)
        {
            var titleElement = new XElement(Text + "index-title", new XAttribute(Text + "name", section.Name + "_Head"));
            blocks.Write(titleElement, [title], sink, session);
            body.Add(titleElement);
            paragraphs.RemoveAt(0);
        }
        blocks.Write(body, paragraphs, sink, session);
        element.Add(body);
        return element;
    }

    private XElement Source(OdtIndexShape shape, IndexDefinition index)
    {
        var source = new XElement(Text + shape.Source);
        switch (index.Kind)
        {
            case IndexKind.Contents:
                source.Add(new XAttribute(Text + "outline-level", index.Levels.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(Text + "use-outline-level", Flag(index.FromOutline)),
                    new XAttribute(Text + "use-index-marks", Flag(index.FromMarks)),
                    new XAttribute(Text + "use-index-source-styles", "false"));
                break;
            case IndexKind.Alphabetical:
                source.Add(new XAttribute(Text + "alphabetical-separators", Flag(index.LetterSeparators)),
                    new XAttribute(Text + "combine-entries", Flag(index.CombineEntries)),
                    new XAttribute(Text + "ignore-case", Flag(!index.CaseSensitive)));
                break;
            case IndexKind.Illustrations or IndexKind.Tables:
                source.Add(new XAttribute(Text + "use-caption", "true"),
                    new XAttribute(Text + "caption-sequence-name", index.Sequence ?? (index.Kind == IndexKind.Tables ? "Table" : "Figure")),
                    new XAttribute(Text + "caption-sequence-format", _captions.First(entry => entry.Display == index.Captions).Token));
                break;
            case IndexKind.Objects:
                source.Add(_objectSources.Select(entry => new XAttribute(Text + entry.Attribute, Flag(index.ObjectSources.HasFlag(entry.Source)))));
                break;
            case IndexKind.User:
                source.Add(new XAttribute(Text + "index-name", index.UserName ?? "User-Defined"),
                    new XAttribute(Text + "use-index-marks", "true"));
                break;
        }
        if (index.Kind is IndexKind.Bibliography or IndexKind.User && !index.SortByPosition) source.Add(new XAttribute(Pt + "sort-alphabetical", "true"));
        source.Add(new XElement(Text + "index-title-template", new XAttribute(Text + "style-name", names.Encode(styles.Heading(index))), index.Title));
        if (index.Kind == IndexKind.Bibliography)
        {
            foreach (var type in _bibliography.Types) source.Add(BibliographyTemplate(shape, type));
            return source;
        }
        if (index.Kind == IndexKind.Alphabetical)
            source.Add(new XElement(Text + shape.Template, new XAttribute(Text + "outline-level", "separator"),
                new XAttribute(Text + "style-name", names.Encode(styles.Entry(index, 0))), new XElement(Text + "index-entry-text")));
        var levels = Math.Max(1, shape.Levels);
        for (var level = 1; level <= levels; level++)
        {
            var template = new XElement(Text + shape.Template, new XAttribute(Text + "style-name", names.Encode(styles.Entry(index, level))));
            if (shape.Levels > 0) template.Add(new XAttribute(Text + "outline-level", level.ToString(CultureInfo.InvariantCulture)));
            if (index.Hyperlinks && index.Kind == IndexKind.Contents) template.Add(new XElement(Text + "index-entry-link-start"));
            template.Add(new XElement(Text + "index-entry-text"));
            if (index.PageNumbers)
            {
                template.Add(index.Kind == IndexKind.Alphabetical
                    ? new XElement(Text + "index-entry-span", ", ")
                    : new XElement(Text + "index-entry-tab-stop", new XAttribute(Style + "type", "right"), new XAttribute(Style + "leader-char", ".")));
                template.Add(new XElement(Text + "index-entry-page-number"));
            }
            if (index.Hyperlinks && index.Kind == IndexKind.Contents) template.Add(new XElement(Text + "index-entry-link-end"));
            source.Add(template);
        }
        return source;
    }

    private XElement BibliographyTemplate(OdtIndexShape shape, string type) =>
        new(Text + shape.Template,
            new XAttribute(Text + "bibliography-type", type),
            new XAttribute(Text + "style-name", names.Encode(DefaultStyleNames.Bibliography1)),
            new XElement(Text + "index-entry-span", "["),
            new XElement(Text + "index-entry-bibliography", new XAttribute(Text + "bibliography-data-field", "identifier")),
            new XElement(Text + "index-entry-span", "] "),
            new XElement(Text + "index-entry-bibliography", new XAttribute(Text + "bibliography-data-field", "author")),
            new XElement(Text + "index-entry-span", ", "),
            new XElement(Text + "index-entry-bibliography", new XAttribute(Text + "bibliography-data-field", "title")),
            new XElement(Text + "index-entry-span", ", "),
            new XElement(Text + "index-entry-bibliography", new XAttribute(Text + "bibliography-data-field", "publisher")),
            new XElement(Text + "index-entry-span", ", "),
            new XElement(Text + "index-entry-bibliography", new XAttribute(Text + "bibliography-data-field", "year")));

    private string Flag(bool value) => value ? "true" : "false";

    public IndexDefinition Read(XElement element, Func<string, string> resolve)
    {
        var shape = _shapes.First(entry => entry.Element == element.Name.LocalName);
        var source = element.Element(Text + shape.Source);
        var templates = source?.Elements(Text + shape.Template).ToList() ?? [];
        var title = source?.Element(Text + "index-title-template")?.Value ?? string.Empty;
        var hasLinks = templates.Any(template => template.Element(Text + "index-entry-link-start") is not null);
        var hasPages = templates.Count == 0 || templates.Any(template => template.Element(Text + "index-entry-page-number") is not null);
        var index = new IndexDefinition(shape.Kind)
        {
            Title = title,
            Hyperlinks = hasLinks,
            PageNumbers = shape.Kind == IndexKind.Bibliography ? false : hasPages,
            SortByPosition = (string?)source?.Attribute(Pt + "sort-alphabetical") != "true",
        };
        index = index with { LevelStyles = LevelStyles(shape, source, resolve) };
        return shape.Kind switch
        {
            IndexKind.Contents => index with
            {
                Levels = int.TryParse((string?)source?.Attribute(Text + "outline-level"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var levels) ? levels : 10,
                FromOutline = (string?)source?.Attribute(Text + "use-outline-level") != "false",
                FromMarks = (string?)source?.Attribute(Text + "use-index-marks") != "false",
            },
            IndexKind.Alphabetical => index with
            {
                LetterSeparators = (string?)source?.Attribute(Text + "alphabetical-separators") == "true",
                CombineEntries = (string?)source?.Attribute(Text + "combine-entries") != "false",
                CaseSensitive = (string?)source?.Attribute(Text + "ignore-case") == "false",
            },
            IndexKind.Illustrations or IndexKind.Tables => index with
            {
                Sequence = (string?)source?.Attribute(Text + "caption-sequence-name"),
                Captions = _captions.FirstOrDefault(entry => entry.Token == (string?)source?.Attribute(Text + "caption-sequence-format")).Display,
            },
            IndexKind.User => index with { UserName = (string?)source?.Attribute(Text + "index-name") },
            IndexKind.Objects => index with
            {
                ObjectSources = _objectSources.Where(entry => (string?)source?.Attribute(Text + entry.Attribute) == "true").Aggregate(IndexObjectSources.None, (all, entry) => all | entry.Source),
            },
            _ => index,
        };
    }

    private IndexLevelStyles LevelStyles(OdtIndexShape shape, XElement? source, Func<string, string> resolve)
    {
        var result = IndexLevelStyles.Defaults;
        if (source is null || shape.Kind == IndexKind.Bibliography) return result;
        if ((string?)source.Element(Text + "index-title-template")?.Attribute(Text + "style-name") is { Length: > 0 } heading
            && resolve(heading) is var title && title != styles.Heading(shape.Kind))
            result = result.WithHeading(title);
        foreach (var template in source.Elements(Text + shape.Template))
        {
            if ((string?)template.Attribute(Text + "style-name") is not { Length: > 0 } encoded) continue;
            var outline = (string?)template.Attribute(Text + "outline-level");
            int level;
            if (outline == "separator") level = 0;
            else if (shape.Levels == 0) level = 1;
            else if (!int.TryParse(outline, NumberStyles.Integer, CultureInfo.InvariantCulture, out level) || level < 1 || level > shape.Levels) continue;
            var name = resolve(encoded);
            if (name != styles.Entry(shape.Kind, level)) result = result.WithEntry(level, name);
        }
        return result;
    }

    public IEnumerable<XElement> BodyElements(XElement element)
    {
        if (element.Element(Text + "index-body") is not { } body) yield break;
        foreach (var child in body.Elements())
        {
            if (child.Name == Text + "index-title")
            {
                foreach (var inner in child.Elements()) yield return inner;
            }
            else yield return child;
        }
    }
}
