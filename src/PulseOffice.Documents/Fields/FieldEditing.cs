using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public sealed record FieldHit(string ParagraphId, int RunIndex, int Start, TextRun Run)
{
    public int End => Start + Run.Text.Length;
}

public interface IFieldEditing
{
    FieldHit? At(TextDocument document, DocumentPosition position);

    DocumentEdit Change(TextDocument document, UndoStack history, FieldHit hit, Func<TextRun, TextRun> change);

    DocumentEdit ToText(TextDocument document, UndoStack history, IReadOnlyList<FieldHit> hits);

    IReadOnlyList<FieldHit> Within(TextDocument document, IEnumerable<ParagraphBlock> paragraphs, DocumentRange? range);
}

public sealed class FieldEditing(IStoryCatalog stories, IFieldRunRewriter rewriter) : IFieldEditing
{
    public FieldHit? At(TextDocument document, DocumentPosition position)
    {
        var paragraph = stories.Find(document, position.ParagraphId).Paragraph;
        FieldHit? before = null;
        var start = 0;
        var runs = paragraph.Content.Runs;
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            var end = start + run.Text.Length;
            if (run.Field != TextFieldKind.None)
            {
                if (position.Offset >= start && position.Offset < end) return new FieldHit(paragraph.Id, i, start, run);
                if (position.Offset == end || run.Text.Length == 0 && position.Offset == start) before = new FieldHit(paragraph.Id, i, start, run);
            }
            start = end;
        }
        return before;
    }

    public IReadOnlyList<FieldHit> Within(TextDocument document, IEnumerable<ParagraphBlock> paragraphs, DocumentRange? range)
    {
        var hits = new List<FieldHit>();
        foreach (var paragraph in paragraphs)
        {
            var start = 0;
            var runs = paragraph.Content.Runs;
            for (var i = 0; i < runs.Count; i++)
            {
                var end = start + runs[i].Text.Length;
                if (runs[i].Field != TextFieldKind.None && Inside(range, paragraph.Id, start, end)) hits.Add(new FieldHit(paragraph.Id, i, start, runs[i]));
                start = end;
            }
        }
        return hits;
    }

    private bool Inside(DocumentRange? range, string paragraphId, int start, int end)
    {
        if (range is not { } r) return true;
        if (r.Start.ParagraphId == paragraphId && end <= r.Start.Offset && !(r.IsCollapsed && end == r.Start.Offset)) return false;
        if (r.End.ParagraphId == paragraphId && start >= r.End.Offset && !(r.IsCollapsed && start == r.End.Offset)) return false;
        return true;
    }

    public DocumentEdit Change(TextDocument document, UndoStack history, FieldHit hit, Func<TextRun, TextRun> change)
    {
        var paragraph = stories.Find(document, hit.ParagraphId).Paragraph;
        var recorder = new EditRecorder(document, "edit-field");
        var runs = paragraph.Content.Runs.ToList();
        if (hit.RunIndex >= runs.Count) return recorder.Commit(history);
        recorder.Touch(paragraph);
        var before = paragraph.Content.Runs;
        runs[hit.RunIndex] = change(runs[hit.RunIndex]);
        foreach (var textChange in rewriter.Changes(paragraph.Id, before, runs)) recorder.Record(textChange);
        paragraph.State = rewriter.Rewrite(paragraph.State, runs);
        return recorder.Commit(history);
    }

    public DocumentEdit ToText(TextDocument document, UndoStack history, IReadOnlyList<FieldHit> hits)
    {
        var recorder = new EditRecorder(document, "field-to-text");
        foreach (var group in hits.GroupBy(hit => hit.ParagraphId))
        {
            var paragraph = stories.Find(document, group.Key).Paragraph;
            recorder.Touch(paragraph);
            var indexes = group.Select(hit => hit.RunIndex).ToHashSet();
            var runs = paragraph.Content.Runs.Select((run, i) => indexes.Contains(i) ? run with { Field = TextFieldKind.None, FieldArgument = null, FieldDetail = null } : run).ToList();
            paragraph.State = paragraph.State with { Content = paragraph.Content.WithRuns(runs) };
        }
        return recorder.Commit(history);
    }
}
