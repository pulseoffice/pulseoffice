using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public interface IFieldRunRewriter
{
    ParagraphState Rewrite(ParagraphState state, IReadOnlyList<TextRun> runs);

    IReadOnlyList<TextChange> Changes(string paragraphId, IReadOnlyList<TextRun> before, IReadOnlyList<TextRun> after);
}

public sealed class FieldRunRewriter : IFieldRunRewriter
{
    public IReadOnlyList<TextChange> Changes(string paragraphId, IReadOnlyList<TextRun> before, IReadOnlyList<TextRun> after)
    {
        var changes = new List<TextChange>();
        var position = 0;
        for (var i = 0; i < before.Count && i < after.Count; i++)
        {
            var was = before[i].Text.Length;
            var now = after[i].Text.Length;
            if (now > was) changes.Add(new TextInserted(paragraphId, position + was, now - was));
            else if (now < was) changes.Add(new TextDeleted(paragraphId, position + now, was - now));
            position += now;
        }
        return changes;
    }

    public ParagraphState Rewrite(ParagraphState state, IReadOnlyList<TextRun> runs)
    {
        var before = state.Content.Runs;
        var spans = new List<(int Start, int End, int Delta, int Length)>();
        var position = 0;
        for (var i = 0; i < before.Count && i < runs.Count; i++)
        {
            var was = before[i].Text.Length;
            if (was != runs[i].Text.Length) spans.Add((position, position + was, runs[i].Text.Length - was, runs[i].Text.Length));
            position += was;
        }
        var length = runs.Sum(run => run.Text.Length);
        var marks = state.Marks.Select(mark => mark.At(Math.Clamp(Moved(mark.Offset, spans), 0, length))).ToList();
        return new ParagraphState(state.Content.WithRuns(runs), marks);
    }

    private int Moved(int offset, IReadOnlyList<(int Start, int End, int Delta, int Length)> spans)
    {
        var shift = 0;
        foreach (var span in spans)
        {
            if (offset >= span.End) shift += span.Delta;
            else if (offset > span.Start) return span.Start + shift + Math.Min(offset - span.Start, span.Length);
            else break;
        }
        return offset + shift;
    }
}

public interface IDocumentReadingOrder
{
    IEnumerable<ParagraphBlock> Paragraphs(TextDocument document);

    IEnumerable<ParagraphBlock> BodyWithFrames(TextDocument document);
}

public sealed class DocumentReadingOrder(IBlockTree tree, IStoryCatalog stories) : IDocumentReadingOrder
{
    public IEnumerable<ParagraphBlock> BodyWithFrames(TextDocument document)
    {
        foreach (var paragraph in tree.Paragraphs(document.Body))
        {
            yield return paragraph;
            foreach (var item in document.Objects.AnchoredTo(paragraph.Id).OrderBy(item => item.Placement.Y.Value))
            {
                if (item.Content.Story is not { } story) continue;
                foreach (var inner in tree.Paragraphs(story)) yield return inner;
            }
        }
    }

    public IEnumerable<ParagraphBlock> Paragraphs(TextDocument document)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var paragraph in BodyWithFrames(document))
            if (seen.Add(paragraph.Id)) yield return paragraph;
        foreach (var paragraph in stories.Paragraphs(document))
            if (seen.Add(paragraph.Id)) yield return paragraph;
    }
}

public interface IFieldUpdater
{
    bool Present(TextDocument document, IReadOnlySet<TextFieldKind>? kinds = null);

    void Refresh(TextDocument document, EditRecorder recorder, FieldUpdate request);

    DocumentEdit Update(TextDocument document, UndoStack history, FieldUpdate request);

    string Preview(TextDocument document, TextRun run, FieldUpdate request, DocumentPosition at);
}

public sealed class FieldUpdater(IEnumerable<IFieldValueSource> sources, IDocumentReadingOrder order, IFieldRunRewriter rewriter, IDocumentOutline outline) : IFieldUpdater
{
    private readonly IReadOnlyDictionary<TextFieldKind, IFieldValueSource> _sources =
        sources.SelectMany(source => source.Kinds.Select(kind => (kind, source))).ToDictionary(pair => pair.kind, pair => pair.source);

    public bool Present(TextDocument document, IReadOnlySet<TextFieldKind>? kinds = null) =>
        order.Paragraphs(document).Any(paragraph => paragraph.Content.Runs.Any(run => run.Field != TextFieldKind.None && (kinds is null || kinds.Contains(run.Field))));

    public DocumentEdit Update(TextDocument document, UndoStack history, FieldUpdate request)
    {
        var recorder = new EditRecorder(document, "update-fields");
        Refresh(document, recorder, request);
        return recorder.Commit(history);
    }

    public void Refresh(TextDocument document, EditRecorder recorder, FieldUpdate request)
    {
        var paragraphs = order.Paragraphs(document).ToList();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < paragraphs.Count; i++) positions.TryAdd(paragraphs[i].Id, i);
        var context = new FieldContext(document, request, positions);
        var body = new HashSet<string>(order.BodyWithFrames(document).Select(paragraph => paragraph.Id), StringComparer.Ordinal);
        foreach (var paragraph in paragraphs)
        {
            context.Paragraph = paragraph;
            if (body.Contains(paragraph.Id) && outline.LevelOf(document, paragraph) == 1) context.Chapter = new OutlineEntry(paragraph, 1);
            var runs = paragraph.Content.Runs;
            if (!runs.Any(run => run.Field != TextFieldKind.None)) continue;
            var rewritten = new List<TextRun>(runs.Count);
            var offset = 0;
            foreach (var run in runs)
            {
                context.Offset = offset;
                offset += run.Text.Length;
                rewritten.Add(Evaluate(run, context));
            }
            if (request.Paragraphs is { } only && !only.Contains(paragraph.Id)) continue;
            if (request.Kinds is { } kinds)
                rewritten = [.. rewritten.Select((run, i) => kinds.Contains(runs[i].Field) ? run : runs[i])];
            if (rewritten.SequenceEqual(runs)) continue;
            recorder.Touch(paragraph);
            foreach (var change in rewriter.Changes(paragraph.Id, runs, rewritten)) recorder.Record(change);
            paragraph.State = rewriter.Rewrite(paragraph.State, rewritten);
        }
    }

    public string Preview(TextDocument document, TextRun run, FieldUpdate request, DocumentPosition at)
    {
        var paragraphs = order.Paragraphs(document).ToList();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < paragraphs.Count; i++) positions.TryAdd(paragraphs[i].Id, i);
        var context = new FieldContext(document, request, positions);
        var body = new HashSet<string>(order.BodyWithFrames(document).Select(paragraph => paragraph.Id), StringComparer.Ordinal);
        foreach (var paragraph in paragraphs)
        {
            context.Paragraph = paragraph;
            if (body.Contains(paragraph.Id) && outline.LevelOf(document, paragraph) == 1) context.Chapter = new OutlineEntry(paragraph, 1);
            var offset = 0;
            var here = paragraph.Id == at.ParagraphId;
            foreach (var existing in paragraph.Content.Runs)
            {
                if (here && offset >= at.Offset) break;
                context.Offset = offset;
                offset += existing.Text.Length;
                Evaluate(existing, context);
            }
            if (!here) continue;
            context.Offset = at.Offset;
            return Evaluate(run, context).Text;
        }
        if (paragraphs.Count > 0) context.Paragraph = paragraphs[^1];
        return Evaluate(run, context).Text;
    }

    private TextRun Evaluate(TextRun run, FieldContext context)
    {
        if (run.Field == TextFieldKind.None || !_sources.TryGetValue(run.Field, out var source)) return run;
        if (source.Evaluate(run, context) is not { } value) return run;
        var text = value.Text.ReplaceLineEndings(" ");
        return run with { Text = text, Hidden = value.Hidden ?? run.Hidden };
    }
}
