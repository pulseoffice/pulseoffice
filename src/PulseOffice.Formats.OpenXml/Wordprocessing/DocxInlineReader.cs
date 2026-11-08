using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxInlineContent(IReadOnlyList<TextRun> Runs, IReadOnlyList<InlineMark> Marks);

public sealed class DocxInlineReader(IDirectFormatting formatting, DocxCharacterCodec characters, DocxFieldInstructions fields, DocxDrawingCodec drawings, DocxVmlReader vml, DocxObjectCodecs objects, DocxContentControls controls, DocxLegacyFormFields forms, DocxParagraphMarkChanges paragraphMarks, DocxFieldRangeReader ranges)
{
    private sealed class OpenFormat(string id, string key)
    {
        public string Id { get; } = id;

        public string Key { get; } = key;

        public string Author { get; init; } = string.Empty;

        public DateTimeOffset Date { get; init; }

        public List<TextRun> Previous { get; } = [];
    }

    private sealed class NestedCapture(int depth)
    {
        public int Depth { get; } = depth;

        public bool InInstruction { get; set; } = true;

        public bool Deeper { get; set; }

        public System.Text.StringBuilder Instruction { get; } = new();

        public System.Text.StringBuilder Result { get; } = new();
    }

    private sealed class Collector(DocxReadContext context, string paragraphId, DocxBlockSource blocks)
    {
        public List<DocxNestedField> Nested { get; } = [];

        public NestedCapture? Capture { get; set; }

        public bool NestedLossy { get; set; }

        public bool NestedInResult { get; set; }

        public List<TextRun> Runs { get; } = [];

        public List<InlineMark> Marks { get; } = [];

        public int Length { get; private set; }

        public DocxReadContext Context { get; } = context;

        public string ParagraphId { get; } = paragraphId;

        public DocxBlockSource Blocks { get; } = blocks;

        public string? Hyperlink { get; set; }

        public string? Ruby { get; set; }

        public DocxField? Field { get; set; }

        public bool InInstruction { get; set; }

        public System.Text.StringBuilder Instruction { get; } = new();

        public int FieldDepth { get; set; }

        public bool FieldLocked { get; set; }

        public bool FieldEmitted { get; set; }

        public bool FieldLinks { get; set; }

        public string? LinkBeforeField { get; set; }

        public (int Length, int Marks) LinkReached { get; set; }

        public Insertion? LastInsertion { get; set; }

        public Insertion? Within { get; set; }

        public OpenFormat? Format { get; set; }

        public W.FormFieldData? FormData { get; set; }

        public string? NativeData { get; set; }

        public string? OpenForm { get; set; }

        public string? FormShown { get; set; }

        public int FormStart { get; set; }

        public void Add(TextRun run)
        {
            if (run.Text.Length == 0 && run.Field == TextFieldKind.None) return;
            Runs.Add(run);
            Length += run.Text.Length;
        }
    }

    public DocxInlineContent Read(W.Paragraph paragraph, DocxReadContext context, string paragraphId, DocxBlockSource blocks)
    {
        var collector = new Collector(context, paragraphId, blocks);
        if (context.OpenInsertion is { } open)
        {
            collector.Marks.Add(new ChangeMark(0, open.Id, MarkEdge.End));
            collector.LastInsertion = open;
        }
        Walk(paragraph.ChildElements, collector);
        collector.Marks.AddRange(ranges.ParagraphEnd(context));
        CloseFormat(collector);
        UnclosedField(collector);
        context.OpenInsertion = paragraphMarks.Insertion(paragraph) is { } mark ? Continue(mark, collector) : null;
        var runs = Merge(collector.Runs);
        if (runs.Count == 0 && paragraph.ParagraphProperties?.ParagraphMarkRunProperties is { } markProperties && characters.Read(markProperties) is { FontSize: not null } sized)
            runs = [formatting.Set(new TextRun(string.Empty), sized)];
        return new DocxInlineContent(runs, collector.Marks);
    }

    private IReadOnlyDictionary<Type, Action<OpenXmlElement, Collector>>? elementReaders;

    private IReadOnlyDictionary<Type, Action<OpenXmlElement, Collector>> ElementReaders => elementReaders ??= new Dictionary<Type, Action<OpenXmlElement, Collector>>
    {
        [typeof(W.Run)] = (element, collector) => ReadRun((W.Run)element, collector),
        [typeof(W.Hyperlink)] = (element, collector) => ReadHyperlink((W.Hyperlink)element, collector),
        [typeof(W.SimpleField)] = (element, collector) => ReadSimpleField((W.SimpleField)element, collector),
        [typeof(W.BookmarkStart)] = (element, collector) => ReadBookmarkStart((W.BookmarkStart)element, collector),
        [typeof(W.BookmarkEnd)] = (element, collector) => ReadBookmarkEnd((W.BookmarkEnd)element, collector),
        [typeof(W.CommentRangeStart)] = (element, collector) => ReadCommentStart((W.CommentRangeStart)element, collector),
        [typeof(W.CommentRangeEnd)] = (element, collector) => ReadCommentEnd((W.CommentRangeEnd)element, collector),
        [typeof(W.InsertedRun)] = (element, collector) => Inserted(((W.InsertedRun)element).Author?.Value, ((W.InsertedRun)element).Date?.Value, element.ChildElements, collector),
        [typeof(W.MoveToRun)] = (element, collector) => Inserted(((W.MoveToRun)element).Author?.Value, ((W.MoveToRun)element).Date?.Value, element.ChildElements, collector),
        [typeof(W.DeletedRun)] = (element, collector) => Deleted(((W.DeletedRun)element).Author?.Value, ((W.DeletedRun)element).Date?.Value, element.ChildElements, collector),
        [typeof(W.MoveFromRun)] = (element, collector) => Deleted(((W.MoveFromRun)element).Author?.Value, ((W.MoveFromRun)element).Date?.Value, element.ChildElements, collector),
        [typeof(DocumentFormat.OpenXml.Math.OfficeMath)] = (element, collector) => Object(objects.Formulas.Read((DocumentFormat.OpenXml.Math.OfficeMath)element, collector.Context, collector.ParagraphId), collector),
        [typeof(DocumentFormat.OpenXml.Math.Paragraph)] = (element, collector) => ReadMathParagraph((DocumentFormat.OpenXml.Math.Paragraph)element, collector),
        [typeof(W.SdtRun)] = (element, collector) => ReadSdtRun((W.SdtRun)element, collector),
        [typeof(W.CustomXmlRun)] = (element, collector) => Walk(element.ChildElements, collector),
    };

    private void Walk(IEnumerable<OpenXmlElement> elements, Collector collector)
    {
        foreach (var element in elements)
            if (ElementReaders.TryGetValue(element.GetType(), out var read)) read(element, collector);
    }

    private void ReadHyperlink(W.Hyperlink link, Collector collector)
    {
        var previous = collector.Hyperlink;
        collector.Hyperlink = link.Id?.Value is { } id ? collector.Context.Link(id) : link.Anchor?.Value is { } anchor ? "#" + anchor : null;
        var reached = (collector.Length, collector.Marks.Count);
        Walk(link.ChildElements, collector);
        if (collector.Hyperlink is not null && reached == (collector.Length, collector.Marks.Count)) collector.Context.Losses.Add("DOCX170");
        collector.Hyperlink = previous;
    }

    private void ReadSimpleField(W.SimpleField simple, Collector collector)
    {
        if (Entry(simple.Instruction?.Value, collector) is { } indexEntry)
        {
            collector.Marks.Add(indexEntry);
            return;
        }
        if (fields.HyperlinkTarget(simple.Instruction?.Value) is { } simpleTarget)
        {
            var linkBefore = collector.Hyperlink;
            collector.Hyperlink = simpleTarget;
            Walk(simple.ChildElements, collector);
            collector.Hyperlink = linkBefore;
            return;
        }
        var outer = collector.Field;
        collector.Field = Locked(fields.WithNativeData(fields.Parse(simple.Instruction?.Value), simple.GetFirstChild<W.FieldData>()?.Text), simple.FieldLock?.Value == true);
        collector.FieldEmitted = false;
        Walk(simple.ChildElements, collector);
        EmptyField(collector);
        collector.Field = outer;
    }

    private void ReadBookmarkStart(W.BookmarkStart start, Collector collector)
    {
        if (start.Name?.Value is { } carrier && carrier.StartsWith(DocxBlockProperties.BookmarkPrefix, StringComparison.Ordinal))
        {
            if (start.Id?.Value is { } carrierId) collector.Context.CarrierIds[carrierId] = carrier;
            collector.Context.CarrierStarts.TryAdd(carrier, collector.ParagraphId);
            return;
        }
        if (start.Name?.Value is not { } name || !KeepsBookmark(name, collector.Context)) return;
        collector.Context.Bookmarks[start.Id?.Value ?? name] = name;
        collector.Marks.Add(new BookmarkMark(collector.Length, name, MarkEdge.Start));
    }

    private void ReadBookmarkEnd(W.BookmarkEnd end, Collector collector)
    {
        if (end.Id?.Value is { } closedId && collector.Context.CarrierIds.TryGetValue(closedId, out var carrier))
        {
            collector.Context.CarrierEnds[carrier] = collector.ParagraphId;
            return;
        }
        if (end.Id?.Value is not { } endId || !collector.Context.Bookmarks.TryGetValue(endId, out var bookmark)) return;
        var opened = collector.Marks.FindLastIndex(mark => mark is BookmarkMark { Edge: MarkEdge.Start } open && open.Name == bookmark && open.Offset == collector.Length);
        if (opened >= 0) collector.Marks[opened] = new BookmarkMark(collector.Length, bookmark, MarkEdge.Point);
        else collector.Marks.Add(new BookmarkMark(collector.Length, bookmark, MarkEdge.End));
    }

    private void ReadCommentStart(W.CommentRangeStart commentStart, Collector collector)
    {
        if (IsReply(commentStart, collector.Context) || commentStart.Id?.Value is not { } startId) return;
        collector.Context.AnchoredComments.Add(startId);
        collector.Marks.Add(new CommentMark(collector.Length, collector.Context.Comments.GetValueOrDefault(startId) ?? "comment" + startId, MarkEdge.Start));
    }

    private void ReadCommentEnd(W.CommentRangeEnd commentEnd, Collector collector)
    {
        if (IsReply(commentEnd, collector.Context) || commentEnd.Id?.Value is not { } endCommentId) return;
        collector.Context.AnchoredComments.Add(endCommentId);
        var commentModel = collector.Context.Comments.GetValueOrDefault(endCommentId) ?? "comment" + endCommentId;
        var openComment = collector.Marks.FindLastIndex(mark => mark is CommentMark { Edge: MarkEdge.Start } open && open.CommentId == commentModel && open.Offset == collector.Length);
        if (openComment >= 0) collector.Marks[openComment] = new CommentMark(collector.Length, commentModel, MarkEdge.Point);
        else collector.Marks.Add(new CommentMark(collector.Length, commentModel, MarkEdge.End));
    }

    private void ReadMathParagraph(DocumentFormat.OpenXml.Math.Paragraph mathParagraph, Collector collector)
    {
        foreach (var math in mathParagraph.Elements<DocumentFormat.OpenXml.Math.OfficeMath>())
            Object(objects.Formulas.Read(math, collector.Context, collector.ParagraphId), collector);
    }

    private void ReadSdtRun(W.SdtRun sdt, Collector collector)
    {
        if (objects.Controls.Read(sdt, collector.Context, collector.ParagraphId) is { } controlObject)
        {
            Object(controlObject, collector);
            return;
        }
        if (controls.IsContentControl(sdt.SdtProperties))
        {
            var controlId = collector.Context.Document.NextId("cc");
            var shown = string.Concat(sdt.SdtContentRun?.Descendants<W.Text>().Select(item => item.Text) ?? []);
            collector.Marks.Add(new ContentControlMark(collector.Length, controlId, MarkEdge.Start) { Control = controls.Read(sdt.SdtProperties, shown) });
            Walk(sdt.SdtContentRun?.ChildElements ?? Enumerable.Empty<OpenXmlElement>(), collector);
            collector.Marks.Add(new ContentControlMark(collector.Length, controlId, MarkEdge.End));
            return;
        }
        Walk(sdt.SdtContentRun?.ChildElements ?? Enumerable.Empty<OpenXmlElement>(), collector);
    }

    private DateTimeOffset Date(DateTime? value) =>
        value is { } date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)) : DateTimeOffset.MinValue;

    private void Inserted(string? author, DateTime? date, IEnumerable<OpenXmlElement> children, Collector collector)
    {
        var when = Date(date);
        var change = collector.LastInsertion;
        var closing = change is null ? -1 : collector.Marks.FindLastIndex(mark => mark is ChangeMark { Edge: MarkEdge.End } end && end.ChangeId == change.Id && end.Offset == collector.Length);
        if (change is not null && closing >= 0 && change.Author == (author ?? string.Empty) && change.Date == when)
        {
            collector.Marks.RemoveAt(closing);
        }
        else
        {
            change = new Insertion(collector.Context.Document.NextId("change"), author ?? string.Empty, when);
            collector.Context.Document.Changes.Add(change);
            collector.Marks.Add(new ChangeMark(collector.Length, change.Id, MarkEdge.Start));
        }

        var outer = collector.Within;
        collector.Within = change;
        Walk(children, collector);
        collector.Within = outer;
        collector.Marks.Add(new ChangeMark(collector.Length, change.Id, MarkEdge.End));
        collector.LastInsertion = change;
    }

    private Insertion Continue(W.TrackChangeType mark, Collector collector)
    {
        var author = mark.Author?.Value ?? string.Empty;
        var when = Date(mark.Date?.Value);
        var change = collector.LastInsertion;
        var closing = change is null ? -1 : collector.Marks.FindLastIndex(found => found is ChangeMark { Edge: MarkEdge.End } end && end.ChangeId == change.Id && end.Offset == collector.Length);
        if (change is not null && closing >= 0 && change.Author == author && change.Date == when)
        {
            collector.Marks.RemoveAt(closing);
            return change;
        }
        var created = new Insertion(collector.Context.Document.NextId("change"), author, when);
        collector.Context.Document.Changes.Add(created);
        collector.Marks.Add(new ChangeMark(collector.Length, created.Id, MarkEdge.Start));
        return created;
    }

    private void Deleted(string? author, DateTime? date, IEnumerable<OpenXmlElement> children, Collector collector)
    {
        var runs = new List<TextRun>();
        var marks = new List<InlineMark>();
        var length = 0;
        var state = new DocxDeletedFieldState(fields);
        var removedId = collector.Context.Document.NextId("p");
        var holdsObjects = false;
        foreach (var element in children.SelectMany(child => DeletedPieces(child)))
        {
            if (element is not W.Run run)
            {
                DeletedCommentEdge(element, collector, length, marks);
                continue;
            }
            var text = new System.Text.StringBuilder();
            void Emit()
            {
                if (state.TakePending() is { } empty)
                    runs.Add(state.Mark(new TextRun(state.Shown(empty)) { Field = empty.Kind, FieldArgument = empty.Argument, FieldDetail = empty.Detail, Hidden = empty.Hidden }));
                if (text.Length == 0) return;
                length += text.Length;
                runs.Add(state.Mark(formatting.Set(new TextRun(text.ToString()) { StyleName = collector.Context.Styles.NameOf(characters.StyleId(run.RunProperties)), Hyperlink = collector.Hyperlink },
                    characters.Read(run.RunProperties))));
                text.Clear();
            }
            foreach (var part in run.ChildElements)
            {
                if (state.SkipsText && part is not (W.FieldChar or W.FieldCode or W.DeletedFieldCode)) continue;
                switch (part)
                {
                    case W.FieldChar fieldChar:
                        Emit();
                        state.Char(fieldChar);
                        Emit();
                        break;
                    case W.FieldCode code: state.Code(code.Text); break;
                    case W.DeletedFieldCode code: state.Code(code.Text); break;
                    case W.DeletedText deleted: text.Append(deleted.Text); break;
                    case W.Text plain: text.Append(plain.Text); break;
                    case W.TabChar: text.Append('\t'); break;
                    case W.Break or W.CarriageReturn: text.Append('\n'); break;
                    case W.FootnoteReference { Id.Value: var footnoteId } when collector.Context.Footnotes.TryGetValue(footnoteId.ToString(CultureInfo.InvariantCulture), out var footnote):
                        marks.Add(new NoteMark(length + text.Length, footnote));
                        break;
                    case W.EndnoteReference { Id.Value: var endnoteId } when collector.Context.Endnotes.TryGetValue(endnoteId.ToString(CultureInfo.InvariantCulture), out var endnote):
                        marks.Add(new NoteMark(length + text.Length, endnote));
                        break;
                    case W.CommentReference { Id.Value: { } commentId } when collector.Context.AnchoredComments.Add(commentId)
                        && collector.Context.Comments.TryGetValue(commentId, out var comment):
                        marks.Add(new CommentMark(length + text.Length, comment, MarkEdge.Point));
                        break;
                    case W.Drawing or W.Picture or W.EmbeddedObject or AlternateContent:
                        Emit();
                        holdsObjects |= DeletedObject(part, collector, removedId, length, marks);
                        break;
                }
            }

            Emit();
        }

        if (runs.Count == 0 && marks.Count == 0 && !holdsObjects) return;
        var paragraph = new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns(Merge(runs));
        var removed = new BlockContainer([new ParagraphBlock(removedId, paragraph) { Marks = marks }]);
        var change = new Deletion(collector.Context.Document.NextId("change"), author ?? string.Empty, Date(date), removed) { WithinInsertion = collector.Within?.Id };
        collector.Context.Document.Changes.Add(change);
        collector.Marks.Add(new ChangeMark(collector.Length, change.Id, MarkEdge.Point));
    }

    private IEnumerable<OpenXmlElement> DeletedPieces(OpenXmlElement child) =>
        child is W.Run or W.CommentRangeStart or W.CommentRangeEnd ? [child] : child.Descendants().Where(item => item is W.Run or W.CommentRangeStart or W.CommentRangeEnd);

    private void DeletedCommentEdge(OpenXmlElement element, Collector collector, int offset, List<InlineMark> marks)
    {
        var id = (element as W.CommentRangeStart)?.Id?.Value ?? (element as W.CommentRangeEnd)?.Id?.Value;
        if (id is null || IsReply(element, collector.Context) || !collector.Context.Comments.TryGetValue(id, out var comment)) return;
        collector.Context.AnchoredComments.Add(id);
        if (element is W.CommentRangeStart)
        {
            marks.Add(new CommentMark(offset, comment, MarkEdge.Start));
            return;
        }
        var opened = marks.FindLastIndex(mark => mark is CommentMark { Edge: MarkEdge.Start } open && open.CommentId == comment && open.Offset == offset);
        if (opened >= 0) marks[opened] = new CommentMark(offset, comment, MarkEdge.Point);
        else marks.Add(new CommentMark(offset, comment, MarkEdge.End));
    }

    private bool DeletedObject(OpenXmlElement part, Collector collector, string removedId, int offset, List<InlineMark> marks)
    {
        var item = part switch
        {
            W.Drawing drawing => drawings.Read(drawing, collector.Context, collector.Blocks, removedId),
            W.Picture picture => vml.Read(picture, collector.Context, collector.Blocks, removedId),
            W.EmbeddedObject embedded => objects.Ole.Read(embedded, collector.Context, removedId),
            AlternateContent alternate => AlternateObject(alternate, collector, removedId),
            _ => null,
        };
        if (item is null) return false;
        collector.Context.Document.Objects.Add(item);
        if (item.Anchor is CharacterAnchor or AsCharacterAnchor) marks.Add(new ObjectMark(offset, item.Id));
        return true;
    }

    private AnchoredObject? AlternateObject(AlternateContent alternate, Collector collector, string paragraphId)
    {
        foreach (var choice in alternate.Elements<AlternateContentChoice>())
            if (choice.Descendants<W.Drawing>().FirstOrDefault() is { } drawing
                && drawings.Read(drawing, collector.Context, collector.Blocks, paragraphId) is { } read)
                return read;
        if (alternate.GetFirstChild<AlternateContentFallback>() is not { } fallback) return null;
        if (fallback.Descendants<W.Drawing>().FirstOrDefault() is { } fallbackDrawing) return drawings.Read(fallbackDrawing, collector.Context, collector.Blocks, paragraphId);
        return fallback.Descendants<W.Picture>().FirstOrDefault() is { } picture ? vml.Read(picture, collector.Context, collector.Blocks, paragraphId) : null;
    }

    private static readonly string[] KeptHiddenBookmarkPrefixes = ["_Ref", "__RefHeading", "__DdeLink"];

    private bool KeepsBookmark(string name, DocxReadContext context) =>
        !name.StartsWith('_')
        || name == "_GoBack"
        || KeptHiddenBookmarkPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))
        || context.ReferencedBookmarks.Contains(name);

    private bool IsReply(OpenXmlElement element, DocxReadContext context)
    {
        var id = (element as W.CommentRangeStart)?.Id?.Value ?? (element as W.CommentRangeEnd)?.Id?.Value;
        return id is not null && context.Comments.TryGetValue(id, out var model) && context.Document.Comments.Find(model) is { IsReply: true };
    }

    private void ReadRun(W.Run run, Collector collector)
    {
        if (run.GetFirstChild<W.Ruby>() is { } ruby)
        {
            var outer = collector.Ruby;
            collector.Ruby = string.Concat(ruby.GetFirstChild<W.RubyContent>()?.Descendants<W.Text>().Select(item => item.Text) ?? []) is { Length: > 0 } annotation ? annotation : null;
            foreach (var inner in ruby.GetFirstChild<W.RubyBase>()?.Elements<W.Run>() ?? []) ReadRun(inner, collector);
            collector.Ruby = outer;
            return;
        }
        var buffer = new System.Text.StringBuilder();
        foreach (var child in run.ChildElements)
        {
            if (collector.Context.FieldRanges.Coding is not null && collector.FieldDepth == 0)
            {
                collector.Marks.AddRange(ranges.Code(child, collector.Context, collector.Length));
                continue;
            }
            switch (child)
            {
                case W.FieldChar { FieldCharType.Value: var type } fieldChar:
                    Flush(run, buffer, collector);
                    if (type == W.FieldCharValues.Begin && collector.FieldDepth == 0 && ranges.Begins(fieldChar, collector.Context, collector.Length)) break;
                    if (type == W.FieldCharValues.End && collector.FieldDepth == 0 && ranges.Ends(collector.Context, collector.Length) is { } rangeEnd)
                    {
                        collector.Marks.Add(rangeEnd);
                        break;
                    }
                    if (type == W.FieldCharValues.Begin)
                    {
                        if (collector.FieldDepth >= 1 && collector.InInstruction)
                        {
                            if (collector.Capture is { } deeper) deeper.Deeper = true;
                            else collector.Capture = new NestedCapture(collector.FieldDepth + 1);
                        }
                        else if (collector.FieldDepth >= 1) collector.NestedInResult = true;
                        collector.FieldDepth++;
                        if (collector.FieldDepth == 1)
                        {
                            collector.Nested.Clear();
                            collector.NestedLossy = false;
                            collector.NestedInResult = false;
                            collector.FieldLocked = fieldChar.FieldLock?.Value == true;
                            collector.FormData = fieldChar.FormFieldData;
                            collector.NativeData = fieldChar.GetFirstChild<W.FieldData>()?.Text;
                            collector.InInstruction = true;
                            collector.Instruction.Clear();
                        }
                    }
                    else if (type == W.FieldCharValues.Separate && collector.Capture is { } capturing && collector.FieldDepth == capturing.Depth)
                    {
                        capturing.InInstruction = false;
                    }
                    else if (type == W.FieldCharValues.Separate && collector.FieldDepth == 1)
                    {
                        collector.InInstruction = false;
                        collector.Field = Locked(fields.WithNativeData(fields.Parse(Resolved(collector).Text), collector.NativeData), collector.FieldLocked);
                        collector.FieldEmitted = false;
                        OpenForm(collector);
                        if (fields.HyperlinkTarget(Resolved(collector).Text) is { } target)
                        {
                            collector.LinkBeforeField = collector.Hyperlink;
                            collector.Hyperlink = target;
                            collector.FieldLinks = true;
                            collector.LinkReached = (collector.Length, collector.Marks.Count);
                        }
                    }
                    else if (type == W.FieldCharValues.End)
                    {
                        if (collector.Capture is { } captured && collector.FieldDepth == captured.Depth)
                        {
                            collector.Instruction.Append(fields.Placeholder(collector.Nested.Count));
                            collector.Nested.Add(new DocxNestedField(captured.Instruction.ToString(), captured.Result.ToString()));
                            collector.NestedLossy |= captured.Deeper;
                            collector.Capture = null;
                        }
                        collector.FieldDepth = Math.Max(0, collector.FieldDepth - 1);
                        if (collector.FieldDepth == 0)
                        {
                            var resolved = Resolved(collector);
                            if ((resolved.Lossy || collector.NestedLossy) && !fields.Structural(resolved.Text) || collector.FormData is not null && collector.Nested.Count > 0) collector.Context.Losses.Add("DOCX160");
                            if (collector.NestedInResult && (collector.FormData is not null || !fields.Structural(resolved.Text) && fields.Parse(resolved.Text) is not null)) collector.Context.Losses.Add("DOCX161");
                            if (collector.InInstruction) OpenForm(collector);
                            CloseForm(run, buffer, collector);
                            if (collector.InInstruction && collector.Field is null)
                            {
                                collector.Field = Locked(fields.WithNativeData(fields.Parse(resolved.Text), collector.NativeData), collector.FieldLocked);
                                collector.FieldEmitted = false;
                            }
                            if (Entry(resolved.Text, collector) is { } complexEntry)
                                collector.Marks.Add(complexEntry);
                            EmptyField(collector);
                            collector.Instruction.Clear();
                            collector.InInstruction = false;
                            collector.Field = null;
                            if (collector.FieldLinks)
                            {
                                if (collector.LinkReached == (collector.Length, collector.Marks.Count)) collector.Context.Losses.Add("DOCX170");
                                collector.Hyperlink = collector.LinkBeforeField;
                                collector.FieldLinks = false;
                            }
                        }
                    }

                    break;
                case W.CommentReference reference when reference.Id?.Value is { } referenceId && collector.Context.AnchoredComments.Add(referenceId)
                    && collector.Context.Comments.GetValueOrDefault(referenceId) is { } referenced && collector.Context.Document.Comments.Find(referenced) is { IsReply: false }:
                    Flush(run, buffer, collector);
                    collector.Marks.Add(new CommentMark(collector.Length, referenced, MarkEdge.Point));
                    break;
                case W.FieldCode code:
                    if (collector.InInstruction && collector.FieldDepth == 1) collector.Instruction.Append(code.Text);
                    else if (collector.Capture is { InInstruction: true } inner && collector.FieldDepth == inner.Depth) inner.Instruction.Append(code.Text);
                    break;
                case W.Text text when collector.Capture is { InInstruction: false } nestedResult:
                    nestedResult.Result.Append(text.Text);
                    break;
                case W.Text text when !collector.InInstruction:
                    buffer.Append(text.Text);
                    break;
                case W.TabChar or W.PositionalTab when !collector.InInstruction:
                    buffer.Append('\t');
                    break;
                case W.Break { Type: null } or W.CarriageReturn when !collector.InInstruction:
                    buffer.Append('\n');
                    break;
                case W.NoBreakHyphen when !collector.InInstruction:
                    buffer.Append('‑');
                    break;
                case W.SoftHyphen when !collector.InInstruction:
                    buffer.Append('­');
                    break;
                case W.SymbolChar symbol when !collector.InInstruction && symbol.Char?.Value is { } hex
                                              && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
                    if (symbol.Font?.Value is { Length: > 0 } symbolFont)
                    {
                        Flush(run, buffer, collector);
                        Symbol(buffer, code < 0x100 ? code + 0xF000 : code);
                        Flush(run, buffer, collector, symbolFont);
                    }
                    else Symbol(buffer, code is >= 0xF000 and <= 0xF0FF ? code - 0xF000 : code);
                    break;
                case W.FootnoteReference footnote when footnote.Id?.Value is { } footnoteId:
                    Flush(run, buffer, collector);
                    Note(run, collector.Context.Footnotes, footnoteId, footnote.CustomMarkFollows?.Value == true, collector);
                    return;
                case W.EndnoteReference endnote when endnote.Id?.Value is { } endnoteId:
                    Flush(run, buffer, collector);
                    Note(run, collector.Context.Endnotes, endnoteId, endnote.CustomMarkFollows?.Value == true, collector);
                    return;
                case W.Drawing drawing:
                    Flush(run, buffer, collector);
                    Object(drawings.Read(drawing, collector.Context, collector.Blocks, collector.ParagraphId), collector);
                    break;
                case AlternateContent alternate:
                    Flush(run, buffer, collector);
                    Alternate(alternate, collector);
                    break;
                case W.Picture picture:
                    Flush(run, buffer, collector);
                    Object(vml.Read(picture, collector.Context, collector.Blocks, collector.ParagraphId), collector);
                    break;
                case W.EmbeddedObject embedded:
                    Flush(run, buffer, collector);
                    var ole = objects.Ole.Read(embedded, collector.Context, collector.ParagraphId);
                    if (ole is null) collector.Context.Losses.Add("DOCX102");
                    Object(ole, collector);
                    break;
            }
        }

        Flush(run, buffer, collector);
    }

    private void Symbol(System.Text.StringBuilder buffer, int code)
    {
        if (System.Text.Rune.IsValid(code) && code != 0) buffer.Append(char.ConvertFromUtf32(code));
    }

    private void Note(W.Run run, Dictionary<string, string> notes, long id, bool custom, Collector collector)
    {
        if (!notes.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out var noteId))
        {
            collector.Context.Losses.Add("DOCX182");
            return;
        }
        collector.Marks.Add(new NoteMark(collector.Length, noteId));
        if (custom && collector.Context.Document.Notes.Find(noteId) is { } note)
            note.Citation = string.Concat(run.Elements<W.Text>().Select(text => text.Text));
    }

    private IndexEntryMark? Entry(string? instruction, Collector collector)
    {
        if (EntryPath(instruction) is not { } path) return null;
        var levels = Levels(path);
        if (levels.Count == 0) return null;
        var leaf = levels.Count > 3 ? string.Join(", ", levels.Skip(2)) : levels[^1];
        var entry = new IndexEntryMark(collector.Length, leaf, MarkEdge.Point)
        {
            Key1 = levels.Count > 1 ? levels[0] : null,
            Key2 = levels.Count > 2 ? levels[1] : null,
        };
        if (System.Text.RegularExpressions.Regex.Match(instruction!, @"(?<=\s)\\r\s+""?([^\s""\\]+)") is { Success: true } range)
            collector.Context.IndexRanges.Add((entry, range.Groups[1].Value));
        return entry;
    }

    private List<string> Levels(string path)
    {
        var levels = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var index = 0; index < path.Length; index++)
        {
            if (path[index] == '\\' && index + 1 < path.Length && path[index + 1] == ':')
            {
                current.Append(':');
                index++;
            }
            else if (path[index] == ':')
            {
                levels.Add(current.ToString().Trim());
                current.Clear();
            }
            else current.Append(path[index]);
        }
        levels.Add(current.ToString().Trim());
        return [.. levels.Where(level => level.Length > 0)];
    }

    private string? EntryPath(string? instruction)
    {
        var text = instruction?.Trim();
        if (text is null || !text.StartsWith("XE ", StringComparison.OrdinalIgnoreCase)) return null;
        var rest = text[3..].Trim();
        if (rest.StartsWith('"'))
        {
            var end = rest.IndexOf('"', 1);
            while (end > 0 && rest[end - 1] == '\\') end = rest.IndexOf('"', end + 1);
            rest = end > 0 ? rest[1..end] : rest[1..];
        }

        return rest.Replace("\\\"", "\"", StringComparison.Ordinal);
    }

    private void Alternate(AlternateContent alternate, Collector collector) =>
        Object(AlternateObject(alternate, collector, collector.ParagraphId), collector);

    private void Object(AnchoredObject? item, Collector collector)
    {
        if (item is null) return;
        collector.Context.Document.Objects.Add(item);
        if (item.Anchor is CharacterAnchor or AsCharacterAnchor) collector.Marks.Add(new ObjectMark(collector.Length, item.Id));
    }

    private void OpenForm(Collector collector)
    {
        if (forms.Read(collector.Instruction.ToString(), collector.FormData) is not { } form) return;
        var id = collector.Context.Document.NextId("cc");
        collector.Marks.Add(new ContentControlMark(collector.Length, id, MarkEdge.Start) { Control = form.Control });
        collector.OpenForm = id;
        collector.FormShown = form.Shown;
        collector.FormStart = collector.Length;
    }

    private void CloseForm(W.Run run, System.Text.StringBuilder buffer, Collector collector)
    {
        if (collector.OpenForm is not { } id) return;
        if (collector.Length == collector.FormStart && collector.FormShown is { Length: > 0 } shown)
        {
            buffer.Append(shown);
            Flush(run, buffer, collector);
        }
        collector.Marks.Add(new ContentControlMark(collector.Length, id, MarkEdge.End));
        collector.OpenForm = null;
        collector.FormData = null;
    }

    private void UnclosedField(Collector collector)
    {
        var instruction = collector.Instruction.ToString();
        if (collector.FieldDepth == 0 || fields.Structural(instruction)) return;
        if (collector.InInstruction || collector.Field is { Kind: TextFieldKind.Preserved } || collector.Field is not null && !collector.FieldEmitted)
            collector.Context.Losses.Add("DOCX151");
    }

    private DocxResolvedInstruction Resolved(Collector collector) => fields.Resolve(collector.Instruction.ToString(), collector.Nested);

    private DocxField? Locked(DocxField? field, bool locked) =>
        field is null || !locked ? field : field with { Detail = (field.Detail ?? new FieldDetail()) with { Fixed = true } };

    private void EmptyField(Collector collector)
    {
        if (collector.Field is not { } field || collector.FieldEmitted) return;
        collector.Add(new TextRun(fields.Shown(field)) { Hyperlink = collector.Hyperlink, Field = field.Kind, FieldArgument = field.Argument, FieldDetail = field.Detail, Hidden = field.Hidden });
        collector.FieldEmitted = true;
    }

    private void Flush(W.Run run, System.Text.StringBuilder buffer, Collector collector) => Flush(run, buffer, collector, null);

    private void Flush(W.Run run, System.Text.StringBuilder buffer, Collector collector, string? font)
    {
        if (buffer.Length == 0) return;
        var field = collector.Field;
        if (field is not null) collector.FieldEmitted = true;
        var model = formatting.Set(new TextRun(buffer.ToString())
        {
            StyleName = collector.Context.Styles.NameOf(characters.StyleId(run.RunProperties)),
            Hyperlink = collector.Hyperlink,
            Ruby = collector.Ruby,
            Field = field?.Kind ?? TextFieldKind.None,
            FieldArgument = field?.Argument,
            FieldDetail = field?.Detail,
        }, font is null ? characters.Read(run.RunProperties) : characters.Read(run.RunProperties) with { Font = font });
        if (field?.Hidden is { } hidden) model = model with { Hidden = hidden };
        TrackFormat(run.RunProperties?.RunPropertiesChange, model, collector);
        collector.Add(model);
        buffer.Clear();
    }

    private void TrackFormat(W.RunPropertiesChange? change, TextRun model, Collector collector)
    {
        if (change is null)
        {
            CloseFormat(collector);
            return;
        }

        var key = (change.Author?.Value ?? string.Empty) + "|" + change.Date?.Value.ToString("O", CultureInfo.InvariantCulture);
        if (collector.Format?.Key != key)
        {
            CloseFormat(collector);
            collector.Format = new OpenFormat(collector.Context.Document.NextId("change"), key)
            {
                Author = change.Author?.Value ?? string.Empty,
                Date = Date(change.Date?.Value),
            };
            collector.Marks.Add(new ChangeMark(collector.Length, collector.Format.Id, MarkEdge.Start));
        }

        var previous = change.PreviousRunProperties;
        collector.Format.Previous.Add(formatting.Set(new TextRun(model.Text)
        {
            StyleName = collector.Context.Styles.NameOf(characters.StyleId(previous)),
            Hyperlink = model.Hyperlink,
            Field = model.Field,
            FieldArgument = model.FieldArgument,
            FieldDetail = model.FieldDetail,
        }, characters.Read(previous)));
    }

    private void CloseFormat(Collector collector)
    {
        if (collector.Format is not { } open) return;
        collector.Context.Document.Changes.Add(new FormatChange(open.Id, open.Author, open.Date, Merge(open.Previous)));
        collector.Marks.Add(new ChangeMark(collector.Length, open.Id, MarkEdge.End));
        collector.Format = null;
    }

    private IReadOnlyList<TextRun> Merge(List<TextRun> runs)
    {
        var merged = new List<TextRun>();
        var pending = new System.Text.StringBuilder();
        TextRun? current = null;
        foreach (var run in runs)
        {
            if (current is not null && run.Field == TextFieldKind.None && current with { Text = run.Text } == run)
            {
                pending.Append(run.Text);
                continue;
            }

            if (current is not null) merged.Add(current with { Text = pending.ToString() });
            current = run;
            pending.Clear().Append(run.Text);
        }

        if (current is not null) merged.Add(current with { Text = pending.ToString() });
        return merged;
    }
}
