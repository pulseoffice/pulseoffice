using System.Globalization;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Fields;

public interface IFolioFieldInsertion
{
    void Insert(FolioEditor editor, TextRun run);

    bool InsertReference(FolioEditor editor, ReferenceTarget target, string format);

    void Replace(FolioEditor editor, FieldHit hit, TextRun run);

    bool ReplaceReference(FolioEditor editor, FieldHit hit, ReferenceTarget target, string format);
}

public sealed class FolioFieldInsertion(IFieldEditing fields, IReferenceTargets targets, IFolioFieldRefresh refresh, TimeProvider clock) : IFolioFieldInsertion
{
    public void Insert(FolioEditor editor, TextRun run)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(run);
        var shown = FixedText(editor, run, clock.GetLocalNow().DateTime) ?? (run.Text is { Length: > 0 } text ? text : "…");
        using (editor.History.BeginTransaction("insert-field"))
        {
            var at = editor.Caret;
            editor.Insert((documents, position) =>
            {
                at = position;
                documents.InsertField(editor.Document, editor.History, position, run.Field, shown, run.FieldArgument);
            }, "insert-field");
            if (fields.At(editor.Document, at) is { } hit && hit.Run.Field == run.Field)
                fields.Change(editor.Document, editor.History, hit, current => current with { FieldDetail = run.FieldDetail });
            refresh.UpdateFields(editor, new HashSet<string>(StringComparer.Ordinal) { at.ParagraphId });
        }
        editor.Refresh();
    }

    public bool InsertReference(FolioEditor editor, ReferenceTarget target, string format)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(target);
        if (editor.RefusesEdit() || RefusesTarget(editor, target)) return false;
        using (editor.History.BeginTransaction("insert-reference"))
            Insert(editor, Reference(editor, target, format));
        return true;
    }

    public void Replace(FolioEditor editor, FieldHit hit, TextRun run)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(hit);
        if (editor.RefusesEdit(hit.ParagraphId)) return;
        using (editor.History.BeginTransaction("edit-field"))
        {
            editor.Change(() => fields.Change(editor.Document, editor.History, hit, current => run with { Text = FixedText(editor, run, null) ?? current.Text }));
            refresh.UpdateFields(editor, new HashSet<string>(StringComparer.Ordinal) { hit.ParagraphId });
        }
    }

    public bool ReplaceReference(FolioEditor editor, FieldHit hit, ReferenceTarget target, string format)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(hit);
        if (editor.RefusesEdit(hit.ParagraphId) || RefusesTarget(editor, target)) return false;
        using (editor.History.BeginTransaction("edit-field"))
            Replace(editor, hit, Reference(editor, target, format));
        return true;
    }

    private string? FixedText(FolioEditor editor, TextRun run, DateTime? unstored)
    {
        if (run.Field is not (TextFieldKind.Date or TextFieldKind.Time) || run.FieldDetail is not { Fixed: true } detail) return null;
        DateTime moment;
        if (DateTime.TryParse(detail.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stored)) moment = stored;
        else if (unstored is { } now) moment = now;
        else return null;
        var fallback = run.Field == TextFieldKind.Date ? "d" : "t";
        try
        {
            return moment.ToString(detail.Format is { Length: > 0 } format ? format : fallback, editor.Culture());
        }
        catch (FormatException)
        {
            return moment.ToString(fallback, editor.Culture());
        }
    }

    private bool RefusesTarget(FolioEditor editor, ReferenceTarget target) =>
        targets.Touches(editor.Document, target) is { } paragraph && editor.RefusesEdit(paragraph);

    private TextRun Reference(FolioEditor editor, ReferenceTarget target, string format)
    {
        var recorder = new EditRecorder(editor.Document, "insert-reference");
        var name = targets.Prepare(editor.Document, recorder, target);
        recorder.Commit(editor.History);
        var notes = target.Kind is ReferenceTargetKind.Footnotes or ReferenceTargetKind.Endnotes;
        return new TextRun(string.Empty)
        {
            Field = notes ? TextFieldKind.NoteReference : TextFieldKind.Bookmark,
            FieldArgument = name,
            FieldDetail = format == ReferenceFormats.Text ? null : new FieldDetail { Format = format },
        };
    }
}
