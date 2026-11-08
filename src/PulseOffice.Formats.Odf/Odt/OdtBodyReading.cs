using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Odt;

using TableRow = PulseOffice.Documents.Blocks.TableRow;
using TableCell = PulseOffice.Documents.Blocks.TableCell;

internal sealed class OdtItemStart(int? value)
{
    private int? _value = value;
    private bool _numbered = true;

    public int? Take()
    {
        var taken = _value;
        _value = null;
        return taken;
    }

    public bool TakeNumber()
    {
        var numbered = _numbered;
        _numbered = false;
        return numbered;
    }
}

internal sealed record OdtListState(string? StyleName, int Depth, bool Header, OdtItemStart Start, string Thread);

internal sealed class OdtStyleLookup(OdtCharacterCodec characters, OdtParagraphCodec paragraphs) : OdtXml
{
    public AutomaticParagraph Paragraph(string? name, OdtReadContext context)
    {
        if (context.Automatic.Find("paragraph", name) is not { } style)
            return new AutomaticParagraph(name is null ? null : context.StyleNames.Resolve(name), ParagraphProperties.None, CharacterProperties.None, null, false);

        var text = style.Element(Style + "text-properties");
        var paragraph = paragraphs.Read(style.Element(Style + "paragraph-properties"), text);
        var character = characters.Read(text, context.FontFaces).Properties;
        var parent = (string?)style.Attribute(Style + "parent-style-name") is { } p ? context.StyleNames.Resolve(p) : null;
        var master = (string?)style.Attribute(Style + "master-page-name") is { Length: > 0 } m ? context.PageNames.Resolve(m) : null;
        return new AutomaticParagraph(parent, paragraph, character, master, (string?)style.Attribute(Pt + "implied-page") == "true");
    }

    public AutomaticText TextStyle(string? name, OdtReadContext context)
    {
        if (context.Automatic.Find("text", name) is not { } style)
            return new AutomaticText(name is null ? null : context.StyleNames.Resolve(name), CharacterProperties.None);

        var parent = (string?)style.Attribute(Style + "parent-style-name") is { } p ? context.StyleNames.Resolve(p) : null;
        return new AutomaticText(parent, characters.Read(style.Element(Style + "text-properties"), context.FontFaces).Properties);
    }
}

internal sealed class OdtBlockReader(OdtParagraphReader paragraphs, OdtTableReader tables, OdtSectionReader sections, OdtObjectReader objects) : OdtXml
{
    public List<Block> Read(XElement container, OdtReadContext context)
    {
        var blocks = new List<Block>();
        foreach (var child in container.Elements()) Read(child, context, null, blocks);
        return blocks;
    }

    public void Read(XElement element, OdtReadContext context, OdtListState? list, List<Block> blocks)
    {
        if (context.Depth > OdtReadContext.MaximumDepth) return;
        context.Depth++;
        try
        {
            ReadElement(element, context, list, blocks);
        }
        finally
        {
            context.Depth--;
        }
    }

    private void ReadElement(XElement element, OdtReadContext context, OdtListState? list, List<Block> blocks)
    {
        var name = element.Name;
        if (name == Text + "p" || name == Text + "h")
        {
            blocks.Add(paragraphs.Read(element, context, list, this));
        }
        else if (name == Text + "list")
        {
            var style = (string?)element.Attribute(Text + "style-name") ?? list?.StyleName;
            var depth = list is null ? 0 : list.Depth + 1;
            var declared = (string?)element.Attribute(Xml + "id");
            OdtListThread thread;
            if (list is null)
            {
                thread = context.Lists.Read(style, declared, (string?)element.Attribute(Text + "continue-numbering") == "true", (string?)element.Attribute(Text + "continue-list"));
            }
            else
            {
                thread = new OdtListThread(list.Thread, false);
                context.Lists.Declare(declared, list.Thread);
            }
            var restarts = thread.Restarts;
            foreach (var item in element.Elements())
            {
                if (item.Name != Text + "list-item" && item.Name != Text + "list-header") continue;
                int? start = int.TryParse((string?)item.Attribute(Text + "start-value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 0, ushort.MaxValue) : null;
                if (restarts && item.Name == Text + "list-item")
                {
                    start ??= context.Document.Styles.List.Find(context.StyleNames.Resolve(style!))?.Level(0).StartAt ?? 1;
                    restarts = false;
                }
                var state = new OdtListState(style, depth, item.Name == Text + "list-header", new OdtItemStart(start), thread.Raw);
                foreach (var child in item.Elements()) Read(child, context, state, blocks);
            }
        }
        else if (name == Table + "table")
        {
            blocks.Add(tables.Read(element, context, this));
        }
        else if (name == Text + "section")
        {
            blocks.Add(sections.Read(element, context, this));
        }
        else if (sections.HandlesIndex(name))
        {
            blocks.Add(sections.ReadIndex(element, context, this));
        }
        else if (name == Text + "index-body" || name == Text + "index-title" || IsIndex(name))
        {
            foreach (var child in element.Elements()) Read(child, context, list, blocks);
        }
        else if (objects.Handles(element))
        {
            objects.ReadTopLevel(element, context, this);
        }
    }

    private bool IsIndex(XName name) =>
        name.Namespace == Text && (name.LocalName.EndsWith("-index", StringComparison.Ordinal) || name.LocalName == "table-of-content" || name.LocalName == "bibliography");
}

internal sealed class OdtParagraphReader(
    OdtStyleLookup styles,
    OdtInlineReader inline,
    IDirectFormatting direct,
    IStyleResolver resolver) : OdtXml
{
    public ParagraphBlock Read(XElement element, OdtReadContext context, OdtListState? list, OdtBlockReader blocks)
    {
        var id = context.Id(element, "p");
        var automatic = styles.Paragraph((string?)element.Attribute(Text + "style-name"), context);
        var (runs, marks) = inline.Read(element, id, automatic.Character, context, blocks);
        if (runs.Count == 0 && automatic.Character != CharacterProperties.None) runs = [direct.Set(new TextRun(string.Empty), automatic.Character)];
        var text = string.Concat(runs.Select(run => run.Text));

        var properties = automatic.Paragraph with { PageStyleName = automatic.Implied ? null : automatic.Master };
        var paragraph = direct.Set(new TextParagraph(text) { StyleName = automatic.Parent }, properties).WithRuns(runs);
        if (runs.Count == 0) paragraph = paragraph with { Runs = [] };
        if ((string?)element.Attribute(Pt + "next-master-page") is { Length: > 0 } deferred && paragraph.PageStyleName is null)
            paragraph = paragraph with { NextPageStyleName = context.PageNames.Resolve(deferred) };

        var resolved = resolver.ResolveParagraphStyle(context.Document.Styles, paragraph.StyleName);
        if (element.Name == Text + "h" && (string?)element.Attribute(Pt + "outline-inherited") != "true")
        {
            var level = int.TryParse((string?)element.Attribute(Text + "outline-level"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
            if (context.Ours || level != resolved.OutlineLevel) paragraph = paragraph with { OutlineLevel = level };
        }

        if (list is not null)
        {
            var listStyle = list.StyleName is null ? null : context.StyleNames.Resolve(list.StyleName);
            var inherited = context.Ours
                ? (string?)element.Attribute(Pt + "list-inherited") == "true"
                : listStyle is not null && listStyle == resolved.ListStyleName;
            var kind = (string?)element.Attribute(Pt + "list-kind");
            var numbered = kind is null
                ? context.Document.Styles.List.Find(listStyle ?? resolved.ListStyleName)?.Level(list.Depth).Format is not (null or NumberFormat.Bullet)
                : kind == "number";
            var header = list.Header || !list.Start.TakeNumber();
            paragraph = paragraph with
            {
                ListStyleName = inherited ? null : listStyle,
                BulletLevel = Math.Min(list.Depth, ListStyle.LevelCount - 1),
                Bullet = !header && !numbered,
                Numbered = !header && numbered,
                ListRestart = list.Start.Take(),
                ListId = list.Thread,
                ListHeader = header,
            };
        }

        return new ParagraphBlock(id, paragraph) { Marks = marks };
    }
}

internal sealed class OdtInlineReader(
    OdtStyleLookup styles,
    OdtFieldTable fields,
    IDirectFormatting direct,
    OdtCommentReader comments,
    OdtNoteReader notes,
    OdtObjectReader objects,
    OdtContentControls controls,
    OdtLegacyFormFields forms) : OdtXml
{
    private sealed record Format(CharacterProperties Properties, string? Style, string? Link)
    {
        public string? Ruby { get; init; }
    }

    private sealed class Builder
    {
        public readonly List<(StringBuilder Text, Format Format, TextFieldKind Field, string? Argument, FieldDetail? Detail, bool? Hidden)> Segments = [];
        public readonly List<InlineMark> Marks = [];
        public int Length;
        public bool Collapsible = true;
        public readonly Stack<string> OpenForms = new();
    }

    public (IReadOnlyList<TextRun> Runs, IReadOnlyList<InlineMark> Marks) Read(
        XElement paragraph, string paragraphId, CharacterProperties paragraphCharacter, OdtReadContext context, OdtBlockReader blocks)
    {
        var builder = new Builder();
        Walk(paragraph, new Format(paragraphCharacter, null, null), builder, paragraphId, context, blocks);
        while (builder.OpenForms.TryPop(out var open)) builder.Marks.Add(new ContentControlMark(builder.Length, open, MarkEdge.End));

        var runs = builder.Segments
            .Where(segment => segment.Text.Length > 0 || segment.Field != TextFieldKind.None)
            .Select(segment =>
            {
                var run = Run(segment.Text.ToString(), segment.Format, segment.Field, segment.Argument);
                return run with { FieldDetail = segment.Detail, Hidden = segment.Hidden ?? run.Hidden };
            })
            .ToList();
        return (runs, builder.Marks);
    }

    private TextRun Run(string text, Format format, TextFieldKind field, string? argument)
    {
        var run = direct.Apply(new TextRun(text) { StyleName = format.Style, Hyperlink = format.Link, Field = field, FieldArgument = argument, Ruby = format.Ruby }, format.Properties);
        var properties = format.Properties;
        var flags = RunOverrides.None;
        if (properties.Bold == false) flags |= RunOverrides.Bold;
        if (properties.Italic == false) flags |= RunOverrides.Italic;
        if (properties.Underline == false) flags |= RunOverrides.Underline;
        if (properties.Strikethrough == false) flags |= RunOverrides.Strikethrough;
        if (properties.DoubleUnderline == false) flags |= RunOverrides.DoubleUnderline;
        if (properties.Overline == false) flags |= RunOverrides.Overline;
        if (properties.Shadow == false) flags |= RunOverrides.Shadow;
        if (properties.Outline == false) flags |= RunOverrides.Outline;
        if (properties.SmallCaps == false) flags |= RunOverrides.SmallCaps;
        if (properties.Baseline == TextBaseline.Normal) flags |= RunOverrides.Baseline;
        return run with { Explicit = flags };
    }

    private void Append(Builder builder, Format format, string value, bool collapse)
    {
        var last = builder.Segments.Count > 0 ? builder.Segments[^1] : default;
        StringBuilder target;
        if (builder.Segments.Count > 0 && last.Field == TextFieldKind.None && last.Format == format) target = last.Text;
        else
        {
            target = new StringBuilder();
            builder.Segments.Add((target, format, TextFieldKind.None, null, null, null));
        }

        foreach (var c in value)
        {
            if (collapse && c is ' ' or '\t' or '\r' or '\n')
            {
                if (builder.Collapsible) continue;
                target.Append(' ');
                builder.Length++;
                builder.Collapsible = true;
                continue;
            }

            target.Append(c);
            builder.Length++;
            builder.Collapsible = false;
        }
    }

    private void Walk(XElement element, Format format, Builder builder, string paragraphId, OdtReadContext context, OdtBlockReader blocks)
    {
        if (context.Depth > OdtReadContext.MaximumDepth) return;
        context.Depth++;
        try
        {
            WalkNodes(element, format, builder, paragraphId, context, blocks);
        }
        finally
        {
            context.Depth--;
        }
    }

    private void WalkNodes(XElement element, Format format, Builder builder, string paragraphId, OdtReadContext context, OdtBlockReader blocks)
    {
        foreach (var node in element.Nodes())
        {
            if (node is XText text)
            {
                Append(builder, format, text.Value, collapse: true);
                continue;
            }

            if (node is not XElement child) continue;
            var name = child.Name;

            if (name == Text + "span" && !fields.Preserves(child))
            {
                var automatic = styles.TextStyle((string?)child.Attribute(Text + "style-name"), context);
                Walk(child, format with { Properties = automatic.Character.Over(format.Properties), Style = automatic.Parent ?? format.Style }, builder, paragraphId, context, blocks);
            }
            else if (name == Text + "ruby")
            {
                var ruby = (string?)child.Element(Text + "ruby-text") is { Length: > 0 } annotation ? annotation : null;
                if (child.Element(Text + "ruby-base") is { } rubyBase) Walk(rubyBase, format with { Ruby = ruby }, builder, paragraphId, context, blocks);
            }
            else if (name == Text + "a")
            {
                if ((string?)child.Attribute(Xlink + "href") is not { Length: > 0 } || child.Value.Length == 0 && !child.Descendants().Any(item => item.Name.Namespace == Draw))
                    context.Losses.Add(OdtReadLosses.EmptyLinks);
                var inner = format with { Link = (string?)child.Attribute(Xlink + "href") ?? format.Link };
                if ((string?)child.Attribute(Text + "style-name") is { } linkStyle && styles.TextStyle(linkStyle, context) is { } linkFormat && linkFormat.Parent is { } parentStyle && format.Style is null && parentStyle != PulseOffice.Documents.Styles.DefaultStyleNames.InternetLink)
                    inner = inner with { Style = parentStyle };
                Walk(child, inner, builder, paragraphId, context, blocks);
            }
            else if (name == Text + "s")
            {
                var count = context.TakeSpaces(int.TryParse((string?)child.Attribute(Text + "c"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) ? Math.Clamp(c, 1, 100_000) : 1);
                Append(builder, format, new string(' ', count), collapse: false);
                builder.Collapsible = false;
            }
            else if (name == Text + "tab") Append(builder, format, "\t", collapse: false);
            else if (name == Text + "line-break") Append(builder, format, "\n", collapse: false);
            else if (name == Text + "soft-page-break" || name.Namespace == Office && name.LocalName != "annotation" && name.LocalName != "annotation-end") { }
            else if (fields.Read(child, context.Document) is { } field)
            {
                bool? hidden = field.Kind is TextFieldKind.HiddenText or TextFieldKind.HiddenParagraph ? (string?)child.Attribute(Text + "is-hidden") == "true" : null;
                var content = fields.Shown(child);
                var shown = field.Kind == TextFieldKind.HiddenParagraph && content.Length == 0 ? PulseOffice.Documents.Fields.ConditionalFieldSource.HiddenParagraphMark : content;
                builder.Segments.Add((new StringBuilder(shown), format, field.Kind, field.Argument, field.Detail, hidden));
                builder.Length += shown.Length;
                builder.Collapsible = false;
            }
            else if (name == Text + "bookmark" || name == Text + "bookmark-start" || name == Text + "bookmark-end")
            {
                var edge = name.LocalName switch { "bookmark-start" => MarkEdge.Start, "bookmark-end" => MarkEdge.End, _ => MarkEdge.Point };
                builder.Marks.Add(new BookmarkMark(builder.Length, (string?)child.Attribute(Text + "name") ?? string.Empty, edge));
            }
            else if (name == Pt + "field-code" && Enum.TryParse<FieldCodePart>((string?)child.Attribute(Pt + "part"), true, out var part) && Enum.IsDefined(part)
                     && (string?)child.Attribute(Pt + "field-id") is { Length: > 0 } fieldId)
            {
                builder.Marks.Add(new FieldCodeMark(builder.Length, fieldId, part, (string?)child.Attribute(Pt + "code") ?? string.Empty)
                {
                    Data = part == FieldCodePart.Begin ? (string?)child.Attribute(Pt + "data") : null,
                    Locked = part == FieldCodePart.Begin && (string?)child.Attribute(Pt + "locked") == "true",
                });
            }
            else if (name == Text + "change-start" || name == Text + "change-end" || name == Text + "change")
            {
                var edge = name.LocalName switch { "change-start" => MarkEdge.Start, "change-end" => MarkEdge.End, _ => MarkEdge.Point };
                builder.Marks.Add(new ChangeMark(builder.Length, (string?)child.Attribute(Text + "change-id") ?? string.Empty, edge));
            }
            else if (name == Office + "annotation")
            {
                if (comments.Read(child, context, blocks) is { } mark) builder.Marks.Add(mark with { Offset = builder.Length });
            }
            else if (name == Office + "annotation-end")
            {
                if ((string?)child.Attribute(Office + "name") is { } endedName)
                {
                    var ended = context.CommentNames.GetValueOrDefault(endedName, endedName);
                    context.EndedComments.Add(ended);
                    builder.Marks.Add(new CommentMark(builder.Length, ended, MarkEdge.End));
                }
            }
            else if (name == Text + "note")
            {
                if (notes.Read(child, context, blocks) is { } noteId) builder.Marks.Add(new NoteMark(builder.Length, noteId));
            }
            else if (child.Name == controls.Name)
            {
                var controlId = controls.Id(child) is { Length: > 0 } stored ? stored : context.Document.NextId("cc");
                context.Document.Ids.Reserve(controlId);
                builder.Marks.Add(new ContentControlMark(builder.Length, controlId, MarkEdge.Start) { Control = controls.Read(child) });
                Walk(child, format, builder, paragraphId, context, blocks);
                builder.Marks.Add(new ContentControlMark(builder.Length, controlId, MarkEdge.End));
            }
            else if (controls.IsItem(child)) { }
            else if (name == forms.Point || name == forms.Start)
            {
                if (forms.Read(child) is not { } form) continue;
                var formId = context.Document.NextId("cc");
                context.Document.Ids.Reserve(formId);
                builder.Marks.Add(new ContentControlMark(builder.Length, formId, MarkEdge.Start) { Control = form.Control });
                if (name == forms.Start)
                {
                    builder.OpenForms.Push(formId);
                    continue;
                }
                if (form.Shown is { Length: > 0 } shown) Append(builder, format, shown, collapse: false);
                builder.Marks.Add(new ContentControlMark(builder.Length, formId, MarkEdge.End));
            }
            else if (name == forms.End)
            {
                if (builder.OpenForms.TryPop(out var openForm)) builder.Marks.Add(new ContentControlMark(builder.Length, openForm, MarkEdge.End));
            }
            else if (objects.Handles(child))
            {
                if (objects.Read(child, paragraphId, context, blocks) is { } objectId) builder.Marks.Add(new ObjectMark(builder.Length, objectId));
            }
            else if (IndexMark(child, builder, paragraphId, context)) { }
            else if (name.Namespace == Text || name.Namespace == Loext || name.Namespace == Officeooo)
            {
                Walk(child, format, builder, paragraphId, context, blocks);
            }
        }
    }

    private bool IndexMark(XElement element, Builder builder, string paragraphId, OdtReadContext context)
    {
        var local = element.Name.LocalName;
        if (element.Name.Namespace != Text) return false;
        var kindName = local.EndsWith("-start", StringComparison.Ordinal) ? local[..^6] : local.EndsWith("-end", StringComparison.Ordinal) ? local[..^4] : local;
        if (kindName is not ("alphabetical-index-mark" or "toc-mark" or "user-index-mark")) return false;

        var index = kindName switch
        {
            "alphabetical-index-mark" => "alphabetical",
            "toc-mark" => (string?)element.Attribute(Pt + "index") ?? "toc",
            _ => (string?)element.Attribute(Text + "index-name") ?? "user",
        };
        var id = (string?)element.Attribute(Text + "id") ?? string.Empty;

        if (local.EndsWith("-start", StringComparison.Ordinal))
        {
            var entry = (string?)element.Attribute(Pt + "entry");
            builder.Marks.Add(new IndexEntryMark(builder.Length, entry ?? string.Empty, MarkEdge.Start) { Index = index, Key1 = Key(element, "key1"), Key2 = Key(element, "key2") });
            if (entry is null) context.OpenIndexMarks[id] = (paragraphId, builder.Length, index);
        }
        else if (local.EndsWith("-end", StringComparison.Ordinal))
        {
            var start = builder.Marks.OfType<IndexEntryMark>().LastOrDefault(mark => mark.Edge == MarkEdge.Start && mark.Index == index);
            var entry = start?.Entry ?? string.Empty;
            if (context.OpenIndexMarks.Remove(id, out var open) && open.ParagraphId == paragraphId && start is not null)
            {
                entry = Collected(builder)[open.Offset..builder.Length];
                var position = builder.Marks.IndexOf(start);
                builder.Marks[position] = start with { Entry = entry };
            }
            builder.Marks.Add(new IndexEntryMark(builder.Length, entry, MarkEdge.End) { Index = index, Key1 = start?.Key1, Key2 = start?.Key2 });
        }
        else
        {
            var value = ((string?)element.Attribute(Text + "string-value") ?? element.Value).Trim();
            var keys = new[] { Key(element, "key1"), Key(element, "key2") }.OfType<string>().ToList();
            if (value.Length == 0 && keys.Count > 0)
            {
                value = keys[^1];
                keys.RemoveAt(keys.Count - 1);
            }
            builder.Marks.Add(new IndexEntryMark(builder.Length, value, MarkEdge.Point) { Index = index, Key1 = keys.Count > 0 ? keys[0] : null, Key2 = keys.Count > 1 ? keys[1] : null });
        }
        return true;
    }

    private string? Key(XElement element, string name) => (string?)element.Attribute(Text + name) is { } key && key.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    private string Collected(Builder builder) => string.Concat(builder.Segments.Select(segment => segment.Text.ToString()));
}

internal sealed class OdtCommentReader : OdtXml
{
    public CommentMark? Read(XElement annotation, OdtReadContext context, OdtBlockReader blocks)
    {
        var name = (string?)annotation.Attribute(Office + "name");
        var id = context.Id(name, "c");
        if (name is { Length: > 0 }) context.CommentNames[name] = id;
        if ((string?)annotation.Attribute(Loext + "parent-name") is { Length: > 0 } parentName) context.CommentParents[id] = parentName;
        var content = new BlockContainer(annotation.Elements()
            .Where(child => child.Name.Namespace != Dc && child.Name.Namespace != Meta && child.Name.Namespace != Loext)
            .SelectMany(child =>
            {
                var list = new List<Block>();
                blocks.Read(child, context, null, list);
                return list;
            }));
        var comment = new Comment(id, (string?)annotation.Element(Dc + "creator") ?? string.Empty, Date((string?)annotation.Element(Dc + "date")), content)
        {
            Initials = (string?)annotation.Element(Meta + "creator-initials") ?? (string?)annotation.Element(Loext + "sender-initials"),
            Resolved = (string?)annotation.Attribute(Loext + "resolved") == "true",
        };
        context.Document.Comments.Add(comment);
        return new CommentMark(0, id, (string?)annotation.Attribute(Pt + "point") == "true" ? MarkEdge.Point : MarkEdge.Start);
    }

    public DateTimeOffset Date(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DateTimeOffset.MinValue;
        var text = value.Trim();
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0)
        {
            var end = dot + 1;
            while (end < text.Length && char.IsAsciiDigit(text[end])) end++;
            var fraction = text[(dot + 1)..end];
            if (fraction.Length > 7) text = text[..(dot + 1)] + fraction[..7] + text[end..];
        }
        var zoned = text.EndsWith('Z') || text.LastIndexOfAny(['+', '-']) > 10;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, zoned ? DateTimeStyles.None : DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : DateTimeOffset.MinValue;
    }
}

internal sealed class OdtNoteReader(OdtVocabulary vocabulary) : OdtXml
{
    public string? Read(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var id = context.Id((string?)element.Attribute(Text + "id"), "ftn");
        var body = element.Element(Text + "note-body");
        var note = new Note(id, vocabulary.NoteClass.Value((string?)element.Attribute(Text + "note-class")),
            new BlockContainer(body is null ? [] : blocks.Read(body, context)))
        {
            Citation = (string?)element.Element(Text + "note-citation")?.Attribute(Text + "label"),
        };
        context.Document.Notes.Add(note);
        return id;
    }
}

internal sealed class OdtObjectReader(OdtMeasure measure, OdtFrameCodec frames, OdfImageTypes images, OdfImageMapCodec imageMaps, OdtDrawingReader drawings, OdtContours contours, OdfSignatureLineCodec signatures) : OdtXml
{
    private (double Left, double Top, double Right, double Bottom) Clip(string? clip, DocumentSize size)
    {
        if (clip is null) return default;
        var open = clip.IndexOf('(');
        var close = clip.IndexOf(')');
        if (open < 0 || close <= open) return default;
        var parts = clip[(open + 1)..close].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return default;
        double Part(int index) => measure.Points(parts[index]) ?? 0;
        double width = size.Width.ToPoints(), height = size.Height.ToPoints();
        return (width > 0 ? Part(3) / width : 0, height > 0 ? Part(0) / height : 0, width > 0 ? Part(1) / width : 0, height > 0 ? Part(2) / height : 0);
    }

    public bool Handles(XElement element) => element.Name == Draw + "frame" || element.Name == Draw + "a" || drawings.Handles(element);

    public void ReadTopLevel(XElement element, OdtReadContext context, OdtBlockReader blocks) => Read(element, null, context, blocks);

    private readonly IReadOnlySet<string> _plainTextElements = new HashSet<string>(StringComparer.Ordinal)
    {
        "p", "h", "span", "s", "tab", "line-break", "soft-page-break", "bookmark", "bookmark-start", "bookmark-end", "list", "list-item",
    };

    private readonly IReadOnlySet<string> _rectangles = new HashSet<string>(StringComparer.Ordinal) { "rectangle", "ooxml-rect", "rect" };

    private readonly IReadOnlySet<string> _visibleFills = new HashSet<string>(StringComparer.Ordinal) { "solid", "gradient", "hatch", "bitmap" };

    private bool CaptionRectangle(XElement shape, XElement? graphic, OdtReadContext context) =>
        !context.Ours && !_visibleFills.Contains((string?)graphic?.Attribute(Draw + "fill") ?? "none") && RectangleWithRichText(shape);

    private bool RectangleWithRichText(XElement shape) =>
        _rectangles.Contains((string?)shape.Element(Draw + "enhanced-geometry")?.Attribute(Draw + "type") ?? string.Empty)
        && shape.Elements().Where(child => child.Name == Text + "p" || child.Name == Text + "list" || child.Name == Text + "h").DescendantsAndSelf()
            .Any(element => element.Name.Namespace == Draw || element.Name.LocalName == "table" || element.Name.Namespace == Text && !_plainTextElements.Contains(element.Name.LocalName));

    public string? Read(XElement element, string? paragraphId, OdtReadContext context, OdtBlockReader blocks)
    {
        var frame = element.Name == Draw + "a" ? element.Element(Draw + "frame") ?? element.Elements().FirstOrDefault(drawings.Handles) : element;
        if (frame is null) return null;

        var anchorType = (string?)frame.Attribute(Text + "anchor-type") ?? "paragraph";
        ObjectAnchor anchor = anchorType switch
        {
            "page" => new PageAnchor(measure.Integer((string?)frame.Attribute(Text + "anchor-page-number")) ?? 1),
            "char" when paragraphId is not null => new CharacterAnchor(paragraphId),
            "as-char" when paragraphId is not null => new AsCharacterAnchor(paragraphId),
            _ when paragraphId is not null => new ParagraphAnchor(paragraphId),
            _ => new PageAnchor(measure.Integer((string?)frame.Attribute(Text + "anchor-page-number")) ?? 1),
        };

        var height = measure.Twips((string?)frame.Attribute(Svg + "height"))
            ?? measure.Twips((string?)frame.Element(Draw + "text-box")?.Attribute(Fo + "min-height"))
            ?? measure.Twips((string?)frame.Attribute(Fo + "min-height"))
            ?? Twip.Zero;
        var size = new DocumentSize(Length(frame, "width"), height);
        var style = context.Automatic.Find("graphic", (string?)frame.Attribute(Draw + "style-name"));
        var graphic = style?.Element(Style + "graphic-properties");
        var placement = new ObjectPlacement(Length(frame, "x"), Length(frame, "y"));
        var rotation = double.TryParse((string?)frame.Attribute(Pt + "rotation"), NumberStyles.Float, CultureInfo.InvariantCulture, out var stored) ? stored : 0;
        if (frame.Attribute(Pt + "rotation") is null && frame.Name == Draw + "frame" && contours.Transform(frame, size) is { } transformed)
        {
            rotation = transformed.Rotation;
            placement = new ObjectPlacement(transformed.X, transformed.Y);
        }
        var shapeChain = (string?)graphic?.Attribute(Draw + "chain-next-name");
        var shapeBox = frame.Name == Draw + "custom-shape" && (shapeChain is not null || context.Automatic.ChainTargets.Contains((string?)frame.Attribute(Draw + "name") ?? string.Empty) || CaptionRectangle(frame, graphic, context));
        var drawn = shapeBox ? null : frame.Element(Draw + "text-box") is null && frame.Element(Draw + "image") is null || frame.Element(Draw + "object") is not null || frame.Name != Draw + "frame"
            ? drawings.Read(frame, context.Document.NextId("drw_"), context, holder => new BlockContainer(blocks.Read(holder, context)))
            : null;
        AnchoredContent? content;
        if (drawn is not null)
        {
            content = drawn.Content is PulseOffice.Drawing.Shapes.ShapeObject shape && frame.Elements().Any(child => child.Name == Text + "p" || child.Name == Text + "list" || child.Name == Text + "h")
                ? new EmbeddedContent(shape with { Text = null }, new BlockContainer(blocks.Read(frame, context)))
                : drawn.Content is PulseOffice.Drawing.Freeform.FreeformObject && frame.Elements().Any(child => child.Name == Text + "p" || child.Name == Text + "list" || child.Name == Text + "h")
                    ? new EmbeddedContent(drawn.Content, new BlockContainer(blocks.Read(frame, context)))
                    : new EmbeddedContent(drawn.Content, null, drawn.Stories);
            size = new DocumentSize(drawn.Bounds.Width, drawn.Bounds.Height);
            placement = new ObjectPlacement(drawn.Bounds.Left, drawn.Bounds.Top);
            rotation = drawn.Rotation;
        }
        else if (shapeBox)
        {
            content = new TextBoxContent(new BlockContainer(blocks.Read(frame, context))) { AutoGrowHeight = false, NextFrameName = shapeChain };
            placement = new ObjectPlacement(Length(frame, "x"), Length(frame, "y"));
        }
        else content = Content(frame, size, context, blocks);
        if (content is EmbeddedContent { Value: RasterImageObject picture })
            content = new EmbeddedContent(picture with
            {
                Filter = (string?)graphic?.Attribute(Draw + "color-mode") == "greyscale" ? ImageFilter.Grayscale : picture.Filter,
                Crop = Clip((string?)graphic?.Attribute(Fo + "clip"), size),
                Map = imageMaps.Read(frame, size),
            });
        if (content is null) return null;
        if (content is TextBoxContent box) box.Look = frames.ReadLook(graphic);

        var id = context.Id((string?)frame.Attribute(Xml + "id") ?? (string?)frame.Attribute(Pt + "item-id"), "obj");
        var item = new AnchoredObject(id, anchor, content, size)
        {
            Name = drawn is not null ? (string?)frame.Attribute(Pt + "item-name") ?? (context.Ours ? null : (string?)frame.Attribute(Draw + "name")) : (string?)frame.Attribute(Pt + "unnamed") == "true" ? null : (string?)frame.Attribute(Draw + "name"),
            ZOrder = measure.Integer((string?)frame.Attribute(Draw + "z-index")) ?? 0,
            RotationDegrees = rotation,
            Placement = placement with
            {
                Horizontal = frames.Horizontal((string?)graphic?.Attribute(Style + "horizontal-rel")),
                Vertical = frames.Vertical((string?)graphic?.Attribute(Style + "vertical-rel")),
            },
            Wrap = frames.Wrap((string?)graphic?.Attribute(Style + "wrap") ?? "parallel", (string?)graphic?.Attribute(Pt + "wrap") == "top-and-bottom"),
            WrapSpacing = new Margins(
                measure.Twips((string?)graphic?.Attribute(Fo + "margin-left")) ?? Twip.Zero,
                measure.Twips((string?)graphic?.Attribute(Fo + "margin-top")) ?? Twip.Zero,
                measure.Twips((string?)graphic?.Attribute(Fo + "margin-right")) ?? Twip.Zero,
                measure.Twips((string?)graphic?.Attribute(Fo + "margin-bottom")) ?? Twip.Zero),
            InBackground = (string?)graphic?.Attribute(Style + "run-through") == "background",
            WrapContour = (string?)graphic?.Attribute(Style + "wrap-contour") == "true",
            WrapFirstParagraph = (string?)graphic?.Attribute(Style + "number-wrapped-paragraphs") == "1",
            FlipHorizontal = contours.Horizontal((string?)graphic?.Attribute(Style + "mirror")),
            FlipVertical = contours.Vertical((string?)graphic?.Attribute(Style + "mirror")),
            Contour = contours.Read(frame),
            Hyperlink = element.Name == Draw + "a" && (string?)element.Attribute(Xlink + "href") is { Length: > 0 } link ? link : null,
            StyleName = (string?)frame.Attribute(Pt + "implied-style") == "true" ? null : style is null
                ? ((string?)frame.Attribute(Draw + "style-name") is { } named ? context.StyleNames.Resolve(named) : null)
                : (string?)style.Attribute(Style + "parent-style-name") is { } parent ? context.StyleNames.Resolve(parent) : null,
        };
        if (item.FlipHorizontal && Math.Abs(item.RotationDegrees - 180) < 0.01)
        {
            item.FlipHorizontal = false;
            item.FlipVertical = !item.FlipVertical;
            item.RotationDegrees = 0;
        }
        context.Document.Objects.Add(item);
        return anchor is CharacterAnchor or AsCharacterAnchor ? id : null;
    }

    private Twip Length(XElement frame, string name) => measure.Twips((string?)frame.Attribute(Svg + name)) ?? Twip.Zero;

    private AnchoredContent? Content(XElement frame, DocumentSize size, OdtReadContext context, OdtBlockReader blocks)
    {
        if (frame.Element(Draw + "text-box") is { } box)
            return new TextBoxContent(new BlockContainer(blocks.Read(box, context)))
            {
                AutoGrowHeight = box.Attribute(Fo + "min-height") is not null,
                NextFrameName = (string?)box.Attribute(Draw + "chain-next-name") is { Length: > 0 } next ? next : null,
            };

        var title = (string?)frame.Element(Svg + "title");
        var description = (string?)frame.Element(Svg + "desc");

        if (frame.Element(Draw + "object-ole") is { } ole && (string?)ole.Attribute(Xlink + "href") is { Length: > 0 } olePath
            && context.ReadBytes(olePath.TrimStart('.', '/')) is { } bytes)
        {
            return new EmbeddedContent(new PackagedObject
            {
                Id = (string?)ole.Attribute(Pt + "object-id") ?? context.Document.NextId("ole"),
                FileName = (string?)ole.Attribute(Pt + "file-name") ?? Path.GetFileName(olePath),
                MediaType = (string?)ole.Attribute(Draw + "mime-type") ?? "application/octet-stream",
                Data = bytes,
                Replacement = (string?)frame.Element(Draw + "image")?.Attribute(Xlink + "href") is { Length: > 0 } picture ? context.ReadBytes(picture.TrimStart('.', '/')) : null,
                Packaged = (string?)ole.Attribute(Pt + "packaged") == "media" ? PackagedObjectKind.Media : PackagedObjectKind.File,
                Title = title,
                Description = description,
            });
        }

        if (signatures.Read(frame, (string?)frame.Element(Draw + "image")?.Attribute(Pt + "object-id") ?? context.Document.NextId("sig")) is { } signatureLine)
            return new EmbeddedContent(signatureLine);

        if (frame.Element(Draw + "image") is { } unstored && (string?)unstored.Attribute(Xlink + "href") is not { Length: > 0 }
            && unstored.Element(Office + "binary-data") is null && frame.Element(Draw + "object") is null && frame.Element(Draw + "object-ole") is null)
            context.Losses.Add(OdtReadLosses.MissingPictures);

        if (frame.Element(Draw + "image") is { } image && (string?)image.Attribute(Xlink + "href") is { Length: > 0 } href)
        {
            var external = new LinkedFiles().IsExternal(href);
            var data = external ? null : context.ReadBytes(href.TrimStart('.', '/'));
            if (!external && data is null)
            {
                context.Losses.Add(OdtReadLosses.MissingPictures);
                return null;
            }
            var natural = measure.Integer((string?)image.Attribute(Pt + "natural-width")) is { } w && measure.Integer((string?)image.Attribute(Pt + "natural-height")) is { } h
                ? new DocumentSize(new Twip(w), new Twip(h))
                : (DocumentSize?)null;
            if (!external && drawings.Vector(image, data!, title, description, context) is { } vector) return new EmbeddedContent(vector);
            var raster = new RasterImageObject
            {
                Id = (string?)image.Attribute(Pt + "object-id") ?? context.Document.NextId("img"),
                Data = data ?? [],
                MediaType = (string?)image.Attribute(Draw + "mime-type") is { Length: > 0 } mime && mime.StartsWith("image/", StringComparison.Ordinal) ? mime : images.MediaTypeOf(href),
                PixelWidth = measure.Integer((string?)image.Attribute(Pt + "pixel-width")) ?? 0,
                PixelHeight = measure.Integer((string?)image.Attribute(Pt + "pixel-height")) ?? 0,
                LinkPath = external ? new LinkedFiles().ToPath(new LinkedFiles().FromPackageHref(href)) : null,
                Name = (string?)image.Attribute(Pt + "object-name"),
                Title = title,
                Description = description,
            };
            if (natural is { } n) raster = raster with { NaturalSize = n };
            return new EmbeddedContent(raster);
        }

        if (frame.Element(Draw + "object") is not null || frame.Element(Draw + "object-ole") is not null) context.Losses.Add(OdtReadLosses.UnreadObjects);
        return null;
    }
}

internal sealed class OdtTableReader(OdtMeasure measure, OdtStyleNames names, OdtVocabulary vocabulary, OdtBorders borders, OdtTableFormulas formulas, OdtCellNumbers numbers, ITableGridRepair repair, OdtCellDirections directions) : OdtXml
{
    public TableBlock Read(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var id = context.Id(element, "tbl");
        var columns = new List<Twip>();
        var relative = new List<double>();
        foreach (var column in element.Elements(Table + "table-column").Concat(element.Elements(Table + "table-columns").Elements(Table + "table-column"))
            .Concat(element.Elements(Table + "table-header-columns").Elements(Table + "table-column")))
        {
            var repeat = Math.Clamp(measure.Integer((string?)column.Attribute(Table + "number-columns-repeated")) ?? 1, 1, 1024);
            var properties = context.Automatic.Find("table-column", (string?)column.Attribute(Table + "style-name"))?.Element(Style + "table-column-properties");
            var width = measure.Twips((string?)properties?.Attribute(Style + "column-width"));
            var share = double.TryParse(((string?)properties?.Attribute(Style + "rel-column-width"))?.TrimEnd('*'), NumberStyles.Float, CultureInfo.InvariantCulture, out var rel) ? rel : 0;
            for (var i = 0; i < repeat && columns.Count < 1024; i++)
            {
                columns.Add(width ?? Twip.Zero);
                relative.Add(share);
            }
        }

        var header = new List<TableRow>();
        var body = new List<TableRow>();
        Rows(element, context, blocks, header, body, inHeader: false);

        var count = Math.Max(columns.Count, header.Concat(body).Select(row => row.Cells.Count).DefaultIfEmpty(0).Max());
        var tableStyle = context.Automatic.Find("table", (string?)element.Attribute(Table + "style-name"));
        var tableWidth = measure.Twips((string?)tableStyle?.Element(Style + "table-properties")?.Attribute(Style + "width"));
        while (columns.Count < count) columns.Add(Twip.Zero);
        if (columns.Count > 0 && columns.All(c => c.Value == 0))
        {
            var available = (tableWidth ?? TextWidth(context)).Value;
            var total = relative.Count == columns.Count ? relative.Sum() : 0;
            for (var i = 0; i < columns.Count; i++)
                columns[i] = new Twip(total > 0 ? (int)Math.Round(available * relative[i] / total) : available / columns.Count);
        }
        var styleName = tableStyle is null
            ? (string?)element.Attribute(Table + "style-name") is { } named ? context.StyleNames.Resolve(named) : null
            : (string?)tableStyle.Attribute(Style + "parent-style-name") is { } parent ? context.StyleNames.Resolve(parent) : null;

        var table = new TableBlock(id, header.Concat(body), columns)
        {
            Name = (string?)element.Attribute(Pt + "unnamed") == "true" ? null : (string?)element.Attribute(Table + "name"),
            StyleName = styleName,
            HeaderRows = header.Count,
            RepeatHeaderRows = (string?)element.Attribute(Pt + "repeat-header") != "false",
            Direct = Padding(element, context),
            PageStyleName = (string?)tableStyle?.Attribute(Style + "master-page-name") is { Length: > 0 } master ? context.PageNames.Resolve(master) : null,
            PageBreakBefore = (string?)tableStyle?.Element(Style + "table-properties")?.Attribute(Fo + "break-before") == "page",
            Alignment = Alignment(tableStyle?.Element(Style + "table-properties")),
            Indent = Indent(tableStyle?.Element(Style + "table-properties")),
        };
        Unpadded(table);
        repair.Repair(context.Document, table);
        return table;
    }

    private TableAlignment Alignment(XElement? properties) => (string?)properties?.Attribute(Table + "align") switch
    {
        "center" => TableAlignment.Center,
        "right" => TableAlignment.Right,
        _ => TableAlignment.Left,
    };

    private Twip Indent(XElement? properties)
    {
        if (Alignment(properties) != TableAlignment.Left) return Twip.Zero;
        return measure.Twips((string?)properties?.Attribute(Fo + "margin-left")) ?? Twip.Zero;
    }

    private TableProperties Padding(XElement table, OdtReadContext context)
    {
        var first = OwnRows(table).SelectMany(row => row.Elements(Table + "table-cell")).FirstOrDefault(cell => !Inherited(cell).Contains(OdtTableWriter.OwnPadding));
        if (first is null) return TableProperties.None;
        if (Inherited(first).Contains("padding")) return TableProperties.None;
        var properties = context.Automatic.Find("table-cell", (string?)first.Attribute(Table + "style-name"))?.Element(Style + "table-cell-properties");
        var all = measure.Points((string?)properties?.Attribute(Fo + "padding"));
        return new TableProperties
        {
            CellPadding = measure.Points((string?)properties?.Attribute(Fo + "padding-left")) ?? all ?? 0,
            VerticalCellPadding = measure.Points((string?)properties?.Attribute(Fo + "padding-top")) ?? all ?? 0,
        };
    }

    private string[] Inherited(XElement cell) =>
        ((string?)cell.Attribute(Pt + "from-table-style") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private CellPadding? CellPadding(XElement? properties)
    {
        if (properties is null) return null;
        var all = measure.Points((string?)properties.Attribute(Fo + "padding"));
        var top = measure.Points((string?)properties.Attribute(Fo + "padding-top")) ?? all;
        var bottom = measure.Points((string?)properties.Attribute(Fo + "padding-bottom")) ?? all;
        var left = measure.Points((string?)properties.Attribute(Fo + "padding-left")) ?? all;
        var right = measure.Points((string?)properties.Attribute(Fo + "padding-right")) ?? all;
        if (top is null && bottom is null && left is null && right is null) return null;
        return new CellPadding(Math.Max(0, top ?? 0), Math.Max(0, bottom ?? 0), Math.Max(0, left ?? 0), Math.Max(0, right ?? 0));
    }

    private void Unpadded(TableBlock table)
    {
        if (table.Direct.CellPadding is not { } side || table.Direct.VerticalCellPadding is not { } vertical) return;
        var shared = new CellPadding(vertical, vertical, side, side);
        foreach (var cell in table.Rows.SelectMany(row => row.Cells))
            if (cell.Padding is { } own && Same(own, shared)) cell.Padding = null;
    }

    private bool Same(CellPadding one, CellPadding other) =>
        Math.Abs(one.Top - other.Top) < 0.01 && Math.Abs(one.Bottom - other.Bottom) < 0.01 && Math.Abs(one.Left - other.Left) < 0.01 && Math.Abs(one.Right - other.Right) < 0.01;

    private IEnumerable<XElement> OwnRows(XElement container)
    {
        foreach (var child in container.Elements())
        {
            if (child.Name == Table + "table-row") yield return child;
            else if (child.Name == Table + "table-header-rows" || child.Name == Table + "table-rows" || child.Name == Table + "table-row-group")
                foreach (var row in OwnRows(child)) yield return row;
        }
    }

    private Twip TextWidth(OdtReadContext context)
    {
        var page = context.Document.Styles.Page.Find(context.Document.InitialPageStyle) ?? context.Document.Styles.Page.Find(PulseOffice.Documents.Styles.DefaultStyleNames.Standard);
        return page is null ? Twip.FromCentimeters(17) : page.Width - page.Margins.Left - page.Margins.Right;
    }

    private void Rows(XElement container, OdtReadContext context, OdtBlockReader blocks, List<TableRow> header, List<TableRow> body, bool inHeader)
    {
        foreach (var child in container.Elements())
        {
            if (child.Name == Table + "table-header-rows") Rows(child, context, blocks, header, body, inHeader: true);
            else if (child.Name == Table + "table-rows" || child.Name == Table + "table-row-group") Rows(child, context, blocks, header, body, inHeader);
            else if (child.Name == Table + "table-row")
            {
                var repeat = Math.Clamp(measure.Integer((string?)child.Attribute(Table + "number-rows-repeated")) ?? 1, 1, 1024);
                var rows = inHeader ? header : body;
                var first = Row(child, context, blocks);
                rows.Add(first);
                for (var i = 1; i < repeat && context.TakeRepetition(Math.Max(1, first.Cells.Count)); i++) rows.Add(Row(child, context, blocks));
            }
        }
    }

    private TableRow Row(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var cells = new List<TableCell>();
        foreach (var cell in element.Elements())
        {
            if (cell.Name != Table + "table-cell" && cell.Name != Table + "covered-table-cell") continue;
            var repeat = Math.Clamp(measure.Integer((string?)cell.Attribute(Table + "number-columns-repeated")) ?? 1, 1, 1024);
            for (var i = 0; i < repeat && cells.Count < 1024 && (i == 0 || context.TakeRepetition(1)); i++) cells.Add(Cell(cell, context, blocks));
        }

        var properties = context.Automatic.Find("table-row", (string?)element.Attribute(Table + "style-name"))?.Element(Style + "table-row-properties");
        var exact = measure.Twips((string?)properties?.Attribute(Style + "row-height"));
        var minimum = measure.Twips((string?)properties?.Attribute(Style + "min-row-height"));
        return new TableRow(cells)
        {
            Height = exact ?? minimum,
            MinimumHeight = exact is null,
            AllowBreakAcrossPages = (string?)properties?.Attribute(Fo + "keep-together") != "always",
            ChangeId = (string?)element.Attribute(Pt + "change-id") is { } change && context.Document.Changes.Find(change) is Insertion or Deletion ? change : null,
        };
    }

    private TableCell Cell(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var cell = new TableCell(new BlockContainer(blocks.Read(element, context)));
        if (element.Name == Table + "covered-table-cell")
        {
            cell.Covered = true;
            return cell;
        }

        cell.ColumnSpan = Math.Clamp(measure.Integer((string?)element.Attribute(Table + "number-columns-spanned")) ?? 1, 1, 1024);
        cell.RowSpan = Math.Clamp(measure.Integer((string?)element.Attribute(Table + "number-rows-spanned")) ?? 1, 1, 1024);
        var styleName = (string?)element.Attribute(Table + "style-name");
        var style = context.Automatic.Find("table-cell", styleName);
        var properties = style?.Element(Style + "table-cell-properties");
        var inherited = Inherited(element);
        var ours = element.Attribute(Pt + "from-table-style") is not null;
        if (!ours || inherited.Contains(OdtTableWriter.OwnPadding)) cell.Padding = CellPadding(properties);
        cell.TextDirection = directions.Read(properties);
        if (!inherited.Contains("background")) cell.Background = measure.Color((string?)properties?.Attribute(Fo + "background-color"));
        if (!inherited.Contains("borders") && properties is not null && borders.Read(properties) is { } lines)
            cell.Borders = new PulseOffice.Text.Rich.BoxBorders { Top = lines.Top, Bottom = lines.Bottom, Left = lines.Left, Right = lines.Right };
        if (inherited.Contains("ink")) Uninked(cell, measure.Color((string?)element.Attribute(Pt + "header-ink")), (string?)element.Attribute(Pt + "header-bold") == "true");
        cell.Formula = (string?)element.Attribute(Pt + "formula")
            ?? formulas.Read((string?)element.Attribute(Table + "formula"))
            ?? formulas.Read((string?)element.Descendants(Text + "table-formula").FirstOrDefault()?.Attribute(Text + "formula"));
        cell.VerticalAlign = vocabulary.VerticalAlign.Value((string?)properties?.Attribute(Style + "vertical-align"));
        cell.Protected = (string?)element.Attribute(Table + "protected") == "true";
        cell.NumberFormat = numbers.Read(context.Automatic.Find(OdtAutomaticStyles.DataFamily, (string?)style?.Attribute(Style + "data-style-name")));
        cell.StyleName = style is null
            ? styleName is null ? null : names.Decode(styleName)
            : (string?)style.Attribute(Style + "parent-style-name") is { } parent ? context.StyleNames.Resolve(parent) : null;
        return cell;
    }

    private void Uninked(TableCell cell, uint? ink, bool bold)
    {
        foreach (var paragraph in cell.Content.Blocks.OfType<ParagraphBlock>())
        {
            if (paragraph.Content.Runs.Count == 0) continue;
            paragraph.Content = paragraph.Content.WithRuns([.. paragraph.Content.Runs.Select(run => run with
            {
                Color = ink is not null && run.Color == ink ? null : run.Color,
                Bold = !bold && run.Bold,
            })]);
        }
    }
}

internal sealed class OdtSectionReader(OdtColumnsCodec columns, OdtIndexCodec indexes) : OdtXml
{
    public bool HandlesIndex(XName name) => indexes.Handles(name);

    public SectionBlock ReadIndex(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var id = context.Id(element, "sec");
        var properties = context.Automatic.Find("section", (string?)element.Attribute(Text + "style-name"))?.Element(Style + "section-properties");
        var content = new List<Block>();
        foreach (var child in indexes.BodyElements(element)) blocks.Read(child, context, null, content);
        if (content.Count == 0) content.Add(new ParagraphBlock(context.Document.NextId("p"), string.Empty, DefaultStyleNames.Standard));
        return new SectionBlock(id, (string?)element.Attribute(Text + "name") ?? id, new BlockContainer(content))
        {
            Columns = columns.Read(properties),
            Protected = (string?)element.Attribute(Text + "protected") == "true",
            Index = indexes.Read(element, context.StyleNames.Resolve),
        };
    }

    private byte[]? Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return Convert.FromBase64String(text.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public SectionBlock Read(XElement element, OdtReadContext context, OdtBlockReader blocks)
    {
        var id = context.Id(element, "sec");
        var properties = context.Automatic.Find("section", (string?)element.Attribute(Text + "style-name"))?.Element(Style + "section-properties");
        var display = (string?)element.Attribute(Text + "display");
        var content = new BlockContainer(blocks.Read(element, context));
        return new SectionBlock(id, (string?)element.Attribute(Text + "name") ?? id, content)
        {
            Columns = columns.Read(properties),
            Protected = (string?)element.Attribute(Text + "protected") == "true",
            ProtectionKey = Key((string?)element.Attribute(Text + "protection-key")),
            Hidden = display == "none",
            Condition = (string?)element.Attribute(Text + "condition"),
            LinkedFile = (string?)element.Element(Text + "section-source")?.Attribute(Xlink + "href") is { } source ? new LinkedFiles().FromPackageHref(source) : null,
        };
    }
}
