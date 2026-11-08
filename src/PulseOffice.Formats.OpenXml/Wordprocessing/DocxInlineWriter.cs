using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxInlineWriter(IDirectFormatting formatting, DocxCharacterCodec characters, DocxFieldInstructions fields, DocxDrawingCodec drawings, DocxRevisionWriter revisions,
    DocxContentControls controls, IContentControlCatalog catalog, IMarkOrder order, IBlockTree tree, DocxFieldCodeWriter fieldCodes)
{
    private abstract record Segment;

    private sealed record Piece(OpenXmlElement Element, string? Hyperlink, string? Change) : Segment;

    private sealed record ControlEdge(string Id, ContentControl? Control) : Segment;

    private sealed class Cursor
    {
        public int Index { get; set; }
    }

    private sealed class State
    {
        public string? Insertion { get; set; }

        public string? Deletion { get; set; }

        public string? Change => Insertion ?? Deletion;

        public FormatChange? Format { get; set; }

        public int FormatIndex { get; set; }
    }

    public OpenXmlElement? Drawing(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks) => drawings.Write(item, context, blocks);

    public IEnumerable<OpenXmlElement> Write(ParagraphBlock paragraph, DocxWriteContext context, DocxBlockSink blocks)
    {
        var pieces = new List<Segment>();
        var state = new State { Insertion = context.OpenInsertion, Deletion = context.OpenDeletion };
        foreach (var leading in Leading(paragraph, context))
            if (drawings.Write(leading, context, blocks) is { } drawing) pieces.Add(new Piece(drawing, null, null));
        pieces.AddRange(Continued(paragraph, context));

        var marks = order.Ordered(paragraph.Marks.Concat(FieldBookmarks(paragraph))).ToList();
        var next = 0;
        var position = 0;
        foreach (var run in paragraph.Content.Runs)
        {
            var start = 0;
            while (start < run.Text.Length || (run.Text.Length == 0 && start == 0))
            {
                while (next < marks.Count && marks[next].Offset <= position + start)
                    pieces.AddRange(Mark(marks[next++], context, state, blocks));
                var stop = next < marks.Count && marks[next].Offset < position + run.Text.Length
                    ? marks[next].Offset - position
                    : run.Text.Length;
                if (stop <= start && run.Text.Length > 0) stop = run.Text.Length;
                foreach (var element in Runs(run, start, stop, context, state)) pieces.Add(new Piece(element, run.Hyperlink, state.Change));
                start = stop;
                if (run.Text.Length == 0) break;
            }

            position += run.Text.Length;
        }

        while (next < marks.Count) pieces.AddRange(Mark(marks[next++], context, state, blocks));
        context.OpenInsertion = state.Insertion;
        context.OpenDeletion = state.Deletion;
        return Level(pieces, new Cursor(), context, null);
    }

    private IEnumerable<Segment> Continued(ParagraphBlock paragraph, DocxWriteContext context)
    {
        var opened = paragraph.Marks.OfType<ContentControlMark>().Where(mark => mark.Edge == MarkEdge.Start).Select(mark => mark.ControlId).ToHashSet(StringComparer.Ordinal);
        foreach (var end in paragraph.Marks.OfType<ContentControlMark>().Where(mark => mark.Edge == MarkEdge.End && !opened.Contains(mark.ControlId) && !context.BlockControls.Contains(mark.ControlId)))
            if (catalog.Find(context.Document, end.ControlId) is { } span) yield return new ControlEdge(end.ControlId, span.Control);
    }

    private List<OpenXmlElement> Level(List<Segment> segments, Cursor cursor, DocxWriteContext context, string? closing)
    {
        var output = new List<OpenXmlElement>();
        var plain = new List<Piece>();
        while (cursor.Index < segments.Count)
        {
            var segment = segments[cursor.Index++];
            if (segment is Piece piece)
            {
                plain.Add(piece);
                continue;
            }
            if (segment is not ControlEdge edge) continue;
            if (edge.Control is { } control)
            {
                output.AddRange(Group(plain, context).ToList());
                plain.Clear();
                var inner = Level(segments, cursor, context, edge.Id);
                output.Add(new W.SdtRun(controls.Properties(control), new W.SdtContentRun(inner)));
                continue;
            }
            if (edge.Id != closing) continue;
            output.AddRange(Group(plain, context).ToList());
            return output;
        }
        output.AddRange(Group(plain, context).ToList());
        return output;
    }

    private IEnumerable<InlineMark> FieldBookmarks(ParagraphBlock paragraph)
    {
        var offset = 0;
        foreach (var run in paragraph.Content.Runs)
        {
            offset += run.Text.Length;
            if (run.Field == TextFieldKind.VariableSet && run.FieldArgument is { Length: > 0 } variable)
            {
                yield return new BookmarkMark(offset - run.Text.Length, variable, MarkEdge.Start);
                yield return new BookmarkMark(offset, variable, MarkEdge.End);
            }
            if (run.Field != TextFieldKind.Sequence || run.FieldDetail?.Format is not { Length: > 0 } name) continue;
            yield return new BookmarkMark(0, name, MarkEdge.Start);
            yield return new BookmarkMark(offset, name, MarkEdge.End);
        }
    }

    private IEnumerable<AnchoredObject> Leading(ParagraphBlock paragraph, DocxWriteContext context)
    {
        var marked = paragraph.Marks.OfType<ObjectMark>().Select(mark => mark.ObjectId).ToHashSet(StringComparer.Ordinal);
        foreach (var item in context.Document.Objects.AnchoredTo(paragraph.Id).Where(item => !marked.Contains(item.Id) && !context.FloatingTables.Contains(item.Id)))
            yield return item;
        if (context.Part is DocumentFormat.OpenXml.Packaging.MainDocumentPart && context.PendingPageObjects.Count > 0)
        {
            var pending = context.Document.Objects.All.Where(item => context.PendingPageObjects.Contains(item.Id)).ToList();
            context.PendingPageObjects.Clear();
            foreach (var item in pending) yield return item;
        }
    }

    private IEnumerable<OpenXmlElement> Group(List<Piece> pieces, DocxWriteContext context)
    {
        var index = 0;
        while (index < pieces.Count)
        {
            if (pieces[index].Hyperlink is not { } target)
            {
                var run = new List<Piece>();
                while (index < pieces.Count && pieces[index].Hyperlink is null) run.Add(pieces[index++]);
                foreach (var element in Insertions(run, context)) yield return element;
                continue;
            }

            var link = target.StartsWith('#') ? new W.Hyperlink { Anchor = target[1..] } : new W.Hyperlink { Id = context.Link(target) };
            var inside = new List<Piece>();
            while (index < pieces.Count && pieces[index].Hyperlink == target) inside.Add(pieces[index++]);
            link.Append(Insertions(inside, context));
            yield return link;
        }
    }

    private IEnumerable<OpenXmlElement> Insertions(List<Piece> pieces, DocxWriteContext context)
    {
        var index = 0;
        while (index < pieces.Count)
        {
            if (pieces[index].Change is not { } change || context.Document.Changes.Find(change) is not { } found)
            {
                yield return pieces[index++].Element;
                continue;
            }

            OpenXmlCompositeElement wrapper = found is Deletion ? revisions.DeletedRange(found, context) : revisions.Inserted(found, context);
            while (index < pieces.Count && pieces[index].Change == change) wrapper.Append(pieces[index++].Element);
            yield return wrapper;
        }
    }

    public IReadOnlyList<OpenXmlElement> MarkProperties(ParagraphBlock block, DocxWriteContext context) =>
        block.Text.Length == 0 && block.Content.Runs is [{ Text.Length: 0 } run]
            ? [.. characters.Elements(formatting.Run(run), context.Styles.IdOf(StyleFamily.Character, run.StyleName))]
            : [];

    private IEnumerable<OpenXmlElement> Runs(TextRun run, int start, int stop, DocxWriteContext context, State state)
    {
        if (start == 0 && stop == run.Text.Length && ComplexField(run, context, state.Deletion is not null && state.Insertion is null) is { } complex) return complex;
        return [Run(run, run.Text[start..stop], context, state)];
    }

    public IReadOnlyList<W.Run>? ComplexField(TextRun run, DocxWriteContext context, bool deleted)
    {
        var parts = fields.Composite(run, context.Document.Variables, name => Referenced(context, name))
                    ?? (deleted && run.Field != TextFieldKind.None && fields.Instruction(run, context.Document.Variables) is { } instruction ? [new DocxInstructionPart(instruction)] : null);
        if (parts is null) return null;
        var properties = characters.Elements(formatting.Run(run), context.Styles.IdOf(StyleFamily.Character, run.StyleName)).ToList();
        W.Run Piece(params OpenXmlElement[] children)
        {
            var piece = new W.Run();
            if (properties.Count > 0) piece.Append(new W.RunProperties(properties.Select(property => property.CloneNode(true))));
            piece.Append(children);
            return piece;
        }
        W.Run Shown(string text)
        {
            var piece = Piece();
            AppendText(piece, text, deleted);
            return piece;
        }
        OpenXmlElement Code(string text) => deleted
            ? new W.DeletedFieldCode(text) { Space = SpaceProcessingModeValues.Preserve }
            : new W.FieldCode(text) { Space = SpaceProcessingModeValues.Preserve };
        var begin = new W.FieldChar { FieldCharType = W.FieldCharValues.Begin, FieldLock = run.FieldDetail?.Fixed == true ? true : null };
        if (fields.Data(run) is { } data) begin.Append(new W.FieldData(data));
        var elements = new List<W.Run> { Piece(begin) };
        foreach (var part in parts)
        {
            if (!part.Nested)
            {
                elements.Add(Piece(Code(part.Text)));
                continue;
            }
            elements.Add(Piece(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }));
            elements.Add(Piece(Code(part.Text)));
            elements.Add(Piece(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
            elements.Add(Shown(context.Document.Variables.GetValueOrDefault(part.Reference!) ?? string.Empty));
            elements.Add(Piece(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        }
        elements.Add(Piece(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
        elements.Add(Shown(run.Text));
        elements.Add(Piece(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        return elements;
    }

    private string EntryInstruction(IndexEntryMark entry, string? range) =>
        " XE \"" + string.Join(":", entry.Path.Select(level => level.Replace(":", "\\:", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal))) + "\" "
        + (range is null ? string.Empty : "\\r " + range + " ");

    private bool Referenced(DocxWriteContext context, string name) =>
        (context.ReferencedNames ??= ReferencedNames(context.Document)).Contains(name);

    private HashSet<string> ReferencedNames(PulseOffice.Documents.TextDocument document) =>
        tree.Paragraphs(document.Body).SelectMany(paragraph => paragraph.Marks.OfType<BookmarkMark>().Select(mark => mark.Name)
                .Concat(paragraph.Content.Runs.Where(item => item.Field == TextFieldKind.VariableSet && item.FieldArgument is not null).Select(item => item.FieldArgument!)))
            .ToHashSet(StringComparer.Ordinal);

    private OpenXmlElement Run(TextRun run, string text, DocxWriteContext context, State state)
    {
        var element = new W.Run();
        var properties = characters.Elements(formatting.Run(run), context.Styles.IdOf(StyleFamily.Character, run.StyleName)).ToList();
        if (state.Format is { } format && text.Length > 0)
        {
            var previous = format.PreviousRuns.Count == 0 ? run : format.PreviousRuns[Math.Min(state.FormatIndex++, format.PreviousRuns.Count - 1)];
            properties.Add(revisions.FormatChange(format, previous, context));
        }

        if (properties.Count > 0) element.Append(new W.RunProperties(properties));
        AppendText(element, text, deleted: state.Deletion is not null && state.Insertion is null);
        if (run.Field != TextFieldKind.None && fields.Instruction(run, context.Document.Variables) is { } instruction)
        {
            var simple = new W.SimpleField(element) { Instruction = instruction, FieldLock = run.FieldDetail?.Fixed == true ? true : null };
            if (fields.Data(run) is { } data) simple.PrependChild(new W.FieldData(data));
            return simple;
        }
        if (run.Ruby is { Length: > 0 } ruby && text.Length > 0)
        {
            var size = (int)Math.Round((formatting.Run(run).FontSize ?? 12) * 2);
            return new W.Run(new W.Ruby(
                new W.RubyProperties(
                    new W.RubyAlign { Val = W.RubyAlignValues.Center },
                    new W.PhoneticGuideTextFontSize { Val = Math.Max(2, size / 2).ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new W.PhoneticGuideRaise { Val = (short)size },
                    new W.PhoneticGuideBaseTextSize { Val = size.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new W.LanguageId { Val = run.Language ?? "ja-JP" }),
                new W.RubyContent(new W.Run(new W.Text(ruby) { Space = SpaceProcessingModeValues.Preserve })),
                new W.RubyBase(element)));
        }
        return element;
    }

    public void AppendText(W.Run run, string text, bool deleted)
    {
        var buffer = new System.Text.StringBuilder();
        void Flush()
        {
            if (buffer.Length == 0) return;
            run.Append(deleted
                ? new W.DeletedText(buffer.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                : new W.Text(buffer.ToString()) { Space = SpaceProcessingModeValues.Preserve });
            buffer.Clear();
        }

        foreach (var character in text)
        {
            switch (character)
            {
                case '\t':
                    Flush();
                    run.Append(new W.TabChar());
                    break;
                case '\n':
                    Flush();
                    run.Append(new W.Break());
                    break;
                default:
                    buffer.Append(character);
                    break;
            }
        }

        Flush();
    }

    private IEnumerable<Segment> Mark(InlineMark mark, DocxWriteContext context, State state, DocxBlockSink blocks)
    {
        switch (mark)
        {
            case ContentControlMark control when !context.BlockControls.Contains(control.ControlId):
                yield return new ControlEdge(control.ControlId, control.Edge == MarkEdge.Start ? control.Control ?? new ContentControl() : null);
                break;
            case BookmarkMark bookmark:
                var bookmarkId = context.Bookmarks[bookmark.Name].ToString(CultureInfo.InvariantCulture);
                if (bookmark.Edge != MarkEdge.End) yield return new Piece(new W.BookmarkStart { Id = bookmarkId, Name = bookmark.Name }, null, null);
                if (bookmark.Edge != MarkEdge.Start) yield return new Piece(new W.BookmarkEnd { Id = bookmarkId }, null, null);
                break;
            case CommentMark comment when context.Document.Comments.Find(comment.CommentId) is { IsReply: false }:
                var thread = context.Document.Comments.Thread(comment.CommentId).Select(item => context.Comments[item.Id].ToString(CultureInfo.InvariantCulture)).ToList();
                if (comment.Edge != MarkEdge.End)
                    foreach (var commentId in thread) yield return new Piece(new W.CommentRangeStart { Id = commentId }, null, null);
                if (comment.Edge != MarkEdge.Start)
                {
                    foreach (var commentId in thread)
                    {
                        yield return new Piece(new W.CommentRangeEnd { Id = commentId }, null, null);
                        yield return new Piece(new W.Run(new W.CommentReference { Id = commentId }), null, null);
                    }
                }

                break;
            case NoteMark note when context.Document.Notes.Find(note.NoteId) is { } found:
                var custom = found.Citation is { Length: > 0 };
                OpenXmlElement reference = found.Kind == NoteKind.Endnote
                    ? new W.EndnoteReference { Id = context.Endnotes[found.Id], CustomMarkFollows = custom ? true : null }
                    : new W.FootnoteReference { Id = context.Footnotes[found.Id], CustomMarkFollows = custom ? true : null };
                var noteRun = new W.Run(new W.RunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }), reference);
                if (custom) noteRun.Append(new W.Text(found.Citation!) { Space = SpaceProcessingModeValues.Preserve });
                var noteBookmark = context.Bookmarks[DocxNoteBookmarks.Prefix + found.Id].ToString(CultureInfo.InvariantCulture);
                yield return new Piece(new W.BookmarkStart { Id = noteBookmark, Name = DocxNoteBookmarks.Prefix + found.Id }, null, null);
                yield return new Piece(noteRun, null, state.Change);
                yield return new Piece(new W.BookmarkEnd { Id = noteBookmark }, null, null);
                break;
            case FieldCodeMark fieldCode:
                foreach (var fieldRun in fieldCodes.Write(fieldCode, context)) yield return new Piece(fieldRun, null, state.Change);
                break;
            case IndexEntryMark entry when entry.Edge == MarkEdge.Start:
                var range = context.OpenIndexRange(entry.Key);
                yield return new Piece(new W.BookmarkStart { Id = context.Bookmarks[range].ToString(CultureInfo.InvariantCulture), Name = range }, null, null);
                yield return new Piece(new W.SimpleField { Instruction = EntryInstruction(entry, range) }, null, state.Change);
                break;
            case IndexEntryMark entry when entry.Edge == MarkEdge.End:
                if (context.CloseIndexRange(entry.Key) is { } closed)
                    yield return new Piece(new W.BookmarkEnd { Id = context.Bookmarks[closed].ToString(CultureInfo.InvariantCulture) }, null, null);
                break;
            case IndexEntryMark entry:
                yield return new Piece(new W.SimpleField { Instruction = EntryInstruction(entry, null) }, null, state.Change);
                break;
            case ObjectMark item when context.Document.Objects.Find(item.ObjectId) is { } anchored
                                      && drawings.Write(anchored, context, blocks) is { } drawing:
                yield return new Piece(drawing, null, state.Change);
                break;
            case ChangeMark change when context.Change(change.ChangeId) is { } tracked:
                switch (tracked)
                {
                    case Insertion:
                        if (change.Edge == MarkEdge.Start) state.Insertion = tracked.Id;
                        else if (change.Edge == MarkEdge.End && state.Insertion == tracked.Id) state.Insertion = null;
                        break;
                    case FormatChange format:
                        if (change.Edge == MarkEdge.Start)
                        {
                            state.Format = format;
                            state.FormatIndex = 0;
                        }
                        else if (change.Edge == MarkEdge.End && state.Format == format) state.Format = null;
                        break;
                    case Deletion when change.Edge == MarkEdge.Start:
                        state.Deletion = tracked.Id;
                        break;
                    case Deletion when change.Edge == MarkEdge.End:
                        if (state.Deletion == tracked.Id) state.Deletion = null;
                        break;
                    case Deletion deletion when change.Edge == MarkEdge.Point:
                        var host = deletion.WithinInsertion is { } within && context.Document.Changes.Find(within) is Insertion ? within : null;
                        foreach (var (element, hyperlink) in revisions.Deleted(deletion, context, this, blocks)) yield return new Piece(element, hyperlink, host);
                        break;
                }

                break;
        }
    }
}
