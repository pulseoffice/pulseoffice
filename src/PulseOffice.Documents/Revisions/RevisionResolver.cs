using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.Revisions;

public enum RevisionVerdict
{
    Accept,
    Reject,
    Reinstate,
}

public interface IRevisionResolver
{
    int Resolve(TextDocument document, UndoStack history, IEnumerable<string> changeIds, RevisionVerdict verdict);

    int ResolveAll(TextDocument document, UndoStack history, RevisionVerdict verdict, Func<TrackedChange, bool>? filter = null);

    int ResolveIn(TextDocument document, UndoStack history, DocumentRange range, RevisionVerdict verdict);
}

public sealed class RevisionResolver(
    IDirectDocumentEditor editor,
    IChangeMarking marking,
    IChangeRegions regions,
    IFragmentPaster paster,
    IStoryCatalog stories,
    IParagraphText text,
    ITrackedEditing tracked,
    IRevisionAuthor author,
    ITrackedRows rows,
    IDeletedAnnotations annotations) : IRevisionResolver
{
    public int ResolveAll(TextDocument document, UndoStack history, RevisionVerdict verdict, Func<TrackedChange, bool>? filter = null) =>
        Resolve(document, history, document.Changes.All.Where(change => filter?.Invoke(change) ?? true).Select(change => change.Id).ToList(), verdict);

    public int ResolveIn(TextDocument document, UndoStack history, DocumentRange range, RevisionVerdict verdict) =>
        Resolve(document, history, regions.Map(document).Touching(range).Select(region => region.Change.Id).ToList(), verdict);

    public int Resolve(TextDocument document, UndoStack history, IEnumerable<string> changeIds, RevisionVerdict verdict)
    {
        var wanted = changeIds.Distinct(StringComparer.Ordinal).Where(id => document.Changes.Find(id) is not null).ToList();
        if (wanted.Count == 0) return 0;
        var total = wanted.Count;
        var recording = document.Changes.IsRecording;
        document.Changes.IsRecording = false;
        try
        {
            using var transaction = history.BeginTransaction(verdict switch
            {
                RevisionVerdict.Accept => "accept-change",
                RevisionVerdict.Reinstate => "reinstate-change",
                _ => "reject-change",
            });
            var rowChanges = rows.Ids(document);
            var tracked = wanted.Where(rowChanges.Contains).ToList();
            foreach (var id in tracked) rows.Resolve(document, history, document.Changes.Find(id)!, verdict);
            wanted = [.. wanted.Except(tracked).Where(id => document.Changes.Find(id) is not null)];
            if (verdict == RevisionVerdict.Accept)
            {
                var recorder = new EditRecorder(document, "accept-change");
                foreach (var id in wanted)
                {
                    if (document.Changes.Find(id) is Deletion accepted) annotations.Release(document, recorder, accepted);
                    marking.Release(document, recorder, id);
                    marking.Remove(document, recorder, id);
                }
                marking.Prune(document, recorder);
                recorder.Commit(history);
                return total;
            }
            var map = regions.Map(document);
            var ordered = wanted.Select(id => (Id: id, Region: map.Find(id)))
                .OrderByDescending(item => item.Region is null ? -1 : 1)
                .ThenByDescending(item => item.Region?.Start, Comparer<DocumentPosition?>.Create((a, b) => a is null || b is null ? 0 : map.Compare(a.Value, b.Value)))
                .ToList();
            var signature = author.Sign();
            foreach (var (id, _) in ordered)
            {
                if (verdict == RevisionVerdict.Reinstate && document.Changes.Find(id) is Insertion or Deletion) Reinstate(document, history, id, signature);
                else Reject(document, history, id);
            }
            return total;
        }
        finally
        {
            document.Changes.IsRecording = recording;
        }
    }

    private void Reinstate(TextDocument document, UndoStack history, string id, RevisionSignature signature)
    {
        if (document.Changes.Find(id) is not { } change || regions.Map(document).Find(id) is not { } region)
        {
            Reject(document, history, id);
            return;
        }
        var recorder = new EditRecorder(document, "reinstate-change");
        marking.Remove(document, recorder, id);
        recorder.Commit(history);
        if (change is Insertion && !region.IsPoint)
        {
            tracked.Delete(document, history, new DocumentRange(region.Start, region.End), signature);
            return;
        }
        if (change is not Deletion deletion || deletion.Removed.Count == 0) return;
        var found = stories.Find(document, region.Start.ParagraphId);
        var tail = found.Paragraph.Length - region.Start.Offset;
        var edit = Revive(document, history, region.Start, deletion);
        var location = stories.Find(document, region.Start.ParagraphId).Location;
        var last = location.Index + deletion.Removed.Count - 1 < location.Container.Count ? location.Container[location.Index + deletion.Removed.Count - 1] as Blocks.ParagraphBlock : null;
        if (last is null || deletion.Removed.Blocks.Any(block => block is not Blocks.ParagraphBlock)) return;
        tracked.MarkInserted(document, history, edit, region.Start, new DocumentPosition(last.Id, last.Length - tail), signature);
    }

    private void Reject(TextDocument document, UndoStack history, string id)
    {
        if (document.Changes.Find(id) is not { } change) return;
        var region = regions.Map(document).Find(id);
        var recorder = new EditRecorder(document, "reject-change");
        switch (change)
        {
            case FormatChange format when region is not null && format.PreviousKnown && region.Start.ParagraphId == region.End.ParagraphId:
                var paragraph = stories.Find(document, region.Start.ParagraphId).Paragraph;
                recorder.Touch(paragraph);
                paragraph.State = text.Restyle(paragraph.State, region.Start.Offset, region.End.Offset, format.PreviousRuns);
                break;
            case ParagraphFormatChange layout when region is not null && layout.PreviousKnown:
                var target = stories.Find(document, region.Start.ParagraphId).Paragraph;
                recorder.Touch(target);
                target.State = target.State with { Content = layout.Previous.WithRuns(target.State.Content.Runs) };
                break;
        }
        if (change is Insertion)
        {
            foreach (var nested in document.Changes.All.OfType<Deletion>().Where(deletion => deletion.WithinInsertion == id).ToList())
            {
                annotations.Release(document, recorder, nested);
                marking.Remove(document, recorder, nested.Id);
            }
        }
        marking.Remove(document, recorder, id);
        recorder.Commit(history);
        if (region is null) return;
        switch (change)
        {
            case Insertion when !region.IsPoint:
                editor.Delete(document, history, new DocumentRange(region.Start, region.End));
                break;
            case Deletion deletion when deletion.Removed.Count > 0:
                Restore(document, history, region.Start, deletion);
                break;
        }
    }

    private DocumentEdit Revive(TextDocument document, UndoStack history, DocumentPosition at, Deletion deletion)
    {
        var edit = paster.Paste(document, history, at, annotations.Revival(document, deletion));
        var recorder = new EditRecorder(document, "reject-change");
        annotations.Release(document, recorder, deletion);
        recorder.Commit(history);
        return edit;
    }

    private void Restore(TextDocument document, UndoStack history, DocumentPosition at, Deletion deletion)
    {
        var tail = stories.Find(document, at.ParagraphId).Paragraph.Length - at.Offset;
        Revive(document, history, at, deletion);
        if (deletion.WithinInsertion is not { } within || document.Changes.Find(within) is not Insertion) return;
        var location = stories.Find(document, at.ParagraphId).Location;
        var index = location.Index + deletion.Removed.Count - 1;
        if (index >= location.Container.Count || location.Container[index] is not Blocks.ParagraphBlock last) return;
        var end = new DocumentPosition(last.Id, last.Length - tail);
        var map = regions.Map(document);
        var start = at;
        if (map.Find(within) is { } current)
        {
            if (map.Compare(current.Start, start) < 0) start = current.Start;
            if (map.Compare(current.End, end) > 0) end = current.End;
        }
        var recorder = new EditRecorder(document, "reject-change");
        marking.Span(document, recorder, within, start, end);
        recorder.Commit(history);
    }
}
