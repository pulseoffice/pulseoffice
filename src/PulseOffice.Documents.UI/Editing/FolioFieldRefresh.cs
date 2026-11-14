using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioFieldRefresh
{
    IFieldLayoutFacts Facts(FolioEditor editor);

    bool UpdateAll(FolioEditor editor);

    bool UpdateFields(FolioEditor editor, IReadOnlySet<string>? paragraphs = null);

    bool UpdateIndexes(FolioEditor editor, string? sectionId = null);

    void ShowMergeRecord(FolioEditor editor);
}

public sealed class FolioFieldRefresh(IFieldUpdater fields, IIndexEditing indexes, IFieldLayoutFactsFactory facts, TimeProvider clock) : IFolioFieldRefresh
{
    private const int Passes = 3;

    private readonly IReadOnlySet<TextFieldKind> _mergeKinds = new HashSet<TextFieldKind> { TextFieldKind.Database };

    public IFieldLayoutFacts Facts(FolioEditor editor) => facts.Create(editor.Document, editor.Layout);

    public bool UpdateAll(FolioEditor editor) => Run(editor, "update-all", (recorder, current) =>
    {
        indexes.Refresh(editor.Document, recorder, current);
        fields.Refresh(editor.Document, recorder, Request(current));
    });

    public bool UpdateFields(FolioEditor editor, IReadOnlySet<string>? paragraphs = null) => Run(editor, "update-fields", (recorder, current) =>
        fields.Refresh(editor.Document, recorder, Request(current) with { Paragraphs = paragraphs }));

    public bool UpdateIndexes(FolioEditor editor, string? sectionId = null) => Run(editor, "update-index", (recorder, current) =>
        indexes.Refresh(editor.Document, recorder, current, sectionId));

    public void ShowMergeRecord(FolioEditor editor)
    {
        var recorder = new PulseOffice.Documents.Editing.EditRecorder(editor.Document, "merge-record");
        fields.Refresh(editor.Document, recorder, Request(Facts(editor)) with { Kinds = _mergeKinds });
        recorder.Commit(new PulseOffice.Document.Commands.UndoStack());
        editor.Refresh();
    }

    private FieldUpdate Request(IFieldLayoutFacts current) => new(current, clock.GetLocalNow().DateTime);

    private bool Run(FolioEditor editor, string description, Action<PulseOffice.Documents.Editing.EditRecorder, IFieldLayoutFacts> refresh)
    {
        if (editor.ReadOnly()) return false;
        var any = false;
        using (editor.History.BeginTransaction(description))
        {
            for (var pass = 0; pass < Passes; pass++)
            {
                var recorder = new PulseOffice.Documents.Editing.EditRecorder(editor.Document, description);
                refresh(recorder, Facts(editor));
                var changed = recorder.HasChanges;
                recorder.Commit(editor.History);
                any |= changed;
                if (!changed) break;
            }
        }
        editor.Refresh();
        return any;
    }
}

public sealed class FolioFieldWords(PulseOffice.Localization.ITextCatalog texts) : IFieldWords
{
    public string Above => texts["folio.reference.above"];

    public string Below => texts["folio.reference.below"];

    public string MissingReference => texts["folio.reference.missing"];
}
