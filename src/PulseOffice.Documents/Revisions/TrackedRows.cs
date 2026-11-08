using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.Revisions;

public sealed record TrackedRow(TableBlock Table, TableRow Row);

public interface ITrackedRows
{
    IReadOnlyList<TrackedRow> Of(TextDocument document, string changeId);

    IReadOnlySet<string> Ids(TextDocument document);

    void Resolve(TextDocument document, UndoStack history, TrackedChange change, RevisionVerdict verdict);
}

public sealed class TrackedRows(IStoryCatalog stories, IBlockTree tree, ITableEditing tables, IChangeMarking marking, IDeletedAnnotations annotations) : ITrackedRows
{
    public IReadOnlyList<TrackedRow> Of(TextDocument document, string changeId) =>
        [.. stories.Stories(document).SelectMany(tree.Blocks).OfType<TableBlock>()
            .SelectMany(table => table.Rows.Where(row => row.ChangeId == changeId).Select(row => new TrackedRow(table, row)))];

    public IReadOnlySet<string> Ids(TextDocument document) =>
        stories.Stories(document).SelectMany(tree.Blocks).OfType<TableBlock>().SelectMany(table => table.Rows).Select(row => row.ChangeId).OfType<string>().ToHashSet(StringComparer.Ordinal);

    public void Resolve(TextDocument document, UndoStack history, TrackedChange change, RevisionVerdict verdict)
    {
        var keep = change is Insertion == (verdict == RevisionVerdict.Accept);
        if (keep)
        {
            var recorder = new EditRecorder(document, verdict == RevisionVerdict.Accept ? "accept-change" : "reject-change");
            foreach (var tracked in Of(document, change.Id))
            {
                recorder.Touch(tracked.Table);
                tracked.Row.ChangeId = null;
            }
            marking.Remove(document, recorder, change.Id);
            recorder.Commit(history);
            return;
        }
        var inner = Of(document, change.Id).SelectMany(tracked => tracked.Row.Cells).SelectMany(cell => tree.Paragraphs(cell.Content))
            .SelectMany(paragraph => paragraph.Marks).OfType<Marks.ChangeMark>().Select(mark => mark.ChangeId).ToHashSet(StringComparer.Ordinal);
        foreach (var tracked in Of(document, change.Id))
        {
            if (Location(document, tracked) is { } at) tables.DeleteRow(document, history, at);
        }
        var removal = new EditRecorder(document, verdict == RevisionVerdict.Accept ? "accept-change" : "reject-change");
        foreach (var tracked in Of(document, change.Id))
        {
            removal.Touch(tracked.Table);
            tracked.Row.ChangeId = null;
        }
        marking.Remove(document, removal, change.Id);
        var referenced = stories.Paragraphs(document).SelectMany(paragraph => paragraph.Marks).OfType<Marks.ChangeMark>().Select(mark => mark.ChangeId).ToHashSet(StringComparer.Ordinal);
        foreach (var orphan in inner.Where(id => !referenced.Contains(id) && Of(document, id).Count == 0).ToList())
        {
            if (document.Changes.Find(orphan) is Deletion deletion) annotations.Release(document, removal, deletion);
            marking.Remove(document, removal, orphan);
        }
        removal.Commit(history);
    }

    private TableCellLocation? Location(TextDocument document, TrackedRow tracked) =>
        tables.LocateRow(document, tracked.Table, tracked.Table.Rows.IndexOf(tracked.Row));
}
