using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public interface IDocumentEditor
{
    DocumentEdit InsertText(TextDocument document, UndoStack history, DocumentPosition at, string text);

    DocumentEdit Delete(TextDocument document, UndoStack history, DocumentRange range);

    DocumentEdit SplitParagraph(TextDocument document, UndoStack history, DocumentPosition at);

    DocumentEdit MergeWithNext(TextDocument document, UndoStack history, string paragraphId);

    DocumentEdit ApplyParagraphStyle(TextDocument document, UndoStack history, DocumentRange range, string styleName);

    DocumentEdit ApplyCharacterStyle(TextDocument document, UndoStack history, DocumentRange range, string? styleName);

    DocumentEdit SetCharacter(TextDocument document, UndoStack history, DocumentRange range, CharacterProperties properties);

    DocumentEdit SetParagraph(TextDocument document, UndoStack history, DocumentRange range, ParagraphProperties properties);

    DocumentEdit InsertTable(TextDocument document, UndoStack history, DocumentPosition at, int rows, int columns);

    DocumentEdit InsertNote(TextDocument document, UndoStack history, DocumentPosition at, NoteKind kind, string text);

    DocumentEdit AddComment(TextDocument document, UndoStack history, DocumentRange range, string author, DateTimeOffset date, string text, string? parentId = null);

    DocumentEdit AddBookmark(TextDocument document, UndoStack history, DocumentRange range, string name);

    DocumentEdit InsertObject(TextDocument document, UndoStack history, AnchoredObject item, DocumentPosition at);

    DocumentEdit InsertField(TextDocument document, UndoStack history, DocumentPosition at, TextFieldKind field, string display, string? argument = null);

    DocumentEdit SetPageStyle(TextDocument document, UndoStack history, string paragraphId, string? pageStyleName, int? pageNumberRestart = null);

    DocumentEdit SetList(TextDocument document, UndoStack history, DocumentRange range, string? listStyleName, int level);

    DocumentEdit MapRuns(TextDocument document, UndoStack history, DocumentRange range, string description, Func<TextRun, TextRun> map);

    DocumentEdit TransformText(TextDocument document, UndoStack history, DocumentRange range, string description, Func<string, string> transform);

    DocumentEdit MapParagraphs(TextDocument document, UndoStack history, DocumentRange range, string description, Func<TextParagraph, TextParagraph> map);

    DocumentEdit ClearDirectFormatting(TextDocument document, UndoStack history, DocumentRange range);

    DocumentEdit RemoveObject(TextDocument document, UndoStack history, string objectId);

    DocumentEdit ChangeObject(TextDocument document, UndoStack history, string objectId, Action<AnchoredObject> change, DocumentPosition? markAt = null);

    DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, Clipboard.DocumentFragment fragment);
}

public interface IDirectDocumentEditor : IDocumentEditor
{
    DocumentEdit Delete(TextDocument document, UndoStack history, DocumentRange range, AnnotationFate fate);
}

public sealed class DocumentEditor(IStoryCatalog stories, IParagraphText text, IDirectFormatting direct, IContentTrails trails, IRangeDeletion deletion, IBlockTree tree,
    IObjectAnchoring anchoring, ISequenceFields sequences, ITableEditing tables, Clipboard.IFragmentPaster paster)
    : IDirectDocumentEditor
{
    public DocumentEdit Paste(TextDocument document, UndoStack history, DocumentPosition at, Clipboard.DocumentFragment fragment) =>
        paster.Paste(document, history, at, fragment);

    private const string ParagraphPrefix = "p";

    public DocumentEdit InsertText(TextDocument document, UndoStack history, DocumentPosition at, string value)
    {
        at = Snapped(document, at);
        var lines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var found = stories.Find(document, at.ParagraphId);
        var recorder = new EditRecorder(document, "insert-text").Touch(found.Paragraph);
        if (lines.Length > 1) recorder.Touch(found.Location.Container);
        var paragraph = InsertLine(recorder, found.Paragraph, at.Offset, lines[0]);
        var offset = at.Offset + lines[0].Length;
        var index = found.Location.Index;
        foreach (var line in lines.Skip(1))
        {
            paragraph = Split(document, recorder, found.Location.Container, ++index, paragraph, offset);
            InsertLine(recorder, paragraph, 0, line);
            offset = line.Length;
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Delete(TextDocument document, UndoStack history, DocumentRange range) => Delete(document, history, range, AnnotationFate.Purge);

    public DocumentEdit Delete(TextDocument document, UndoStack history, DocumentRange range, AnnotationFate fate)
    {
        var recorder = new EditRecorder(document, "delete");
        if (range.Start.ParagraphId == range.End.ParagraphId)
        {
            var paragraph = stories.Find(document, range.Start.ParagraphId).Paragraph;
            if (range.End.Offset < range.Start.Offset) throw new InvalidOperationException("The deletion range is not in document order.");
            deletion.RemoveText(document, recorder, paragraph, range.Start.Offset, range.End.Offset, fate);
            sequences.Renumber(document, recorder);
            return recorder.Commit(history);
        }
        deletion.Delete(document, recorder, trails.Point(document, range.Start), trails.Point(document, range.End), fate);
        sequences.Renumber(document, recorder);
        return recorder.Commit(history);
    }

    public DocumentEdit SetPageStyle(TextDocument document, UndoStack history, string paragraphId, string? pageStyleName, int? pageNumberRestart = null)
    {
        if (pageStyleName is not null && !document.Styles.Page.Contains(pageStyleName)) throw new KeyNotFoundException($"Page style '{pageStyleName}' not found.");
        if (pageStyleName is null && pageNumberRestart is not null) throw new ArgumentException("A page number restart needs a page style.", nameof(pageNumberRestart));
        if (pageNumberRestart is not null) ArgumentOutOfRangeException.ThrowIfNegative(pageNumberRestart.Value);
        var trail = trails.Find(document, paragraphId);
        if (!ReferenceEquals(trail.Story, document.Body) || trail.Steps.Any(step => step.Block is TableBlock))
        {
            throw new InvalidOperationException("A page style can only start at a body paragraph outside tables.");
        }
        var recorder = new EditRecorder(document, "page-style").Touch(trail.Paragraph);
        trail.Paragraph.State = trail.Paragraph.State with
        {
            Content = trail.Paragraph.Content with { PageStyleName = pageStyleName, PageNumberRestart = pageNumberRestart },
        };
        return recorder.Commit(history);
    }

    public DocumentEdit SplitParagraph(TextDocument document, UndoStack history, DocumentPosition at)
    {
        at = Snapped(document, at);
        var found = stories.Find(document, at.ParagraphId);
        var paragraph = found.Paragraph;
        var recorder = new EditRecorder(document, "split-paragraph").Touch(paragraph).Touch(found.Location.Container);
        Split(document, recorder, found.Location.Container, found.Location.Index + 1, paragraph, at.Offset);
        return recorder.Commit(history);
    }

    private ParagraphBlock Split(TextDocument document, EditRecorder recorder, BlockContainer container, int index, ParagraphBlock paragraph, int offset)
    {
        var atEnd = offset == paragraph.Length;
        var (left, right) = text.Split(paragraph.State, offset);
        paragraph.State = left;
        var nextStyle = atEnd ? document.Styles.Paragraph.Find(paragraph.StyleName)?.NextStyleName : null;
        var cleared = right.Content with { PageStyleName = null, PageNumberRestart = null, PageBreakBefore = null, ColumnBreakBefore = null };
        var content = nextStyle is null ? cleared : cleared with { StyleName = nextStyle, OutlineLevel = null };
        var created = new ParagraphBlock(document.NextId(ParagraphPrefix), content) { Marks = right.Marks };
        container.Insert(index, created);
        anchoring.MoveMarked(document, recorder, right.Marks, paragraph.Id, created.Id);
        recorder.Record(new ParagraphSplit(paragraph.Id, offset, created.Id));
        return created;
    }

    private ParagraphBlock InsertLine(EditRecorder recorder, ParagraphBlock paragraph, int offset, string value)
    {
        paragraph.State = text.Insert(paragraph.State, offset, value);
        if (value.Length > 0) recorder.Record(new TextInserted(paragraph.Id, offset, value.Length));
        return paragraph;
    }

    public DocumentEdit MergeWithNext(TextDocument document, UndoStack history, string paragraphId)
    {
        var found = stories.Find(document, paragraphId);
        var container = found.Location.Container;
        if (found.Location.Index + 1 >= container.Count || container[found.Location.Index + 1] is not ParagraphBlock next)
        {
            throw new InvalidOperationException("The next block is not a paragraph.");
        }
        var recorder = new EditRecorder(document, "merge-paragraphs").Touch(container);
        deletion.Join(document, recorder, found.Paragraph, next);
        container.RemoveAt(found.Location.Index + 1);
        return recorder.Commit(history);
    }

    public DocumentEdit ApplyParagraphStyle(TextDocument document, UndoStack history, DocumentRange range, string styleName)
    {
        if (!document.Styles.Paragraph.Contains(styleName)) throw new KeyNotFoundException($"Paragraph style '{styleName}' not found.");
        return EachParagraph(document, history, range, "paragraph-style",
            (state, _, _) => state with { Content = state.Content with { StyleName = styleName, OutlineLevel = null } });
    }

    public DocumentEdit ApplyCharacterStyle(TextDocument document, UndoStack history, DocumentRange range, string? styleName)
    {
        if (styleName is not null && !document.Styles.Character.Contains(styleName)) throw new KeyNotFoundException($"Character style '{styleName}' not found.");
        return EachParagraph(document, history, range, "character-style",
            (state, start, end) => text.Map(state, start, end, run => run with { StyleName = styleName }));
    }

    public DocumentEdit SetCharacter(TextDocument document, UndoStack history, DocumentRange range, CharacterProperties properties) =>
        EachParagraph(document, history, range, "character-format",
            (state, start, end) => text.Map(state, start, end, run => direct.Set(run, properties)));

    public DocumentEdit SetParagraph(TextDocument document, UndoStack history, DocumentRange range, ParagraphProperties properties) =>
        EachParagraph(document, history, range, "paragraph-format",
            (state, _, _) => state with { Content = direct.Set(state.Content, properties) });

    public DocumentEdit SetList(TextDocument document, UndoStack history, DocumentRange range, string? listStyleName, int level)
    {
        if (listStyleName is not null && !document.Styles.List.Contains(listStyleName)) throw new KeyNotFoundException($"List style '{listStyleName}' not found.");
        var clamped = Math.Clamp(level, 0, ListStyle.LevelCount - 1);
        var format = document.Styles.List.Find(listStyleName)?.Level(clamped).Format;
        return EachParagraph(document, history, range, "list",
            (state, _, _) => state with
            {
                Content = state.Content with
                {
                    ListStyleName = listStyleName,
                    BulletLevel = clamped,
                    Bullet = format == NumberFormat.Bullet,
                    Numbered = format is not null and not NumberFormat.Bullet,
                    ListRestart = listStyleName is null ? null : state.Content.ListRestart,
                    ListId = listStyleName is not null && listStyleName == state.Content.ListStyleName ? state.Content.ListId : null,
                    ListHeader = false,
                },
            });
    }

    public DocumentEdit MapRuns(TextDocument document, UndoStack history, DocumentRange range, string description, Func<TextRun, TextRun> map) =>
        EachParagraph(document, history, range, description, (state, start, end) => text.Map(state, start, end, run =>
        {
            var mapped = map(run);
            return mapped.Text.Length == run.Text.Length ? mapped : mapped with { Text = run.Text };
        }));

    public DocumentEdit TransformText(TextDocument document, UndoStack history, DocumentRange range, string description, Func<string, string> transform) =>
        EachParagraph(document, history, range, description, (state, start, end) =>
        {
            var pieces = new List<TextRun>();
            text.Map(state, start, end, run =>
            {
                pieces.Add(run);
                return run;
            });
            var plain = string.Concat(pieces.Select(run => run.Text));
            var changed = transform(plain);
            if (changed.Length != plain.Length) return state;
            var cursor = 0;
            return text.Map(state, start, end, run =>
            {
                var piece = changed.Substring(cursor, run.Text.Length);
                cursor += run.Text.Length;
                return run.Field == TextFieldKind.None ? run with { Text = piece } : run;
            });
        });

    public DocumentEdit MapParagraphs(TextDocument document, UndoStack history, DocumentRange range, string description, Func<TextParagraph, TextParagraph> map) =>
        EachParagraph(document, history, range, description, (state, _, _) =>
        {
            var mapped = map(state.Content);
            return state with { Content = mapped.Text == state.Content.Text ? mapped : state.Content };
        });

    public DocumentEdit ClearDirectFormatting(TextDocument document, UndoStack history, DocumentRange range) =>
        EachParagraph(document, history, range, "clear-formatting", (state, start, end) => text.Map(state, start, end,
            run => direct.Clear(run) with { Field = run.Field, FieldArgument = run.FieldArgument, Hyperlink = run.Hyperlink, StyleName = run.StyleName }));

    public DocumentEdit RemoveObject(TextDocument document, UndoStack history, string objectId)
    {
        var recorder = new EditRecorder(document, "remove-object").Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => mark is ObjectMark found && found.ObjectId == objectId)).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => mark is not ObjectMark found || found.ObjectId != objectId)] };
        }
        document.Objects.Remove(objectId);
        sequences.Renumber(document, recorder);
        return recorder.Commit(history);
    }

    public DocumentEdit ChangeObject(TextDocument document, UndoStack history, string objectId, Action<AnchoredObject> change, DocumentPosition? markAt = null)
    {
        var item = document.Objects.Find(objectId) ?? throw new KeyNotFoundException($"Object '{objectId}' not found.");
        var recorder = new EditRecorder(document, "change-object").Touch(item);
        change(item);
        if (markAt is { } at)
        {
            at = Snapped(document, at);
            var marked = stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => mark is ObjectMark found && found.ObjectId == objectId)).ToList();
            var target = stories.Find(document, at.ParagraphId).Paragraph;
            foreach (var paragraph in marked.Append(target)) recorder.Touch(paragraph);
            foreach (var paragraph in marked)
                paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => mark is not ObjectMark found || found.ObjectId != objectId)] };
            target.State = text.AddMark(target.State, new ObjectMark(at.Offset, objectId));
        }
        return recorder.Commit(history);
    }

    public DocumentEdit InsertTable(TextDocument document, UndoStack history, DocumentPosition at, int rows, int columns)
    {
        at = Snapped(document, at);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        using var transaction = history.BeginTransaction("insert-table");
        var found = stories.Find(document, at.ParagraphId);
        var index = found.Location.Index + (at.Offset == 0 ? 0 : 1);
        if (at.Offset > 0 && at.Offset < found.Paragraph.Length) SplitParagraph(document, history, at);
        var width = new Twip(Math.Max(Twip.FromMillimeters(5).Value, AvailableWidth(document, at.ParagraphId) / columns));
        var table = new TableBlock(
            document.NextId("t"),
            Enumerable.Range(0, rows).Select(_ => new TableRow(Enumerable.Range(0, columns).Select(_ => new TableCell(new BlockContainer(
                [new ParagraphBlock(document.NextId(ParagraphPrefix), string.Empty, DefaultStyleNames.TableContents)]))))),
            Enumerable.Repeat(width, columns))
        { StyleName = DefaultStyleNames.DefaultTableStyle };
        var recorder = new EditRecorder(document, "insert-table").Touch(found.Location.Container);
        found.Location.Container.Insert(index, table);
        recorder.Record(new BlockRestored(table.Id));
        if (index == found.Location.Container.Count - 1)
        {
            var following = new ParagraphBlock(document.NextId(ParagraphPrefix), string.Empty, found.Paragraph.StyleName);
            found.Location.Container.Insert(index + 1, following);
            recorder.Record(new BlockRestored(following.Id));
        }
        return recorder.Commit(history);
    }

    private int AvailableWidth(TextDocument document, string paragraphId)
    {
        if (tables.Locate(document, paragraphId) is { } cell)
        {
            var span = Math.Max(1, cell.Table.Rows[cell.Row].Cells[cell.Column].ColumnSpan);
            var spanned = cell.Table.Columns.Skip(cell.Column).Take(span).Sum(column => column.Value);
            return Math.Max(0, spanned - 2 * CellInset);
        }
        var page = document.Styles.Page.Find(document.InitialPageStyle);
        return page is null ? DefaultTextWidth : Math.Max(0, page.Size.Width.Value - page.Margins.Left.Value - page.Margins.Right.Value);
    }

    private const int DefaultTextWidth = 9638;

    private const int CellInset = 108;

    public DocumentEdit InsertNote(TextDocument document, UndoStack history, DocumentPosition at, NoteKind kind, string value)
    {
        at = Snapped(document, at);
        var found = stories.Find(document, at.ParagraphId);
        var style = kind == NoteKind.Footnote ? document.Notes.FootnoteSettings.ParagraphStyleName : document.Notes.EndnoteSettings.ParagraphStyleName;
        var note = new Note(document.NextId(kind == NoteKind.Footnote ? "fn" : "en"), kind,
            new BlockContainer([new ParagraphBlock(document.NextId(ParagraphPrefix), value, style)]));
        var recorder = new EditRecorder(document, "insert-note").Touch(found.Paragraph).Touch(document.Notes, () => document.Notes.All, document.Notes.Replace);
        found.Paragraph.State = text.AddMark(found.Paragraph.State, new NoteMark(at.Offset, note.Id));
        document.Notes.Add(note);
        return recorder.Commit(history);
    }

    public DocumentEdit AddComment(TextDocument document, UndoStack history, DocumentRange range, string author, DateTimeOffset date, string value, string? parentId = null)
    {
        var comment = new Comment(document.NextId("c"), author, date,
            new BlockContainer([new ParagraphBlock(document.NextId(ParagraphPrefix), value)])) { ParentId = parentId };
        var recorder = new EditRecorder(document, "add-comment").Touch(document.Comments, () => document.Comments.All, document.Comments.Replace);
        if (parentId is null) Bracket(document, recorder, range, edge => new CommentMark(0, comment.Id, edge));
        document.Comments.Add(comment);
        return recorder.Commit(history);
    }

    public DocumentEdit AddBookmark(TextDocument document, UndoStack history, DocumentRange range, string name)
    {
        var recorder = new EditRecorder(document, "add-bookmark");
        Bracket(document, recorder, range, edge => new BookmarkMark(0, name, range.IsCollapsed ? MarkEdge.Point : edge));
        return recorder.Commit(history);
    }

    public DocumentEdit InsertObject(TextDocument document, UndoStack history, AnchoredObject item, DocumentPosition at)
    {
        at = Snapped(document, at);
        var found = stories.Find(document, at.ParagraphId);
        var recorder = new EditRecorder(document, "insert-object").Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        if (item.Anchor is CharacterAnchor or AsCharacterAnchor)
        {
            recorder.Touch(found.Paragraph);
            found.Paragraph.State = text.AddMark(found.Paragraph.State, new ObjectMark(at.Offset, item.Id));
        }
        document.Objects.Add(item);
        return recorder.Commit(history);
    }

    public DocumentEdit InsertField(TextDocument document, UndoStack history, DocumentPosition at, TextFieldKind field, string display, string? argument = null)
    {
        at = Snapped(document, at);
        var found = stories.Find(document, at.ParagraphId);
        var recorder = new EditRecorder(document, "insert-field").Touch(found.Paragraph);
        var inserted = text.Insert(found.Paragraph.State, at.Offset, display);
        found.Paragraph.State = text.Map(inserted, at.Offset, at.Offset + display.Length,
            run => run with { Field = field, FieldArgument = argument });
        recorder.Record(new TextInserted(at.ParagraphId, at.Offset, display.Length));
        if (field == TextFieldKind.Sequence) sequences.Renumber(document, recorder);
        return recorder.Commit(history);
    }

    private DocumentPosition Snapped(TextDocument document, DocumentPosition at) =>
        at with { Offset = text.Snap(stories.Find(document, at.ParagraphId).Paragraph.State, at.Offset) };

    private void Bracket(TextDocument document, EditRecorder recorder, DocumentRange range, Func<MarkEdge, InlineMark> mark)
    {
        var start = stories.Find(document, range.Start.ParagraphId).Paragraph;
        var end = stories.Find(document, range.End.ParagraphId).Paragraph;
        recorder.Touch(start).Touch(end);
        if (range.IsCollapsed)
        {
            start.State = text.AddMark(start.State, mark(MarkEdge.Point).At(range.Start.Offset));
            return;
        }
        start.State = text.AddMark(start.State, mark(MarkEdge.Start).At(range.Start.Offset));
        end.State = text.AddMark(end.State, mark(MarkEdge.End).At(range.End.Offset));
    }

    private DocumentEdit EachParagraph(TextDocument document, UndoStack history, DocumentRange range, string description,
        Func<ParagraphState, int, int, ParagraphState> change)
    {
        var start = trails.Point(document, range.Start);
        var end = trails.Point(document, range.End);
        if (!ReferenceEquals(start.Trail.Story, end.Trail.Story)) throw new InvalidOperationException("A formatting range cannot span different stories.");
        if (start.CompareTo(end) > 0) throw new InvalidOperationException("The formatting range is not in document order.");
        var recorder = new EditRecorder(document, description);
        var first = start.Trail.Paragraph;
        var last = end.Trail.Paragraph;
        var paragraphs = tree.Paragraphs(start.Trail.Story)
            .SkipWhile(paragraph => !ReferenceEquals(paragraph, first))
            .TakeWhile(paragraph => !ReferenceEquals(paragraph, last))
            .Append(last)
            .ToList();
        foreach (var paragraph in paragraphs)
        {
            recorder.Touch(paragraph);
            var from = ReferenceEquals(paragraph, first) ? range.Start.Offset : 0;
            var to = ReferenceEquals(paragraph, last) ? range.End.Offset : paragraph.Length;
            paragraph.State = change(paragraph.State, from, to);
        }
        return recorder.Commit(history);
    }
}
