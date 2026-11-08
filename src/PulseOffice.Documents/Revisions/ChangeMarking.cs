using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Revisions;

public interface IChangeMarking
{
    Insertion AddInsertion(TextDocument document, EditRecorder recorder, DocumentPosition start, DocumentPosition end, RevisionSignature signature);

    Deletion AddDeletion(TextDocument document, EditRecorder recorder, DocumentPosition at, BlockContainer removed, RevisionSignature signature, string? withinInsertion = null);

    FormatChange AddFormat(TextDocument document, EditRecorder recorder, ParagraphBlock paragraph, int start, int end, IReadOnlyList<TextRun> previous, RevisionSignature signature);

    ParagraphFormatChange AddParagraphFormat(TextDocument document, EditRecorder recorder, ParagraphBlock paragraph, TextParagraph previous, RevisionSignature signature);

    void Replace(TextDocument document, EditRecorder recorder, TrackedChange replacement);

    void Remove(TextDocument document, EditRecorder recorder, string changeId);

    void MoveEnd(TextDocument document, EditRecorder recorder, string changeId, DocumentPosition at);

    void Span(TextDocument document, EditRecorder recorder, string changeId, DocumentPosition start, DocumentPosition end);

    void Release(TextDocument document, EditRecorder recorder, string insertionId);

    void Prune(TextDocument document, EditRecorder recorder);
}

public sealed class ChangeMarking(IStoryCatalog stories, IParagraphText text, IChangeRegions regions) : IChangeMarking
{
    private const string Prefix = "ct";

    public Insertion AddInsertion(TextDocument document, EditRecorder recorder, DocumentPosition start, DocumentPosition end, RevisionSignature signature)
    {
        var change = new Insertion(document.NextId(Prefix), signature.Author, signature.Date);
        Log(document, recorder).Add(change);
        Mark(document, recorder, start, new ChangeMark(start.Offset, change.Id, MarkEdge.Start));
        Mark(document, recorder, end, new ChangeMark(end.Offset, change.Id, MarkEdge.End));
        return change;
    }

    public Deletion AddDeletion(TextDocument document, EditRecorder recorder, DocumentPosition at, BlockContainer removed, RevisionSignature signature, string? withinInsertion = null)
    {
        var change = new Deletion(document.NextId(Prefix), signature.Author, signature.Date, removed) { WithinInsertion = withinInsertion };
        Log(document, recorder).Add(change);
        Mark(document, recorder, at, new ChangeMark(at.Offset, change.Id, MarkEdge.Point));
        return change;
    }

    public FormatChange AddFormat(TextDocument document, EditRecorder recorder, ParagraphBlock paragraph, int start, int end, IReadOnlyList<TextRun> previous, RevisionSignature signature)
    {
        var change = new FormatChange(document.NextId(Prefix), signature.Author, signature.Date, previous);
        Log(document, recorder).Add(change);
        recorder.Touch(paragraph);
        paragraph.State = text.AddMark(text.AddMark(paragraph.State, new ChangeMark(start, change.Id, MarkEdge.Start)), new ChangeMark(end, change.Id, MarkEdge.End));
        return change;
    }

    public ParagraphFormatChange AddParagraphFormat(TextDocument document, EditRecorder recorder, ParagraphBlock paragraph, TextParagraph previous, RevisionSignature signature)
    {
        var change = new ParagraphFormatChange(document.NextId(Prefix), signature.Author, signature.Date, previous);
        Log(document, recorder).Add(change);
        recorder.Touch(paragraph);
        paragraph.State = text.AddMark(paragraph.State, new ChangeMark(0, change.Id, MarkEdge.Point));
        return change;
    }

    public void Replace(TextDocument document, EditRecorder recorder, TrackedChange replacement)
    {
        var log = Log(document, recorder);
        log.Replace(log.All.Select(change => change.Id == replacement.Id ? replacement : change));
    }

    public void Remove(TextDocument document, EditRecorder recorder, string changeId)
    {
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => Belongs(mark, changeId))).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => !Belongs(mark, changeId))] };
        }
        Log(document, recorder).Remove(changeId);
    }

    public void MoveEnd(TextDocument document, EditRecorder recorder, string changeId, DocumentPosition at)
    {
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => mark is ChangeMark { Edge: MarkEdge.End } end && end.ChangeId == changeId)).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => mark is not ChangeMark { Edge: MarkEdge.End } end || end.ChangeId != changeId)] };
        }
        Mark(document, recorder, at, new ChangeMark(at.Offset, changeId, MarkEdge.End));
    }

    public void Span(TextDocument document, EditRecorder recorder, string changeId, DocumentPosition start, DocumentPosition end)
    {
        foreach (var paragraph in stories.Paragraphs(document).Where(paragraph => paragraph.Marks.Any(mark => Edge(mark, changeId))).ToList())
        {
            recorder.Touch(paragraph);
            paragraph.State = paragraph.State with { Marks = [.. paragraph.Marks.Where(mark => !Edge(mark, changeId))] };
        }
        Mark(document, recorder, start, new ChangeMark(start.Offset, changeId, MarkEdge.Start));
        Mark(document, recorder, end, new ChangeMark(end.Offset, changeId, MarkEdge.End));
    }

    public void Release(TextDocument document, EditRecorder recorder, string insertionId)
    {
        if (!document.Changes.All.OfType<Deletion>().Any(deletion => deletion.WithinInsertion == insertionId)) return;
        var log = Log(document, recorder);
        log.Replace(log.All.Select(change => change is Deletion deletion && deletion.WithinInsertion == insertionId
            ? new Deletion(deletion.Id, deletion.Author, deletion.Date, deletion.Removed) { Comment = deletion.Comment }
            : change));
    }

    public void Prune(TextDocument document, EditRecorder recorder)
    {
        var holding = document.Changes.All.OfType<Deletion>().Select(deletion => deletion.WithinInsertion).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var region in regions.Map(document).Regions.Where(region => region.IsPoint && region.Change is Insertion or FormatChange && !holding.Contains(region.Change.Id)).ToList())
        {
            Remove(document, recorder, region.Change.Id);
        }
    }

    private void Mark(TextDocument document, EditRecorder recorder, DocumentPosition at, InlineMark mark)
    {
        var paragraph = stories.Find(document, at.ParagraphId).Paragraph;
        recorder.Touch(paragraph);
        paragraph.State = text.AddMark(paragraph.State, mark);
    }

    private TrackedChangeLog Log(TextDocument document, EditRecorder recorder)
    {
        recorder.Touch(document.Changes, () => document.Changes.All, document.Changes.Replace);
        return document.Changes;
    }

    private bool Belongs(InlineMark mark, string changeId) => mark is ChangeMark change && change.ChangeId == changeId;

    private bool Edge(InlineMark mark, string changeId) => mark is ChangeMark { Edge: MarkEdge.Start or MarkEdge.End } change && change.ChangeId == changeId;
}
