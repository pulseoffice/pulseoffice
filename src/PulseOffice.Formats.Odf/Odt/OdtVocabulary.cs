using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed record OdtFieldEntry(TextFieldKind Kind, XName Element, XName? Argument, IReadOnlyList<XAttribute> Fixed);

internal sealed record OdtFieldRead(TextFieldKind Kind, string? Argument, FieldDetail? Detail);

internal sealed class OdtFieldTable(OdtPreservedFields preserved, PulseOffice.Formats.Odf.TextCodecs.IOdfParagraphText text) : OdtXml
{
    private readonly IReadOnlyList<OdtFieldEntry> _entries =
    [
        new(TextFieldKind.PageNumber, Text + "page-number", null, [new XAttribute(Text + "select-page", "current")]),
        new(TextFieldKind.SlideNumber, Text + "page-number", null, [new XAttribute(Text + "select-page", "current"), new XAttribute(OdtNamespaces.Pulse + "field", "slide-number")]),
        new(TextFieldKind.PageCount, Text + "page-count", null, []),
        new(TextFieldKind.SlideCount, Text + "page-count", null, [new XAttribute(OdtNamespaces.Pulse + "field", "slide-count")]),
        new(TextFieldKind.SlideTitle, Loext + "page-name", null, []),
        new(TextFieldKind.Title, Text + "title", null, []),
        new(TextFieldKind.Subject, Text + "subject", null, []),
        new(TextFieldKind.Description, Text + "description", null, []),
        new(TextFieldKind.Keywords, Text + "keywords", null, []),
        new(TextFieldKind.WordCount, Text + "word-count", null, []),
        new(TextFieldKind.CharacterCount, Text + "character-count", null, []),
        new(TextFieldKind.ParagraphCount, Text + "paragraph-count", null, []),
        new(TextFieldKind.TableCount, Text + "table-count", null, []),
        new(TextFieldKind.ImageCount, Text + "image-count", null, []),
        new(TextFieldKind.ObjectCount, Text + "object-count", null, []),
        new(TextFieldKind.Reference, Text + "reference-ref", Text + "ref-name", []),
        new(TextFieldKind.Bookmark, Text + "bookmark-ref", Text + "ref-name", []),
        new(TextFieldKind.NoteReference, Text + "note-ref", Text + "ref-name", [new XAttribute(Text + "note-class", "footnote")]),
        new(TextFieldKind.Variable, Text + "user-field-get", Text + "name", []),
        new(TextFieldKind.VariableSet, Text + "variable-set", Text + "name", [new XAttribute(Office + "value-type", "string")]),
        new(TextFieldKind.ConditionalText, Text + "conditional-text", Text + "condition", []),
        new(TextFieldKind.HiddenText, Text + "hidden-text", Text + "condition", []),
        new(TextFieldKind.HiddenParagraph, Text + "hidden-paragraph", Text + "condition", []),
        new(TextFieldKind.CustomProperty, Text + "user-defined", Text + "name", []),
        new(TextFieldKind.Database, Text + "database-display", Text + "column-name", []),
        new(TextFieldKind.Sequence, Text + "sequence", Text + "name", [new XAttribute(Style + "num-format", "1")]),
        new(TextFieldKind.Chapter, Text + "chapter", Text + "display", [new XAttribute(Text + "outline-level", "1")]),
        new(TextFieldKind.Author, Text + "author-name", null, []),
        new(TextFieldKind.FileName, Text + "file-name", null, [new XAttribute(Text + "display", "name-and-extension")]),
        new(TextFieldKind.Date, Text + "date", null, []),
        new(TextFieldKind.Time, Text + "time", null, []),
        new(TextFieldKind.ModifiedDate, Text + "modification-date", null, []),
        new(TextFieldKind.CreatedDate, Text + "creation-date", null, []),
    ];

    private readonly IReadOnlyList<(XName Element, TextFieldKind Kind)> _aliases =
    [
        (Text + "initial-creator", TextFieldKind.Author),
        (Text + "creator", TextFieldKind.Author),
        (Text + "variable-get", TextFieldKind.Variable),
        (Text + "user-field-get", TextFieldKind.Variable),
        (Text + "page-continuation", TextFieldKind.PageNumber),
        (Text + "sequence-ref", TextFieldKind.Bookmark),
    ];

    private readonly IReadOnlyList<string> _referenceFormats = ["page", "chapter", "direction", "text", "category-and-value", "caption", "value", "number", "number-no-context", "number-all-superior"];

    public XElement? Build(TextRun run, string shown, OdtWriteSession session)
    {
        var document = session.Document;
        var kind = run.Field;
        var argument = run.FieldArgument;
        if (kind == TextFieldKind.Sender) return Sender(run, shown);
        if (kind == TextFieldKind.Preserved) return preserved.Build(run, shown);
        var entry = _entries.FirstOrDefault(e => e.Kind == kind);
        if (entry is null) return kind == TextFieldKind.Citation ? Citation(run, shown, document) : null;
        var element = new XElement(kind == TextFieldKind.Variable && argument is not null && !document.Variables.ContainsKey(argument) ? Text + "variable-get" : entry.Element,
            entry.Fixed.Select(attribute => new XAttribute(attribute)));
        if (entry.Argument is { } name && argument is not null) element.SetAttributeValue(name, kind is TextFieldKind.ConditionalText or TextFieldKind.HiddenText or TextFieldKind.HiddenParagraph ? "ooow:" + argument : argument);
        else if (argument is not null) element.SetAttributeValue(Pt + "argument", argument);
        if (kind == TextFieldKind.Sequence && argument is { Length: > 0 }) element.SetAttributeValue(Text + "formula", "ooow:" + argument + "+1");
        if (kind == TextFieldKind.Sequence && run.FieldDetail?.Format is { Length: > 0 } reference) element.SetAttributeValue(Text + "ref-name", reference);
        if (kind == TextFieldKind.Bookmark && argument is not null && session.IsSequenceReference(argument, SequenceReferences) && !HasBookmark(document, argument))
            element.Name = Text + "sequence-ref";
        if (kind == TextFieldKind.Database) Database(element, document.MergeSource);
        Detail(element, run, shown, document);
        if (kind != TextFieldKind.HiddenParagraph) AddShown(element, shown);
        return element;
    }

    private readonly TokenMap<PulseOffice.Documents.Fields.MergeCommandType> _commandTypes = new(PulseOffice.Documents.Fields.MergeCommandType.Table, "table",
        (PulseOffice.Documents.Fields.MergeCommandType.Table, "table"), (PulseOffice.Documents.Fields.MergeCommandType.Query, "query"), (PulseOffice.Documents.Fields.MergeCommandType.Command, "command"));

    private void Database(XElement element, PulseOffice.Documents.Fields.MailMergeSource? source)
    {
        element.SetAttributeValue(Text + "table-name", source?.Command ?? string.Empty);
        element.SetAttributeValue(Text + "table-type", _commandTypes.Token(source?.Type ?? PulseOffice.Documents.Fields.MergeCommandType.Table));
        element.SetAttributeValue(Text + "database-name", source?.Database ?? string.Empty);
    }

    private void ReadDatabase(XElement element, TextDocument document)
    {
        if (document.MergeSource is not null) return;
        var table = (string?)element.Attribute(Text + "table-name");
        var database = (string?)element.Attribute(Text + "database-name");
        if (string.IsNullOrEmpty(table) && string.IsNullOrEmpty(database)) return;
        document.MergeSource = new PulseOffice.Documents.Fields.MailMergeSource(database ?? string.Empty, table ?? string.Empty, _commandTypes.Value((string?)element.Attribute(Text + "table-type")));
    }

    private IEnumerable<string> SequenceReferences(TextDocument document) =>
        Paragraphs(document).SelectMany(paragraph => paragraph.Content.Runs)
            .Where(run => run.Field == TextFieldKind.Sequence && run.FieldDetail?.Format is { Length: > 0 })
            .Select(run => run.FieldDetail!.Format!);

    private bool HasBookmark(TextDocument document, string name) =>
        Paragraphs(document).Any(paragraph => paragraph.Marks.Any(mark => mark is PulseOffice.Documents.Marks.BookmarkMark bookmark && bookmark.Name == name));

    private IEnumerable<PulseOffice.Documents.Blocks.ParagraphBlock> Paragraphs(TextDocument document)
    {
        IEnumerable<PulseOffice.Documents.Blocks.ParagraphBlock> Walk(PulseOffice.Documents.Blocks.BlockContainer container) => container.Blocks.SelectMany(block => block switch
        {
            PulseOffice.Documents.Blocks.ParagraphBlock paragraph => [paragraph],
            PulseOffice.Documents.Blocks.SectionBlock section => Walk(section.Content),
            PulseOffice.Documents.Blocks.TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Walk(cell.Content)),
            _ => [],
        });
        return Walk(document.Body)
            .Concat(document.Notes.All.SelectMany(note => Walk(note.Content)))
            .Concat(document.Objects.All.SelectMany(item => item.Content.Stories).SelectMany(story => Walk(story)));
    }

    private void Detail(XElement element, TextRun run, string shown, TextDocument document)
    {
        var detail = run.FieldDetail;
        switch (run.Field)
        {
            case TextFieldKind.Bookmark or TextFieldKind.Reference or TextFieldKind.NoteReference:
                element.SetAttributeValue(Text + "reference-format", detail?.Format ?? "text");
                if (run.Field == TextFieldKind.NoteReference && run.FieldArgument is { } id && document.Notes.Find(id)?.Kind == PulseOffice.Documents.Annotations.NoteKind.Endnote)
                    element.SetAttributeValue(Text + "note-class", "endnote");
                break;
            case TextFieldKind.VariableSet:
                element.SetAttributeValue(Office + "string-value", detail?.Format ?? shown);
                if (detail?.WhenFalse == PulseOffice.Documents.Fields.FieldVisibility.Invisible) element.SetAttributeValue(Text + "display", "none");
                break;
            case TextFieldKind.ConditionalText:
                element.SetAttributeValue(Text + "string-value-if-true", detail?.WhenTrue ?? string.Empty);
                element.SetAttributeValue(Text + "string-value-if-false", detail?.WhenFalse ?? string.Empty);
                element.SetAttributeValue(Text + "current-value", shown == (detail?.WhenTrue ?? string.Empty) ? "true" : "false");
                break;
            case TextFieldKind.Sequence:
                break;
            case TextFieldKind.HiddenParagraph:
                element.SetAttributeValue(Text + "is-hidden", run.Hidden == true ? "true" : "false");
                break;
            case TextFieldKind.HiddenText:
                element.SetAttributeValue(Text + "string-value", detail?.WhenTrue ?? shown);
                element.SetAttributeValue(Text + "is-hidden", run.Hidden == true ? "true" : "false");
                break;
            default:
                if (detail?.Fixed == true) element.SetAttributeValue(Text + "fixed", "true");
                if (detail?.Value is { Length: > 0 } stored && run.Field is TextFieldKind.Date or TextFieldKind.Time)
                    element.SetAttributeValue(Text + (run.Field == TextFieldKind.Date ? "date-value" : "time-value"), stored);
                if (detail?.Format is { Length: > 0 } format) element.SetAttributeValue(Pt + "format", format);
                break;
        }
    }

    private XElement Citation(TextRun run, string shown, TextDocument document)
    {
        var identifier = run.FieldArgument ?? shown;
        var source = document.Bibliography.Find(identifier);
        var element = new XElement(Text + "bibliography-mark",
            new XAttribute(Text + "identifier", identifier),
            new XAttribute(Text + "bibliography-type", source?.Type ?? "book"));
        if (source is not null)
            foreach (var (field, value) in source.Values.Where(pair => pair.Value.Length > 0)) element.SetAttributeValue(Text + field, value);
        AddShown(element, shown);
        return element;
    }

    private readonly IReadOnlySet<string> _senderParts = new HashSet<string>(StringComparer.Ordinal)
    {
        "firstname", "lastname", "initials", "title", "position", "email", "phone-private", "fax", "company", "phone-work",
        "street", "city", "postal-code", "country", "state-or-province",
    };

    public bool Preserves(XElement element) => preserved.Preserves(element);

    private XElement Sender(TextRun run, string shown)
    {
        var part = run.FieldArgument is { } argument && _senderParts.Contains(argument) ? argument : "lastname";
        var element = new XElement(Text + ("sender-" + part));
        if (run.FieldDetail?.Fixed == true) element.SetAttributeValue(Text + "fixed", "true");
        AddShown(element, shown);
        return element;
    }

    public string Shown(XElement element) => text.Line(element);

    private void AddShown(XElement element, string shown) => element.Add(text.Paragraph(shown).Nodes());

    public OdtFieldRead? Read(XElement element, TextDocument document)
    {
        if (preserved.Read(element) is { } kept) return kept;
        if (element.Name == Text + "bibliography-mark") return ReadCitation(element, document);
        if (element.Name.Namespace == Text && element.Name.LocalName.StartsWith("sender-", StringComparison.Ordinal) && _senderParts.Contains(element.Name.LocalName["sender-".Length..]))
            return new OdtFieldRead(TextFieldKind.Sender, element.Name.LocalName["sender-".Length..], (string?)element.Attribute(Text + "fixed") == "true" ? new FieldDetail { Fixed = true } : null);
        var marker = (string?)element.Attribute(Pt + "field");
        var entry = _entries.FirstOrDefault(e => e.Element == element.Name && (marker is null
            ? !e.Fixed.Any(attribute => attribute.Name == Pt + "field")
            : e.Fixed.Any(attribute => attribute.Name == Pt + "field" && attribute.Value == marker)));
        var kind = entry?.Kind ?? _aliases.Where(alias => alias.Element == element.Name).Select(alias => (TextFieldKind?)alias.Kind).FirstOrDefault();
        if (kind is null) return null;
        var argumentName = entry?.Argument ?? (kind is TextFieldKind.Variable ? Text + "name" : kind is TextFieldKind.Bookmark ? Text + "ref-name" : null);
        var argument = (string?)element.Attribute(Pt + "argument") ?? (argumentName is null ? null : (string?)element.Attribute(argumentName));
        if (kind == TextFieldKind.Chapter && (string?)element.Attribute(Pt + "argument") is null && argument == "name") argument = null;
        if (kind is TextFieldKind.ConditionalText or TextFieldKind.HiddenText or TextFieldKind.HiddenParagraph && argument is not null) argument = Unprefixed(argument);
        if (kind == TextFieldKind.Database) ReadDatabase(element, document);
        return new OdtFieldRead(kind.Value, argument, ReadDetail(element, kind.Value));
    }

    private string Unprefixed(string condition)
    {
        foreach (var prefix in new[] { "ooow:", "ooo:", "oooc:" })
            if (condition.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return condition[prefix.Length..];
        return condition;
    }

    private FieldDetail? ReadDetail(XElement element, TextFieldKind kind)
    {
        switch (kind)
        {
            case TextFieldKind.Bookmark or TextFieldKind.Reference or TextFieldKind.NoteReference:
                var format = (string?)element.Attribute(Text + "reference-format");
                if (element.Name == Text + "sequence-ref" && format is null) format = "category-and-value";
                if (format is "number-no-context" or "number-all-superior") format = "number";
                return format is null or "text" || !_referenceFormats.Contains(format) ? null : new FieldDetail { Format = format };
            case TextFieldKind.VariableSet:
                return new FieldDetail
                {
                    Format = (string?)element.Attribute(Office + "string-value") ?? (string?)element.Attribute(Office + "value") ?? Shown(element),
                    WhenFalse = (string?)element.Attribute(Text + "display") == "none" ? PulseOffice.Documents.Fields.FieldVisibility.Invisible : null,
                };
            case TextFieldKind.ConditionalText:
                return new FieldDetail
                {
                    WhenTrue = (string?)element.Attribute(Text + "string-value-if-true") ?? string.Empty,
                    WhenFalse = (string?)element.Attribute(Text + "string-value-if-false") ?? string.Empty,
                };
            case TextFieldKind.HiddenText:
                return new FieldDetail { WhenTrue = (string?)element.Attribute(Text + "string-value") ?? Shown(element) };
            case TextFieldKind.Sequence:
                return (string?)element.Attribute(Text + "ref-name") is { Length: > 0 } reference ? new FieldDetail { Format = reference } : null;
            default:
                var fixedValue = (string?)element.Attribute(Text + "fixed") == "true";
                var own = (string?)element.Attribute(Pt + "format");
                var stored = fixedValue ? (string?)element.Attribute(Text + "date-value") ?? (string?)element.Attribute(Text + "time-value") : null;
                return fixedValue || own is not null ? new FieldDetail { Fixed = fixedValue, Format = own, Value = stored } : null;
        }
    }

    private OdtFieldRead ReadCitation(XElement element, TextDocument document)
    {
        var identifier = ((string?)element.Attribute(Text + "identifier") ?? Shown(element)).Trim();
        if (identifier.StartsWith("CITATION ", StringComparison.OrdinalIgnoreCase)) identifier = identifier["CITATION ".Length..].Trim().Trim('"');
        var values = element.Attributes()
            .Where(attribute => attribute.Name.Namespace == Text && attribute.Name.LocalName is not ("identifier" or "bibliography-type"))
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value, StringComparer.Ordinal);
        if (document.Bibliography.Find(identifier) is null)
            document.Bibliography.Store(new PulseOffice.Documents.Fields.BibliographySource(identifier, (string?)element.Attribute(Text + "bibliography-type") ?? "book", values));
        return new OdtFieldRead(TextFieldKind.Citation, identifier, null);
    }
}

internal sealed class OdtNumberFormats
{
    private readonly TokenMap<NumberFormat> _formats = new(NumberFormat.Arabic, "1",
        (NumberFormat.Arabic, "1"), (NumberFormat.LowerLetter, "a"), (NumberFormat.UpperLetter, "A"),
        (NumberFormat.LowerRoman, "i"), (NumberFormat.UpperRoman, "I"), (NumberFormat.None, ""));

    public string Token(NumberFormat format) => _formats.Token(format);

    public NumberFormat Value(string? token) => token is null ? NumberFormat.Arabic : _formats.Value(token);
}

internal sealed class OdtVocabulary
{
    public TokenMap<ParagraphStyleCategory> Categories { get; } = new(ParagraphStyleCategory.Text, "text",
        (ParagraphStyleCategory.Text, "text"), (ParagraphStyleCategory.Chapter, "chapter"), (ParagraphStyleCategory.List, "list"),
        (ParagraphStyleCategory.Index, "index"), (ParagraphStyleCategory.Special, "extra"));

    public TokenMap<PageUsage> Usage { get; } = new(PageUsage.All, "all",
        (PageUsage.All, "all"), (PageUsage.Left, "left"), (PageUsage.Right, "right"), (PageUsage.Mirrored, "mirrored"));

    public TokenMap<NoteNumbering> Restart { get; } = new(NoteNumbering.Document, "document",
        (NoteNumbering.Document, "document"), (NoteNumbering.Chapter, "chapter"), (NoteNumbering.Page, "page"));

    public TokenMap<NoteKind> NoteClass { get; } = new(NoteKind.Footnote, "footnote",
        (NoteKind.Footnote, "footnote"), (NoteKind.Endnote, "endnote"));

    public TokenMap<PulseOffice.Documents.Blocks.CellVerticalAlign> VerticalAlign { get; } = new(PulseOffice.Documents.Blocks.CellVerticalAlign.Top, "top",
        (PulseOffice.Documents.Blocks.CellVerticalAlign.Top, "top"), (PulseOffice.Documents.Blocks.CellVerticalAlign.Middle, "middle"),
        (PulseOffice.Documents.Blocks.CellVerticalAlign.Bottom, "bottom"));
}
