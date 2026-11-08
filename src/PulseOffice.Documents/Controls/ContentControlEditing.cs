using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Controls;

public interface IContentControlEditing
{
    DocumentEdit Insert(TextDocument document, UndoStack history, DocumentRange range, ContentControl control);

    DocumentEdit Change(TextDocument document, UndoStack history, string controlId, ContentControl control);

    DocumentEdit Toggle(TextDocument document, UndoStack history, string controlId);

    DocumentEdit Choose(TextDocument document, UndoStack history, string controlId, string text);

    DocumentEdit SetDate(TextDocument document, UndoStack history, string controlId, DateTime date);

    DocumentEdit SetPicture(TextDocument document, UndoStack history, string controlId, AnchoredObject picture);

    DocumentEdit Fill(TextDocument document, UndoStack history, string controlId, string text);

    DocumentEdit Refill(TextDocument document, UndoStack history, string fallbackPlaceholder);

    DocumentEdit Remove(TextDocument document, UndoStack history, string controlId);

    DocumentEdit InsertBeside(TextDocument document, UndoStack history, string controlId, DocumentPosition at, string value);

    string DateText(TextDocument document, ContentControl control, DateTime date);
}

public sealed class ContentControlEditing(ILiveContentControlCatalog catalog, IStoryCatalog stories, IParagraphText text, IRangeDeletion deletion, IContentTrails trails, IMarkOrder order, PulseOffice.Text.Rich.ITextCultures cultures)
    : IContentControlEditing
{
    private const string DefaultDateFormat = "dd/MM/yyyy";

    public DocumentEdit Insert(TextDocument document, UndoStack history, DocumentRange range, ContentControl control)
    {
        var recorder = new EditRecorder(document, "insert-content-control");
        var id = document.NextId("cc");
        var start = stories.Find(document, range.Start.ParagraphId).Paragraph;
        var end = stories.Find(document, range.End.ParagraphId).Paragraph;
        recorder.Touch(start).Touch(end);
        if (range.IsCollapsed)
        {
            var content = control.Kind == ContentControlKind.CheckBox ? control.CurrentSymbol : control.PlaceholderText ?? string.Empty;
            var showing = control.Kind != ContentControlKind.CheckBox && content.Length > 0;
            var inserted = text.Insert(start.State, range.Start.Offset, content);
            var marks = inserted.Marks.ToList();
            marks.Add(new ContentControlMark(range.Start.Offset, id, MarkEdge.Start) { Control = control with { ShowingPlaceholder = showing } });
            marks.Add(new ContentControlMark(range.Start.Offset + content.Length, id, MarkEdge.End));
            start.State = inserted with { Marks = marks };
            if (content.Length > 0) recorder.Record(new TextInserted(start.Id, range.Start.Offset, content.Length));
            return recorder.Commit(history);
        }
        var block = range.Start.Offset == 0 && range.End.Offset == end.Length && (start.Id != end.Id || control.Block);
        start.State = text.AddMark(start.State, new ContentControlMark(range.Start.Offset, id, MarkEdge.Start) { Control = control with { Block = block, ShowingPlaceholder = false } });
        end.State = text.AddMark(end.State, new ContentControlMark(range.End.Offset, id, MarkEdge.End));
        return recorder.Commit(history);
    }

    public DocumentEdit Change(TextDocument document, UndoStack history, string controlId, ContentControl control)
    {
        var span = Span(document, controlId);
        var recorder = new EditRecorder(document, "content-control-properties");
        var refresh = span.Control.Kind != control.Kind || control.Kind == ContentControlKind.CheckBox && (control.CheckedSymbol != span.Control.CheckedSymbol || control.UncheckedSymbol != span.Control.UncheckedSymbol || control.Checked != span.Control.Checked)
            || span.Control.ShowingPlaceholder && control.PlaceholderText != span.Control.PlaceholderText;
        if (refresh && span.InOneParagraph)
        {
            var value = control.Kind == ContentControlKind.CheckBox ? control.CurrentSymbol
                : span.Control.ShowingPlaceholder || span.Control.Kind == ContentControlKind.CheckBox ? control.PlaceholderText ?? string.Empty : null;
            if (value is not null)
            {
                Replace(document, recorder, span, value, _ => control with { ShowingPlaceholder = control.Kind != ContentControlKind.CheckBox && value.Length > 0 });
                return recorder.Commit(history);
            }
        }
        Update(document, recorder, span, _ => control with { ShowingPlaceholder = span.Control.ShowingPlaceholder && control.Kind != ContentControlKind.CheckBox });
        return recorder.Commit(history);
    }

    public DocumentEdit Toggle(TextDocument document, UndoStack history, string controlId)
    {
        var span = Span(document, controlId);
        var recorder = new EditRecorder(document, "toggle-content-control");
        var toggled = span.Control with { Checked = !span.Control.Checked, ShowingPlaceholder = false };
        Replace(document, recorder, span, toggled.CurrentSymbol, _ => toggled);
        return recorder.Commit(history);
    }

    public DocumentEdit Choose(TextDocument document, UndoStack history, string controlId, string value)
    {
        var span = Span(document, controlId);
        var recorder = new EditRecorder(document, "choose-content-control");
        Replace(document, recorder, span, value, control => control with { ShowingPlaceholder = false });
        return recorder.Commit(history);
    }

    public DocumentEdit SetDate(TextDocument document, UndoStack history, string controlId, DateTime date)
    {
        var span = Span(document, controlId);
        var recorder = new EditRecorder(document, "date-content-control");
        Replace(document, recorder, span, DateText(document, span.Control, date), control => control with { ShowingPlaceholder = false, Date = date });
        return recorder.Commit(history);
    }

    public DocumentEdit SetPicture(TextDocument document, UndoStack history, string controlId, AnchoredObject picture)
    {
        var span = Span(document, controlId);
        var recorder = new EditRecorder(document, "picture-content-control").Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        Replace(document, recorder, span, string.Empty, control => control with { ShowingPlaceholder = false });
        var paragraph = stories.Find(document, span.Start.ParagraphId).Paragraph;
        var marks = paragraph.Marks.ToList();
        var closing = marks.FindIndex(mark => mark is ContentControlMark { Edge: MarkEdge.End } end && end.ControlId == controlId);
        var placed = new AnchoredObject(picture.Id, new AsCharacterAnchor(paragraph.Id), picture.Content, picture.Size);
        marks.Insert(closing < 0 ? marks.Count : closing, new ObjectMark(span.Start.Offset, placed.Id));
        paragraph.State = paragraph.State with { Marks = marks };
        document.Objects.Add(placed);
        return recorder.Commit(history);
    }

    public DocumentEdit Fill(TextDocument document, UndoStack history, string controlId, string value)
    {
        var span = Span(document, controlId);
        var recorder = new EditRecorder(document, "fill-content-control");
        Replace(document, recorder, span, value, control => control with { ShowingPlaceholder = false });
        return recorder.Commit(history);
    }

    public DocumentEdit Refill(TextDocument document, UndoStack history, string fallbackPlaceholder)
    {
        var recorder = new EditRecorder(document, "refill-content-control");
        var touched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var span in catalog.All(document).Where(span => span.IsEmpty && !span.Control.ShowingPlaceholder && !HasObject(document, span)).ToList())
        {
            var control = span.Control;
            var value = control.Kind == ContentControlKind.CheckBox ? control.CurrentSymbol : control.PlaceholderText ?? fallbackPlaceholder;
            if (value.Length == 0) continue;
            if ((touched.Add(span.Start.ParagraphId) ? span : Reread(document, span)) is not { } current) continue;
            Replace(document, recorder, current, value, refilled => refilled with { ShowingPlaceholder = refilled.Kind != ContentControlKind.CheckBox });
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Remove(TextDocument document, UndoStack history, string controlId)
    {
        var recorder = new EditRecorder(document, "remove-content-control");
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => Belongs(mark, controlId))).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => !Belongs(mark, controlId))] };
        }
        return recorder.Commit(history);
    }

    public DocumentEdit InsertBeside(TextDocument document, UndoStack history, string controlId, DocumentPosition at, string value)
    {
        var paragraph = stories.Find(document, at.ParagraphId).Paragraph;
        var recorder = new EditRecorder(document, "insert-text").Touch(paragraph);
        var before = paragraph.State;
        var inserted = text.Insert(before, at.Offset, value);
        var marks = before.Marks.Zip(inserted.Marks, (old, shifted) => old switch
        {
            ContentControlMark { Edge: MarkEdge.End } end when end.ControlId == controlId && end.Offset == at.Offset => old,
            ContentControlMark { Edge: MarkEdge.Start } start when start.ControlId == controlId && start.Offset == at.Offset => old.At(at.Offset + value.Length),
            _ => shifted,
        }).ToList();
        paragraph.State = inserted with { Marks = marks };
        if (value.Length > 0) recorder.Record(new TextInserted(paragraph.Id, at.Offset, value.Length));
        return recorder.Commit(history);
    }

    public string DateText(TextDocument document, ContentControl control, DateTime date)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(control);
        var culture = cultures.For(control.DateLanguage, document.Language);
        try
        {
            return date.ToString(control.DateFormat is { Length: > 0 } format ? format : DefaultDateFormat, culture);
        }
        catch (FormatException)
        {
            return date.ToString(DefaultDateFormat, culture);
        }
    }

    private bool Belongs(InlineMark mark, string controlId) => mark is ContentControlMark control && control.ControlId == controlId;

    private bool HasObject(TextDocument document, ContentControlSpan span) =>
        stories.Find(document, span.Start.ParagraphId).Paragraph.Marks.OfType<ObjectMark>().Any(mark => mark.Offset == span.Start.Offset);

    private ContentControlSpan? Reread(TextDocument document, ContentControlSpan span)
    {
        var marks = stories.Find(document, span.Start.ParagraphId).Paragraph.Marks.OfType<ContentControlMark>().Where(mark => mark.ControlId == span.Id).ToList();
        if (marks.FirstOrDefault(mark => mark.Edge == MarkEdge.Start) is not { } start || marks.FirstOrDefault(mark => mark.Edge == MarkEdge.End) is not { } end)
            return catalog.Find(document, span.Id);
        return span with { Start = span.Start with { Offset = start.Offset }, End = span.End with { Offset = end.Offset } };
    }

    private ContentControlSpan Span(TextDocument document, string controlId) =>
        catalog.Find(document, controlId) ?? throw new KeyNotFoundException($"Content control '{controlId}' not found.");

    private void Update(TextDocument document, EditRecorder recorder, ContentControlSpan span, Func<ContentControl, ContentControl> change)
    {
        var paragraph = stories.Find(document, span.Start.ParagraphId).Paragraph;
        recorder.Touch(paragraph);
        paragraph.State = paragraph.State with
        {
            Marks = [.. paragraph.Marks.Select(mark => mark is ContentControlMark { Edge: MarkEdge.Start } start && start.ControlId == span.Id
                ? start with { Control = change(start.Control ?? new ContentControl()) }
                : mark)],
        };
    }

    private void Replace(TextDocument document, EditRecorder recorder, ContentControlSpan span, string value, Func<ContentControl, ContentControl> change)
    {
        if (!span.InOneParagraph)
        {
            deletion.Delete(document, recorder, trails.Point(document, span.Start), trails.Point(document, span.End));
            span = catalog.Find(document, span.Id) ?? throw new InvalidOperationException("The content control was lost while clearing it.");
        }
        var paragraph = stories.Find(document, span.Start.ParagraphId).Paragraph;
        if (span.End.Offset > span.Start.Offset) deletion.RemoveText(document, recorder, paragraph, span.Start.Offset, span.End.Offset);
        recorder.Touch(paragraph);
        var offset = span.Start.Offset;
        var before = paragraph.State;
        var inserted = value.Length == 0 ? before : text.Insert(before, offset, value);
        var ordered = order.Ordered(before.Marks).ToList();
        var closing = ordered.FindIndex(mark => mark is ContentControlMark { Edge: MarkEdge.End } end && end.ControlId == span.Id);
        var rank = new Dictionary<InlineMark, int>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < ordered.Count; index++) rank.TryAdd(ordered[index], index);
        var marks = before.Marks.Select(old => old switch
        {
            ContentControlMark { Edge: MarkEdge.End } end when end.ControlId == span.Id => end.At(offset + value.Length),
            ContentControlMark { Edge: MarkEdge.Start } start when start.ControlId == span.Id => start with { Offset = offset, Control = change(start.Control ?? new ContentControl()) },
            _ when old.Offset > offset || old.Offset == offset && closing >= 0 && rank[old] > closing => old.At(old.Offset + value.Length),
            _ => old,
        }).ToList();
        paragraph.State = inserted with { Marks = marks };
        if (value.Length > 0) recorder.Record(new TextInserted(paragraph.Id, offset, value.Length));
    }
}
